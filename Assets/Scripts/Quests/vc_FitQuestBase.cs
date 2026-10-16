using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Shared plumbing for hand-built "skill fit" quests (see vc_CreakyFloorQuest). Handles the
/// skill subscription, the "already working" lock, best-fit takeover, the progress bar,
/// pixel-snapped shake, one-shot sprite effects, a walking helper NPC, and the finish flow.
/// A quest script only decides what each skill DOES (OnSkill) and how the props look.
/// Skill zones gate WHERE skills can be pressed, so no proximity checks here.
/// </summary>
public abstract class vc_FitQuestBase : MonoBehaviour, vc_IQuestLogic
{
    [Header("Shared")]
    [SerializeField] protected vc_FloatingMarker mainMarker;
    [Tooltip("Town NPC walk frames for the 'Call' helper (npc_<look>_f0..f5).")]
    [SerializeField] protected Sprite[] helperWalk;
    [SerializeField] protected string sortingLayer = "Default";

    protected vc_QuestRoom QuestRoom;
    protected bool Started, Done, Working;

    private vc_QuestTimer _timer;
    private Coroutine _work;
    private Transform _bar, _barFill;
    private SpriteRenderer _helper;

    // ── Quest-specific hooks ─────────────────────────────────────────────

    protected abstract void OnBegin();
    protected abstract void OnSkill(vc_SkillData skill);
    /// <summary>Put props back after a cancelled slow fix (bar and helper are cleaned up here).</summary>
    protected virtual void OnWorkCancelled() { }
    /// <summary>World position the progress bar's left edge sits at.</summary>
    protected abstract Vector3 BarWorldPosition { get; }

    // ── Lifecycle ────────────────────────────────────────────────────────

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    public void BeginQuest(vc_QuestRoom questRoom, vc_QuestTimer questTimer)
    {
        CancelWork();
        QuestRoom = questRoom;
        Started = true;
        Done = false;
        Working = false;

        if (_timer != null) _timer.QuestEnded -= HandleQuestEnded;
        _timer = questTimer;
        if (_timer != null) _timer.QuestEnded += HandleQuestEnded;

        OnBegin();
        Subscribe();
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill skill)
    {
        if (!Started || Done || skill == null || skill.SkillData == null) return;
        OnSkill(skill.SkillData);
    }

    // The timer ends a failed quest without going through Finish; stop reacting to skills.
    private void HandleQuestEnded(vc_QuestTimer.QuestCompletionResult result)
    {
        if (_timer != null) _timer.QuestEnded -= HandleQuestEnded;
        if (Done) return;
        Started = false;
        CancelWork();
        Unsubscribe();
    }

    // ── Work lock ────────────────────────────────────────────────────────

    /// <summary>Start a slow fix. The best-fit skill calls this too, replacing any slow fix.</summary>
    protected void StartWork(IEnumerator routine)
    {
        CancelWork();
        Working = true;
        _work = StartCoroutine(routine);
    }

    protected void CancelWork()
    {
        if (_work != null) StopCoroutine(_work);
        _work = null;
        Working = false;
        ShowBar(false);
        if (_helper != null) Destroy(_helper.gameObject);
        _helper = null;
        OnWorkCancelled();
    }

    /// <summary>True (and shows the toast) when a slow fix is already running.</summary>
    protected bool BlockedByWork()
    {
        if (!Working) return false;
        Toast("Already working on it...", 1.5f);
        return true;
    }

    protected static void Toast(string msg, float seconds = 2f) => vc_QuestHUD.Instance?.ShowFeedbackTimed(msg, seconds);

    // ── Finish ───────────────────────────────────────────────────────────

    protected void Finish(string msg)
    {
        if (Done) return;
        Done = true;
        Working = false;
        _work = null;
        ShowBar(false);
        Unsubscribe();
        SunodGame.Core.vc_Sfx.Play("step_done", 0.8f);
        vc_FloatingMessage.Instance?.Show(msg);
        StartCoroutine(CompleteAfter(0.6f));
    }

