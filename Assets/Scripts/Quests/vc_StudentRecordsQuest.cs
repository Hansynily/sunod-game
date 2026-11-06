using System.Collections;
using UnityEngine;

/// <summary>
/// C quest (C4). Sort Mrs. Ong's mixed-up student records. Any APPROPRIATE skill works - how
/// well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   direct   C  best fit     files everything A-Z; folders slide into the cabinet            ~2s
///   inspect  C  workaround   checks each record for errors, one stack per press              x3
///   collect  C  workaround   gathers the loose papers into folders first, then files         4s
///   plan     I  off-letter   designs a new filing system on paper, then files                8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (direct) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_StudentRecordsQuest : vc_FitQuestBase
{
    [Header("Office")]
    [SerializeField] private SpriteRenderer fileWall;
    [SerializeField] private Sprite wallClosed;
    [SerializeField] private Sprite wallOpen;
    [SerializeField] private SpriteRenderer[] piles;
    [Tooltip("Folder per pile (A, M, Z).")]
    [SerializeField] private Sprite[] folders;
    [SerializeField] private SpriteRenderer lamp;
    [SerializeField] private Sprite lampOn;
    [SerializeField] private Sprite lampOff;
    [SerializeField] private SpriteRenderer lampGlow;

    [Header("Effects")]
    [SerializeField] private Sprite magnifier;
    [SerializeField] private Sprite checkMark;
    [SerializeField] private Sprite planSheet;
    [SerializeField] private Sprite[] scribbleFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float directTime = 2f;
    [SerializeField] private int inspectPresses = 3;
    [SerializeField] private float collectTime = 4f;
    [SerializeField] private float planTime = 8f;

    // Skill system v2 (QUEST_REDESIGN.md): Plan (prep) pins a filing plan to the cabinet, then
    // Collect sweeps every paper into its folder in one go. Preference: Inspect (checking each stack).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Mrs. Ong",
        presses = 5,
        bestFitTag = "direct",
        bestFitSeconds = 2f,
        prepTag = "plan",
        prepUnlocksTag = "collect",
        prepLine = "You pin a filing plan to the cabinet...",
        preferredTag = "inspect",
        preferenceLine = "You checked every record for mistakes? Thank you, that was careful work!",
        fastLines = new[]
        {
            ("inspect", "Every mistake caught already? Sharp eyes!"),
            ("collect", "All gathered and filed? Thank you!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private GameObject _planPin;
    private Sprite[] _pileSprites;
    private Vector3[] _pileRest;
    private bool[] _filed;
    private int _inspected;
    private float _flickerClock, _twitchClock;
    private GameObject _sheet;

    protected override Vector3 BarWorldPosition => (fileWall != null ? fileWall.transform.position : transform.position) + new Vector3(-0.5f, 2.2f, 0f);

    private void Awake()
    {
        _pileSprites = new Sprite[piles.Length];
        _pileRest = new Vector3[piles.Length];
        for (int i = 0; i < piles.Length; i++) { _pileSprites[i] = piles[i].sprite; _pileRest[i] = piles[i].transform.position; }
        _filed = new bool[piles.Length];
    }

    protected override void OnBegin()
    {
        if (_sheet != null) Destroy(_sheet);
        if (_planPin != null) Destroy(_planPin);
        for (int i = 0; i < piles.Length; i++)
        {
            piles[i].gameObject.SetActive(true);
            piles[i].sprite = _pileSprites[i];
            piles[i].transform.position = _pileRest[i];
            _filed[i] = false;
        }
        _inspected = 0;
        if (fileWall != null) fileWall.sprite = wallClosed;
        SetLamp(true);
        _flickerClock = 2f; _twitchClock = 1f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Student Records",
            "Mrs. Ong's student records are all mixed up, and some have mistakes in them. Get them sorted. She'd love it if someone checked the records over.",
            new[] { "File the records" });
    }

    // ── Idle life: lamp flicker, papers rustle ───────────────────────────

    private void Update()
    {
        if (!Started || Done) return;
        _flickerClock -= Time.deltaTime;
        if (_flickerClock <= 0f) { _flickerClock = Random.Range(2.5f, 4f); StartCoroutine(Flicker()); }
        _twitchClock -= Time.deltaTime;
        if (_twitchClock <= 0f && !Working)
        {
            _twitchClock = Random.Range(1.2f, 2.2f);
            int i = Random.Range(0, piles.Length);
            if (!_filed[i] && piles[i].gameObject.activeSelf)
            {
                StartCoroutine(Shake(piles[i].transform, piles[i].transform.localPosition, 0.2f, 1));
                SunodGame.Core.vc_Sfx.Play("rustle", 0.2f, 0.15f, 0.5f);
            }
        }
    }

    private IEnumerator Flicker()
    {
        SetLamp(false);
        yield return new WaitForSeconds(0.08f);
        SetLamp(true);
        yield return new WaitForSeconds(0.1f);
        SetLamp(false);
        yield return new WaitForSeconds(0.06f);
        SetLamp(true);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("inspect")) { InspectPress(); return; }
        if (d.HasTag("collect")) { StartWork(CollectRoutine(PathTime(collectTime), CurrentInstant)); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine(PathTime(planTime))); return; }
        Toast("Records need careful, orderly work.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_sheet != null) Destroy(_sheet);
    }

    private IEnumerator DirectRoutine()
    {
        Toast("You file everything A to Z...", directTime);
        ShowBar(true);
        yield return FileAll(0f, directTime);
        Finish("Filed A to Z. Neat and orderly!");
    }

    // 3 presses at Lv1, 2 at Lv2, 1 at Lv3: each press checks and files its share of the stacks.
    private void InspectPress()
    {
        _inspected++;
        int need = Mathf.Max(1, PathPresses(inspectPresses));
        if (_inspected >= need)
        {
            StartWork(InspectFinishRoutine(need == 1));
            return;
        }

        int left = 0;
        for (int n = 0; n < piles.Length; n++) if (!_filed[n]) left++;
        int share = Mathf.Max(1, Mathf.CeilToInt(left / (float)(need - _inspected + 1)));
        for (int k = 0; k < share; k++)
        {
            int pi = NextUnfiled();
            if (pi < 0) break;
            CheckPile(pi, k * 0.3f);
        }
        Toast($"Stack checked and filed ({_inspected}/{need})", 2f);
    }

    private IEnumerator InspectFinishRoutine(bool oneGo)
    {
        Toast(oneGo ? "You check every stack in one sweep!" : "You check the last stacks...", 2f);
        ShowBar(true);
        int k = 0;
        for (int n = 0; n < piles.Length; n++)
            if (!_filed[n]) { CheckPile(n, k * 0.3f); k++; }
        yield return BarStep(k * 0.3f + 0.7f, 0f, 1f);
        Finish(oneGo ? "Every record checked and filed in one sweep. No errors!" : "Every record checked and filed. No errors!");
    }

    /// <summary>Magnifier and check mark over a pile, then it files itself.</summary>
    private void CheckPile(int pile, float delay)
    {
        Vector3 top = PileTop(pile);
        if (delay <= 0f) PileCheckFx(top);
        else StartCoroutine(Later(delay, () => PileCheckFx(top)));
        StartCoroutine(FileOne(pile, 0.35f, true, delay));
    }

    private void PileCheckFx(Vector3 top)
    {
        if (magnifier != null) Fx(new[] { magnifier }, top, 2.5f, 0.15f, 24);
        if (checkMark != null) StartCoroutine(Later(0.3f, () => Fx(new[] { checkMark }, top + new Vector3(0.3f, 0.2f, 0f), 1.6f, 0.2f, 25)));
        SunodGame.Core.vc_Sfx.Play("scan", 0.4f, 0.1f);
    }

    private IEnumerator CollectRoutine(float total, bool oneGo)
    {
        Toast(oneGo ? "Following the plan, every paper lands in its folder!" : "You gather the loose papers into folders...", total);
        ShowBar(true);
        float gather = total * 0.6f, per = gather / Mathf.Max(1, piles.Length), t = 0f;
        for (int i = 0; i < piles.Length; i++)
        {
            if (_filed[i]) { t += per; continue; }
            Fx(dustFrames, PileTop(i) + new Vector3(0f, -0.3f, 0f), 10f, 0.15f);
            SunodGame.Core.vc_Sfx.Play("rustle", 0.5f, 0.1f);
            yield return BarStep(per * 0.6f, t / total, (t + per * 0.6f) / total);
            if (folders != null && i < folders.Length) piles[i].sprite = folders[i];
            SunodGame.Core.vc_Sfx.Play("page_next", 0.5f, 0.1f);
            yield return BarStep(per * 0.4f, (t + per * 0.6f) / total, (t + per) / total);
            t += per;
        }
        yield return FileAll(t, total);
        if (_planPin != null) Destroy(_planPin);
        Finish(oneGo ? "Every paper gathered and filed in one go. All tidy!" : "Papers gathered and filed. All tidy!");
    }

    private IEnumerator PlanRoutine(float total)
    {
        Toast("You design a new filing system first...", total);
        ShowBar(true);
        Vector3 at = (piles.Length > 0 ? _pileRest[0] : transform.position) + new Vector3(-0.2f, -0.9f, 0f);
        if (planSheet != null)
        {
            _sheet = new GameObject("FilingPlan");
            _sheet.transform.SetParent(transform, true);
            _sheet.transform.position = Snap(at);
            var sr = _sheet.AddComponent<SpriteRenderer>();
            sr.sprite = planSheet; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 14;
        }
        float work = total * 0.7f, t = 0f, step = Mathf.Clamp(work / 6f, 0.1f, 0.4f);
        while (t < work)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 0.6f), 0f), 6f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("scribble", 0.35f, 0.15f, 0.3f);
            yield return BarStep(step, t / total, (t + step) / total);
            t += step;
        }
        if (checkMark != null) Fx(new[] { checkMark }, at + new Vector3(0f, 0.9f, 0f), 1.5f, 0.2f, 24);
        yield return FileAll(t, total);
        if (_sheet != null) Destroy(_sheet);
        Finish("Your new system works. Everything's filed!");
    }

    // Prep (Plan): a filing plan is pinned to the cabinet; it stays up until Collect has filed everything.
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        SunodGame.Core.vc_Sfx.Play("scribble", 0.5f, 0.1f);
        Vector3 at = (fileWall != null ? fileWall.bounds.center : transform.position) + new Vector3(0f, 1.0f, 0f);
        if (_planPin != null) Destroy(_planPin);
        _planPin = new GameObject("FilingPlanPin");
        _planPin.transform.SetParent(transform, true);
        _planPin.transform.position = Snap(at);
        var pinSr = _planPin.AddComponent<SpriteRenderer>();
        pinSr.sortingLayerName = sortingLayer; pinSr.sortingOrder = 14;
        if (planSheet != null) pinSr.sprite = planSheet;
        else
        {
            pinSr.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            pinSr.color = new Color(1f, 0.96f, 0.8f);
            _planPin.transform.localScale = new Vector3(0.8f, 1.0f, 1f);
        }
        for (int k = 0; k < 3; k++)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(-0.2f, 0.4f), 0f), 6f, 0f, 22);
            yield return new WaitForSeconds(0.25f);
        }
        if (checkMark != null) Fx(new[] { checkMark }, at + new Vector3(0.3f, 0.5f, 0f), 1.6f, 0.2f, 25);
        yield return new WaitForSeconds(0.25f);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private IEnumerator FileAll(float t0, float total)
    {
        if (fileWall != null) fileWall.sprite = wallOpen;
        SunodGame.Core.vc_Sfx.Play("clack", 0.5f);
        int left = 0;
        for (int i = 0; i < piles.Length; i++) if (!_filed[i]) left++;
        float per = (total - t0 - 0.2f) / Mathf.Max(1, left), t = t0;
        for (int i = 0; i < piles.Length; i++)
        {
            if (_filed[i]) continue;
            StartCoroutine(FileOne(i, per * 0.8f, false));
            yield return BarStep(per, t / total, (t + per) / total);
            t += per;
        }
        if (fileWall != null) fileWall.sprite = wallClosed;
        SunodGame.Core.vc_Sfx.Play("clack", 0.6f);
        if (fileWall != null) Fx(sparkFrames, fileWall.bounds.center + new Vector3(0.6f, 0.6f, 0f), 12f, 0.2f);
        yield return BarStep(0.2f, t / total, 1f);
    }

    /// <summary>Pile turns into its folder and flies into the cabinet.</summary>
    private IEnumerator FileOne(int i, float seconds, bool openWall, float delay = 0f)
    {
        if (_filed[i]) yield break;
        _filed[i] = true;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (openWall && fileWall != null) fileWall.sprite = wallOpen;
        if (folders != null && i < folders.Length) piles[i].sprite = folders[i];
        Vector3 from = piles[i].transform.position;
        Vector3 to = fileWall != null
            ? fileWall.bounds.center + new Vector3((i - 1) * 0.7f, 0.2f, 0f)
            : from + Vector3.up;
        SunodGame.Core.vc_Sfx.Play("page_next", 0.5f, 0.1f);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = t / seconds;
            piles[i].transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
            yield return null;
        }
        piles[i].gameObject.SetActive(false);
        SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.1f);
        if (openWall)
        {
            yield return new WaitForSeconds(0.2f);
            if (fileWall != null) fileWall.sprite = wallClosed;
        }
    }

    private int NextUnfiled()
    {
        for (int i = 0; i < piles.Length; i++) if (!_filed[i]) return i;
        return -1;
    }

    private Vector3 PileTop(int i)
    {
        Bounds b = piles[i].bounds;
        return new Vector3(b.center.x, b.max.y + 0.3f, 0f);
    }

    private void SetLamp(bool on)
    {
        if (lamp != null) lamp.sprite = on ? lampOn : lampOff;
        if (lampGlow != null) lampGlow.enabled = on;
    }

    private IEnumerator Later(float s, System.Action a)
    {
        yield return new WaitForSeconds(s);
        a();
    }

    private IEnumerator BarStep(float seconds, float k0, float k1)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetBar(Mathf.Lerp(k0, k1, t / seconds));
            yield return null;
        }
    }
}
