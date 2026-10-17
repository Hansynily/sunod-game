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
            "The student records are all mixed up. Help Mrs. Ong sort them.",
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
        if (d.HasTag("collect")) { StartWork(CollectRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
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

    private void InspectPress()
    {
        int i = NextUnfiled();
        if (i < 0) return;
        _inspected++;
        Vector3 top = PileTop(i);
        if (magnifier != null) Fx(new[] { magnifier }, top, 2.5f, 0.15f, 24);
        if (checkMark != null) StartCoroutine(Later(0.3f, () => Fx(new[] { checkMark }, top + new Vector3(0.3f, 0.2f, 0f), 1.6f, 0.2f, 25)));
        SunodGame.Core.vc_Sfx.Play("scan", 0.4f, 0.1f);
        StartCoroutine(FileOne(i, 0.35f, true));
        if (_inspected < inspectPresses && NextUnfiled() >= 0)
        {
            Toast($"Stack checked and filed ({_inspected}/{inspectPresses})", 2f);
            return;
        }
        Finish("Every record checked and filed. No errors!");
    }

    private IEnumerator CollectRoutine()
    {
        Toast("You gather the loose papers into folders...", collectTime);
        ShowBar(true);
        float gather = collectTime - 1.6f, per = gather / Mathf.Max(1, piles.Length), t = 0f;
        for (int i = 0; i < piles.Length; i++)
        {
            if (_filed[i]) { t += per; continue; }
            Fx(dustFrames, PileTop(i) + new Vector3(0f, -0.3f, 0f), 10f, 0.15f);
            SunodGame.Core.vc_Sfx.Play("rustle", 0.5f, 0.1f);
            yield return BarStep(per * 0.6f, t / collectTime, (t + per * 0.6f) / collectTime);
            if (folders != null && i < folders.Length) piles[i].sprite = folders[i];
            SunodGame.Core.vc_Sfx.Play("page_next", 0.5f, 0.1f);
            yield return BarStep(per * 0.4f, (t + per * 0.6f) / collectTime, (t + per) / collectTime);
            t += per;
        }
        yield return FileAll(t, collectTime);
        Finish("Papers gathered and filed. All tidy!");
    }

    private IEnumerator PlanRoutine()
    {
        Toast("You design a new filing system first...", planTime);
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
        float work = planTime - 2.4f, t = 0f;
        while (t < work)
        {
            Fx(scribbleFrames, at + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 0.6f), 0f), 6f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("scribble", 0.35f, 0.15f, 0.3f);
            yield return BarStep(0.4f, t / planTime, (t + 0.4f) / planTime);
            t += 0.4f;
        }
        if (checkMark != null) Fx(new[] { checkMark }, at + new Vector3(0f, 0.9f, 0f), 1.5f, 0.2f, 24);
        yield return FileAll(t, planTime);
        if (_sheet != null) Destroy(_sheet);
        Finish("Your new system works. Everything's filed!");
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
    private IEnumerator FileOne(int i, float seconds, bool openWall)
    {
        if (_filed[i]) yield break;
        _filed[i] = true;
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