    private IEnumerator CompleteAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        Started = false;
        vc_QuestHUD.Instance?.CheckObjective(0);
        if (mainMarker != null) mainMarker.Hide();
        if (QuestRoom != null) QuestRoom.OnQuestComplete();
    }

    // ── Progress bar ─────────────────────────────────────────────────────

    protected void ShowBar(bool on)
    {
        if (on && _bar == null)
        {
            Sprite px = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);
            _bar = new GameObject("WorkBar").transform;
            _bar.SetParent(transform, true);
            var bg = _bar.gameObject.AddComponent<SpriteRenderer>();
            bg.sprite = px; bg.color = new Color32(46, 34, 40, 230);
            bg.sortingLayerName = sortingLayer; bg.sortingOrder = 30;
            _bar.localScale = new Vector3(1f, 0.16f, 1f);

            _barFill = new GameObject("Fill").transform;
            _barFill.SetParent(_bar, false);
            _barFill.localPosition = new Vector3(0.03f, 0f, 0f);
            var fg = _barFill.gameObject.AddComponent<SpriteRenderer>();
            fg.sprite = px; fg.color = new Color32(255, 204, 66, 255);
            fg.sortingLayerName = sortingLayer; fg.sortingOrder = 31;
            _barFill.localScale = new Vector3(0f, 0.6f, 1f);
        }
        if (_bar != null)
        {
            if (on) _bar.position = BarWorldPosition;
            _bar.gameObject.SetActive(on);
        }
        if (on) SetBar(0f);
    }

    protected void SetBar(float k)
    {
        if (_barFill == null) return;
        k = Mathf.Clamp01(k);
        _barFill.localScale = new Vector3(Mathf.Round(k * 30f) / 30f * 0.94f, 0.6f, 1f);
    }

    // ── Effects ──────────────────────────────────────────────────────────

    protected static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * 32f) / 32f, Mathf.Round(p.y * 32f) / 32f, p.z);

    /// <summary>Plays sprite frames once at a world position, drifting up by 'rise' units, then disappears.</summary>
    protected void Fx(Sprite[] frames, Vector3 worldPos, float fps = 10f, float rise = 0f, int order = 20)
    {
        if (frames == null || frames.Length == 0) return;
        StartCoroutine(FxRoutine(frames, worldPos, fps, rise, order));
    }

    private IEnumerator FxRoutine(Sprite[] frames, Vector3 pos, float fps, float rise, int order)
    {
        var go = new GameObject("Fx");
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(pos);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = order;
        float dur = frames.Length / fps, t = 0f;
        while (t < dur && go != null)
        {
            t += Time.deltaTime;
            sr.sprite = frames[Mathf.Min(frames.Length - 1, (int)(t * fps))];
            go.transform.position = Snap(pos + new Vector3(0f, rise * (t / dur), 0f));
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    /// <summary>1-px-step horizontal shake of a transform around its resting local position.</summary>
    protected IEnumerator Shake(Transform tr, Vector3 restLocal, float duration, int px)
    {
        if (tr == null) yield break;
        float t = 0f;
        int i = 0;
        while (t < duration && tr != null)
        {
            t += Time.deltaTime;
            tr.localPosition = restLocal + new Vector3((i++ % 2 == 0 ? 1 : -1) * px / 22f, 0f, 0f);
            yield return new WaitForSeconds(0.04f);
        }
        if (tr != null) tr.localPosition = restLocal;
    }

    // ── Walking helper (Call path) ───────────────────────────────────────

    protected SpriteRenderer SpawnHelper(Vector3 at, int order)
    {
        if (_helper != null) Destroy(_helper.gameObject);
        var go = new GameObject("Helper");
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(at);
        _helper = go.AddComponent<SpriteRenderer>();
        if (helperWalk != null && helperWalk.Length > 0) _helper.sprite = helperWalk[0];
        _helper.sortingLayerName = sortingLayer;
        _helper.sortingOrder = order;
        return _helper;
    }

    /// <summary>Walks the spawned helper in a straight line, animating the walk frames.</summary>
    protected IEnumerator HelperWalk(Vector3 from, Vector3 to, float seconds, Action<float> each = null)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            if (_helper != null)
            {
                _helper.transform.position = Snap(Vector3.Lerp(from, to, t / seconds));
                if (helperWalk != null && helperWalk.Length > 0)
                    _helper.sprite = helperWalk[(int)(t * 8f) % helperWalk.Length];
            }
            each?.Invoke(t);
            yield return null;
        }
        if (_helper != null && helperWalk != null && helperWalk.Length > 0) _helper.sprite = helperWalk[0];
    }

    // ── Skill subscription ───────────────────────────────────────────────

    private void Subscribe()
    {
        if (vc_SkillManager.Instance == null) return;
        vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        vc_SkillManager.Instance.SkillUsed += HandleSkillUsed;
    }

    protected void Unsubscribe()
    {
        if (vc_SkillManager.Instance != null)
            vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        if (_timer != null) _timer.QuestEnded -= HandleQuestEnded;
    }
}
