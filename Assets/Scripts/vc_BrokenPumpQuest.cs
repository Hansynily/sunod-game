using System.Collections;
using UnityEngine;

/// <summary>
/// R quest (R6). Fix the yard pump. Any APPROPRIATE skill works - how well it fits decides
/// how fast it goes (and so the stars); skills that make no sense here do nothing.
///
///   repair   R  best fit     tighten the cracked joint                    ~1s
///   push     R  workaround   work the stuck handle loose: 3 presses        ~3-4s
///   build    R  workaround   build a bypass pipe round the crack           5s
///   plan / survey / inspect  I/I/C  figure out the fault, fix by hand      8s
///   summon   E  call the plumber; he walks over and fixes it               ~10s
///   anything else: a hint, no effect
///
/// While a slow fix is running, using the best-fit skill (repair) finishes it at once.
/// Skill-zone gates WHERE the skill can be pressed, so no proximity check here.
/// </summary>
[DisallowMultipleComponent]
public class vc_BrokenPumpQuest : MonoBehaviour, vc_IQuestLogic
{
    [Header("Pump art")]
    [SerializeField] private SpriteRenderer pump;
    [SerializeField] private Sprite pumpBroken;
    [SerializeField] private Sprite pumpFixed;
    [SerializeField] private Sprite pumpBypass;
    [SerializeField] private Sprite waterDrop;

    [Header("Plumber (summon path)")]
    [SerializeField] private Sprite[] plumberWalk;
    [SerializeField] private float plumberPpuScale = 1f;

    [Header("Legacy (unused)")]
    [SerializeField] private GameObject brokenPump;
    [SerializeField] private GameObject fixedPump;
    [SerializeField] private vc_FloatingMarker mainMarker;
    [SerializeField] private float animDuration = 0.5f;

    [Header("Timing (seconds)")]
    [SerializeField] private float repairTime = 1f;
    [SerializeField] private int pushPresses = 3;
    [SerializeField] private float buildTime = 5f;
    [SerializeField] private float figureOutTime = 8f;
    [SerializeField] private float plumberWalkTime = 6f;
    [SerializeField] private float plumberFixTime = 4f;

    private vc_QuestRoom _questRoom;
    private bool _started, _done, _working;
    private int _pushes;
    private Coroutine _work;
    private Transform _bar, _barFill;
    private SpriteRenderer _plumber;
    private Vector3 _pumpBase;
    private float _dripClock;

    private void OnDestroy() => Unsubscribe();

