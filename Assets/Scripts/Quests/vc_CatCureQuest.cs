using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// I quest (I4). Develop a treatment for the sick cat. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   unlock   I  best fit     work out the right remedy and mix it                        ~2s
///   survey   I  workaround   study the symptoms, then pick the right bottle              4s
///   plan     I  workaround   test the treatments one by one                              5s
///   inspect  C  off-letter   read every medicine label carefully                         8s
///   heal     S  off-letter   nurse the cat slowly back to health                         8s
///   anything else: a hint, no effect
///
/// Replaces the old craft/build/barrier/paint "mold the medicine" paths, which made no sense here.
/// While a slow fix is running the best fit (unlock) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_CatCureQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer cat;
    [SerializeField] private Transform table;
    [Tooltip("Bottles on the table: red, green, blue, yellow. Green is the right remedy.")]
    [SerializeField] private SpriteRenderer[] bottles;
    [SerializeField] private Transform mortar;
    [SerializeField] private Transform pestle;
    [SerializeField] private int rightBottle = 1;
    [SerializeField] private Color sickColor = new Color(0.72f, 0.9f, 0.72f, 1f);

    [Header("Art")]
    [SerializeField] private Sprite bubbleSick;
    [SerializeField] private Sprite bubbleSmile;
    [SerializeField] private Sprite thermometer;
    [SerializeField] private Sprite crossMark;
    [SerializeField] private Sprite magnifier;
    [SerializeField] private Sprite[] labels;
    [SerializeField] private Sprite[] heartFrames;
    [SerializeField] private Sprite[] sweatFrames;

    [Header("Effects")]
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float solveTime = 2f;
    [SerializeField] private float surveyTime = 4f;
    [SerializeField] private float planTime = 5f;
    [SerializeField] private float inspectTime = 8f;
    [SerializeField] private float healTime = 8f;

    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Dr. Santos",
        presses = 3,
        bestFitTag = "unlock",
        bestFitSeconds = 2f,
        prepTag = "heal",
        prepUnlocksTag = "survey",
        prepLine = "You stroke the cat gently until it stops shivering...",
        preferredTag = "plan",
        preferenceLine = "Careful, one by one. That's how it's done. Thank you!",
        fastLines = new[]
        {
            ("survey", "You read her symptoms that fast? Impressive."),
            ("plan", "Careful and quick. She's going to be okay."),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private bool _calm;
    private float _elapsed, _total = 1f;
    private float _idleClock;
    private Vector3 _catRest;
    private bool _cured;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    protected override Vector3 BarWorldPosition => (cat != null ? cat.transform.position : transform.position) + new Vector3(-0.5f, 1.6f, 0f);

    private void Awake()
    {
        if (cat != null) _catRest = cat.transform.position;
    }

    protected override void OnBegin()
    {
        CleanSpawned();
        _cured = false;
        _calm = false;
        if (cat != null) { cat.color = sickColor; cat.transform.position = _catRest; }
        if (pestle != null) pestle.gameObject.SetActive(true);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Cat Medicine",
            "Dr. Santos has a sick, shivering cat at the clinic. There are four medicines on the table, but only one will cure it. Figure out which one - by reasoning it out, studying its symptoms, or testing them one by one (the doctor likes careful testing).",
            new[]
            {
                "Find out what is making the cat sick",
                "Work out which bottle is the right medicine",
                "Give the cat its treatment",
            });
    }

    // ── Idle life: the sick cat shivers and sniffles ─────────────────────

    private void Update()
    {
        if (!Started || Done || cat == null || _cured || _calm) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = 2.4f;
        Fx(new[] { bubbleSick }, cat.transform.position + new Vector3(0f, 1.1f, 0f), 1.4f, 0.15f, 26);
        Fx(sweatFrames, cat.transform.position + new Vector3(0.35f, 0.6f, 0f), 4f, -0.3f, 26);
        StartCoroutine(Shiver());
    }

    private IEnumerator Shiver()
    {
        for (int i = 0; i < 4; i++)
        {
            if (cat == null || _cured) yield break;
            cat.transform.position = _catRest + new Vector3((i % 2 == 0 ? 1 : -1) / 22f, 0f, 0f);
            yield return new WaitForSeconds(0.06f);
        }
        if (cat != null && !_cured) cat.transform.position = _catRest;
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("unlock")) { StartWork(SolveRoutine()); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("survey")) { StartWork(SurveyRoutine(PathTime(surveyTime), CurrentInstant)); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine(PathTime(planTime))); return; }
        if (d.HasTag("inspect")) { StartWork(InspectRoutine(PathTime(inspectTime))); return; }
        if (d.HasTag("heal")) { StartWork(HealRoutine(PathTime(healTime))); return; }

        Toast("That won't cure the cat. Think it through: what's wrong with it, and which medicine fixes it?", 2.8f);
    }

    protected override void OnWorkCancelled()
    {
        CleanSpawned();
        if (cat != null && !_cured) cat.transform.position = _catRest;
    }

    // Prep (Heal): stroke the cat until it stops shivering; Survey then reads the symptoms at once.
    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        if (cat == null) { yield return new WaitForSeconds(1f); _calm = true; yield break; }
        Vector3 c = cat.transform.position;
        Color from = cat.color;
        Color calmColor = Color.Lerp(sickColor, Color.white, 0.7f);
        float t = 0f, heart = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            heart -= Time.deltaTime;
            cat.color = Color.Lerp(from, calmColor, t);
            if (heart <= 0f)
            {
                heart = 0.3f;
                Fx(heartFrames, c + new Vector3(Random.Range(-0.4f, 0.4f), 0.7f, 0f), 3f, 0.6f, 26);
                SunodGame.Core.vc_Sfx.Play("text_blip", 0.3f, 0.2f);
            }
            yield return null;
        }
        _calm = true;
        cat.color = calmColor;
        cat.transform.position = _catRest;
        SunodGame.Core.vc_Sfx.Play("meow", 0.4f);
    }

    private void CleanSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    private void Timeline(float total)
    {
        _elapsed = 0f; _total = Mathf.Max(0.1f, total);
        ShowBar(true);
    }

    private void Tick(float dt)
    {
        _elapsed += dt;
        SetBar(_elapsed / _total);
    }

    private IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.deltaTime; Tick(Time.deltaTime); yield return null; }
    }

    // ── Solve: work it out, grind it, deliver it ─────────────────────────

    private IEnumerator SolveRoutine()
    {
        Toast("Working out the right remedy...", Mathf.Max(1.5f, solveTime));
        Timeline(solveTime);
        Fx(bulbFrames, table.position + new Vector3(0f, 1.9f, 0f), 2f, 0.3f, 26);
        SunodGame.Core.vc_Sfx.Play("idea", 0.7f);
        yield return Wait(solveTime * 0.2f);

        yield return Grind(3, solveTime * 0.45f);
        Highlight(rightBottle);
        yield return Wait(solveTime * 0.1f);
        yield return Deliver(rightBottle, solveTime * 0.25f, "The right remedy! The cat is feeling better!");
    }

    // ── Survey: study the symptoms, then pick the bottle ─────────────────

    private IEnumerator SurveyRoutine(float seconds, bool oneGo)
    {
        Toast(oneGo ? "You read her symptoms at a glance..." : "Studying the cat's symptoms...", Mathf.Max(1.5f, seconds));
        Timeline(seconds);
        Vector3 c = cat.transform.position;
        Fx(new[] { thermometer }, c + new Vector3(-0.45f, 0.7f, 0f), 1.8f, 0.3f, 26);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f);
        yield return Wait(seconds * 0.2f);
        Fx(ringFrames, c + new Vector3(0f, 0.3f, 0f), 6f);
        Fx(new[] { bubbleSick }, c + new Vector3(0f, 1.1f, 0f), 1.8f, 0.15f, 26);
        yield return Wait(seconds * 0.2f);

        GameObject lens = Spawn("Lens", magnifier, 27);
        float per = seconds * 0.45f / bottles.Length;
        for (int i = 0; i < bottles.Length; i++)
        {
            lens.transform.position = Snap(bottles[i].transform.position + new Vector3(0.2f, 0.7f, 0f));
            if (i == rightBottle) { Highlight(i); break; }
            Fx(new[] { crossMark }, bottles[i].transform.position + new Vector3(0f, 0.9f, 0f), 2f, 0.2f, 26);
            SunodGame.Core.vc_Sfx.Play("buzz", 0.3f);
            yield return Wait(per);
        }
        Destroy(lens);
        yield return Wait(per * 0.5f);
        yield return Deliver(rightBottle, seconds * 0.12f, oneGo ? "One look at her symptoms, and you picked the right bottle!" : "You read the symptoms and picked the right bottle!");
    }

    // ── Plan: test the treatments one by one ─────────────────────────────

    private IEnumerator PlanRoutine(float seconds)
    {
        Toast("Testing the treatments one by one...", seconds);
        Timeline(seconds);
        int n = bottles.Length;
        int[] order = { 0, 2, rightBottle };
        float per = seconds * 0.8f / order.Length;
        foreach (int i in order)
        {
            Vector3 from = bottles[i].transform.position + new Vector3(0f, 0.7f, 0f);
            Vector3 to = mortar.position + new Vector3(0f, 0.6f, 0f);
            GameObject b = Spawn("TestBottle", bottles[i].sprite, 27);
            float s = 0f;
            while (s < per * 0.5f)
            {
                s += Time.deltaTime; Tick(Time.deltaTime);
                float k = Mathf.Clamp01(s / (per * 0.5f));
                if (b != null) b.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
                yield return null;
            }
            Destroy(b);
            Fx(sparkFrames, to, 10f, 0.2f, 27);
            if (i == rightBottle)
            {
                Fx(checkFrames, to + new Vector3(0f, 0.5f, 0f), 3f, 0.3f, 27);
                SunodGame.Core.vc_Sfx.Play("step_done", 0.6f);
                Highlight(i);
            }
            else
            {
                Fx(new[] { crossMark }, to + new Vector3(0f, 0.5f, 0f), 2f, 0.2f, 27);
                SunodGame.Core.vc_Sfx.Play("buzz", 0.35f);
            }
            yield return Wait(per * 0.5f);
        }
        yield return Deliver(rightBottle, seconds * 0.2f, "Your tests found the right treatment!");
    }

    // ── Inspect: read every label ────────────────────────────────────────

    private IEnumerator InspectRoutine(float seconds)
    {
        Toast("Reading each medicine label...", seconds);
        Timeline(seconds);
        GameObject lens = Spawn("Lens", magnifier, 27);
        float per = seconds * 0.75f / bottles.Length;
        for (int i = 0; i < bottles.Length; i++)
        {
            Vector3 p = bottles[i].transform.position;
            lens.transform.position = Snap(p + new Vector3(0.2f, 0.7f, 0f));
            Fx(ringFrames, p + new Vector3(0f, 0.3f, 0f), 6f);
            if (labels != null && i < labels.Length) Fx(new[] { labels[i] }, p + new Vector3(0f, 1.0f, 0f), 1.4f, 0.25f, 26);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.45f, 0.1f);
            yield return Wait(per);
        }
        Destroy(lens);
        Highlight(rightBottle);
        yield return Wait(seconds * 0.05f);
        yield return Deliver(rightBottle, seconds * 0.2f, "You read every label and found the right medicine!");
    }

    // ── Heal: nurse the cat slowly back to health ────────────────────────

    private IEnumerator HealRoutine(float seconds)
    {
        Toast("You nurse the cat gently...", seconds);
        Timeline(seconds);
        Vector3 c = cat.transform.position;
        float t = 0f, heart = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime; Tick(Time.deltaTime);
            heart -= Time.deltaTime;
            cat.color = Color.Lerp(sickColor, Color.white, t / seconds);
            if (heart <= 0f)
            {
                heart = 0.55f;
                Fx(heartFrames, c + new Vector3(Random.Range(-0.4f, 0.4f), 0.7f, 0f), 3f, 0.6f, 26);
                SunodGame.Core.vc_Sfx.Play("text_blip", 0.3f, 0.2f);
            }
            yield return null;
        }
        yield return Cure("You nursed the cat back to health!");
    }

    // ── Shared steps ─────────────────────────────────────────────────────

    private IEnumerator Grind(int times, float seconds)
    {
        float per = seconds / times;
        for (int i = 0; i < times; i++)
        {
            if (pestle != null) pestle.localPosition += new Vector3(0f, 3f / 22f, 0f);
            yield return Wait(per * 0.5f);
            if (pestle != null) pestle.localPosition -= new Vector3(0f, 3f / 22f, 0f);
            SunodGame.Core.vc_Sfx.Play("clack", 0.4f, 0.12f);
            yield return Wait(per * 0.5f);
        }
    }

    private void Highlight(int i)
    {
        if (bottles == null || i >= bottles.Length) return;
        Fx(sparkFrames, bottles[i].transform.position + new Vector3(0f, 0.5f, 0f), 10f, 0.3f, 27);
        Fx(checkFrames, bottles[i].transform.position + new Vector3(0f, 1.0f, 0f), 3f, 0.3f, 27);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.5f);
    }

    private IEnumerator Deliver(int bottle, float seconds, string msg)
    {
        Vector3 from = bottles[bottle].transform.position + new Vector3(0f, 0.7f, 0f);
        Vector3 to = cat.transform.position + new Vector3(0f, 0.4f, 0f);
        GameObject v = Spawn("Vial", bottles[bottle].sprite, 27);
        float t = 0f;
        seconds = Mathf.Max(0.3f, seconds);
        while (t < seconds)
        {
            t += Time.deltaTime; Tick(Time.deltaTime);
            float k = Mathf.Clamp01(t / seconds);
            if (v != null) v.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 1.0f, 0f));
            yield return null;
        }
        Destroy(v);
        yield return Cure(msg);
    }

    private IEnumerator Cure(string msg)
    {
        _cured = true;
        float t = 0f;
        Color from = cat.color;
        while (t < 0.5f)
        {
            t += Time.deltaTime;
            cat.color = Color.Lerp(from, Color.white, t / 0.5f);
            yield return null;
        }
        cat.color = Color.white;
        Vector3 c = cat.transform.position;
        Fx(heartFrames, c + new Vector3(-0.3f, 0.7f, 0f), 3f, 0.7f, 26);
        Fx(heartFrames, c + new Vector3(0.3f, 0.8f, 0f), 3f, 0.8f, 26);
        Fx(new[] { bubbleSmile }, c + new Vector3(0f, 1.1f, 0f), 1.4f, 0.15f, 26);
        SunodGame.Core.vc_Sfx.Play("meow", 0.7f);
        for (int i = 0; i < 2; i++)
        {
            cat.transform.position = c + new Vector3(0f, 6f / 22f, 0f);
            yield return new WaitForSeconds(0.1f);
            cat.transform.position = c;
            yield return new WaitForSeconds(0.1f);
        }
        Finish(msg);
    }

    private GameObject Spawn(string name, Sprite s, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        _spawned.Add(go);
        return go;
    }
}