    public void BeginQuest(vc_QuestRoom activeQuestRoom, vc_QuestTimer questTimer)
    {
        _questRoom = activeQuestRoom;
        _started = true;
        _done = false;
        _working = false;
        _pushes = 0;

        if (pump != null)
        {
            pump.sprite = pumpBroken;
            _pumpBase = pump.transform.localPosition;
        }
        Subscribe();

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Broken Pump",
            "The yard pump is broken. Get it working again.",
            new[] { "Fix the water pump" });
    }

    private void Update()
    {
        // a slow drip while the pump is broken
        if (!_started || _done || pump == null || waterDrop == null) return;
        _dripClock += Time.deltaTime;
        if (_dripClock >= 0.9f) { _dripClock = 0f; StartCoroutine(Drip()); }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!_started || _done || skill == null || skill.SkillData == null) return;
        vc_SkillData d = skill.SkillData;

        // best fit always wins, even over a slow fix already running
        if (d.HasTag("repair"))
        {
            StopWork();
            _work = StartCoroutine(Fix("You tighten the cracked joint. Good as new!", repairTime, pumpFixed, shake: true));
            return;
        }

        if (_working)
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("Already working on it...", 1.5f);
            return;
        }

        if (d.HasTag("push"))
        {
            _pushes++;
            if (_pushes < pushPresses)
            {
                vc_QuestHUD.Instance?.ShowFeedbackTimed($"You heave the stuck handle... it budges ({_pushes}/{pushPresses})", 2f);
                StartCoroutine(Shake(0.35f, 2));
                SunodGame.Core.vc_Sfx.Play("step_done", 0.6f, 0.05f);
            }
            else
            {
                _work = StartCoroutine(Fix("The handle pops free. Water flows again!", 0.6f, pumpFixed, shake: true));
            }
            return;
        }

        if (d.HasTag("build"))
        {
            _work = StartCoroutine(Fix("You build a bypass pipe around the crack. Slow, but it works.", buildTime, pumpBypass ?? pumpFixed, shake: true,
                                       startMsg: "Building a bypass pipe..."));
            return;
        }

        if (d.HasTag("plan") || d.HasTag("survey") || d.HasTag("inspect"))
        {
            _work = StartCoroutine(Fix("Found it: a loose valve. You fix it by hand.", figureOutTime, pumpFixed, shake: false,
                                       startMsg: "Figuring out what's wrong..."));
            return;
        }

        if (d.HasTag("summon"))
        {
            _work = StartCoroutine(CallPlumber());
            return;
        }

        vc_QuestHUD.Instance?.ShowFeedbackTimed("That won't fix a pump. Something hands-on might.", 2.2f);
    }

    // ── Fix routines ─────────────────────────────────────────────────────

    private IEnumerator Fix(string doneMsg, float seconds, Sprite result, bool shake, string startMsg = null)
    {
        _working = true;
        if (!string.IsNullOrEmpty(startMsg)) vc_QuestHUD.Instance?.ShowFeedbackTimed(startMsg, Mathf.Max(1.5f, seconds));
        ShowBar(true);

        float t = 0f, tick = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar(t / seconds);
            if (tick >= 0.45f)
            {
                tick = 0f;
                SunodGame.Core.vc_Sfx.Play("timer_tick", 0.35f, 0.1f);
                if (shake) StartCoroutine(Shake(0.15f, 1));
            }
            yield return null;
        }

        ShowBar(false);
        Finish(doneMsg, result);
    }

    private IEnumerator CallPlumber()
    {
        _working = true;
        vc_QuestHUD.Instance?.ShowFeedbackTimed("You call the plumber. He's on his way...", 3f);
        ShowBar(true);

        // plumber walks in from the room's left side to the pump
        Vector3 to = pump != null ? pump.transform.position + new Vector3(-0.9f, -0.3f, 0f) : transform.position;
        Vector3 from = to + new Vector3(-6f, 0f, 0f);
        _plumber = MakePlumber(from);

        float total = plumberWalkTime + plumberFixTime, t = 0f;
        while (t < plumberWalkTime)
        {
            t += Time.deltaTime;
            SetBar(t / total);
            if (_plumber != null)
            {
                _plumber.transform.position = Snap(Vector3.Lerp(from, to, t / plumberWalkTime));
                if (plumberWalk != null && plumberWalk.Length > 0)
                    _plumber.sprite = plumberWalk[(int)(t * 8f) % plumberWalk.Length];
            }
            yield return null;
        }

        if (_plumber != null && plumberWalk != null && plumberWalk.Length > 0) _plumber.sprite = plumberWalk[0];
        vc_QuestHUD.Instance?.ShowFeedbackTimed("The plumber gets to work...", plumberFixTime);
        float tick = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            tick += Time.deltaTime;
            SetBar(t / total);
            if (tick >= 0.45f) { tick = 0f; SunodGame.Core.vc_Sfx.Play("timer_tick", 0.35f, 0.1f); StartCoroutine(Shake(0.15f, 1)); }
            yield return null;
        }

        ShowBar(false);
        Finish("The plumber fixed it. Thanks for calling him!", pumpFixed);
    }

    private void Finish(string msg, Sprite result)
    {
        if (_done) return;
        _done = true;
        _working = false;
        Unsubscribe();
        if (pump != null)
        {
            pump.transform.localPosition = _pumpBase;
            if (result != null) pump.sprite = result;
        }
        SunodGame.Core.vc_Sfx.Play("step_done", 0.8f);
        vc_FloatingMessage.Instance?.Show(msg);
        StartCoroutine(CompleteAfter(0.6f));
    }

    private IEnumerator CompleteAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _started = false;
        vc_QuestHUD.Instance?.CheckObjective(0);
        mainMarker?.Hide();
        _questRoom?.OnQuestComplete();
    }

    private void StopWork()
    {
        if (_work != null) StopCoroutine(_work);
        _work = null;
        _working = false;
        ShowBar(false);
        if (_plumber != null) Destroy(_plumber.gameObject);
        if (pump != null) pump.transform.localPosition = _pumpBase;
    }

    // ── Visual helpers ───────────────────────────────────────────────────

    private IEnumerator Shake(float duration, int px)
    {
        if (pump == null) yield break;
        float t = 0f;
        int i = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float off = (i++ % 2 == 0 ? 1 : -1) * px / 22f;
            pump.transform.localPosition = _pumpBase + new Vector3(off, 0f, 0f);
            yield return new WaitForSeconds(0.04f);
        }
        pump.transform.localPosition = _pumpBase;
    }

    private IEnumerator Drip()
    {
        var go = new GameObject("Drip");
        go.transform.SetParent(pump.transform, false);
        go.transform.localPosition = new Vector3(-0.3f, 0.95f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = waterDrop;
        sr.sortingLayerName = pump.sortingLayerName;
        sr.sortingOrder = pump.sortingOrder + 1;
        float t = 0f;
        while (t < 0.5f && go != null)
        {
            t += Time.deltaTime;
            go.transform.localPosition = new Vector3(-0.3f, 0.95f - Mathf.Round(t * 1.8f * 22f) / 22f, 0f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private void ShowBar(bool on)
    {
        if (on && _bar == null && pump != null)
        {
            Sprite px = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            _bar = new GameObject("WorkBar").transform;
            _bar.SetParent(pump.transform, false);
            _bar.localPosition = new Vector3(-0.5f, 1.65f, 0f);
            var bg = _bar.gameObject.AddComponent<SpriteRenderer>();
            bg.sprite = px; bg.color = new Color32(46, 34, 40, 230);
            bg.sortingLayerName = pump.sortingLayerName; bg.sortingOrder = pump.sortingOrder + 5;
            _bar.localScale = new Vector3(1f, 0.16f, 1f);

            _barFill = new GameObject("Fill").transform;
            _barFill.SetParent(_bar, false);
            _barFill.localPosition = new Vector3(0.03f, 0f, 0f);
            var fg = _barFill.gameObject.AddComponent<SpriteRenderer>();
            fg.sprite = px; fg.color = new Color32(255, 204, 66, 255);
            fg.sortingLayerName = pump.sortingLayerName; fg.sortingOrder = pump.sortingOrder + 6;
            _barFill.localScale = new Vector3(0f, 0.6f, 1f);
        }
        if (_bar != null) _bar.gameObject.SetActive(on);
        if (on) SetBar(0f);
    }

    private void SetBar(float k)
    {
        if (_barFill == null) return;
        k = Mathf.Clamp01(k);
        _barFill.localScale = new Vector3(Mathf.Round(k * 30f) / 30f * 0.94f, 0.6f, 1f);
    }

    private SpriteRenderer MakePlumber(Vector3 at)
    {
        var go = new GameObject("Plumber");
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(at);
        go.transform.localScale = Vector3.one * plumberPpuScale;
        var sr = go.AddComponent<SpriteRenderer>();
        if (plumberWalk != null && plumberWalk.Length > 0) sr.sprite = plumberWalk[0];
        sr.sortingLayerName = pump != null ? pump.sortingLayerName : "Default";
        sr.sortingOrder = pump != null ? pump.sortingOrder + 2 : 5;
        return sr;
    }

    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, p.z);

    private void Subscribe()
    {
        if (vc_SkillManager.Instance == null) return;
        vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        vc_SkillManager.Instance.SkillUsed += HandleSkillUsed;
    }

    private void Unsubscribe()
    {
        if (vc_SkillManager.Instance != null)
            vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
    }
}
