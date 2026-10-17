using System.Collections;
using UnityEngine;

/// <summary>
/// R quest (R5). Grind Mang Erning's metal part to fit. Any APPROPRIATE skill works - how well it
/// fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   push     R  best fit     feed the part steadily into the wheel; it fits the slot      ~2s
///   repair   R  workaround   fix the dull grinding wheel first, then grind                5s
///   build    R  workaround   build a guide jig, then grind it straight                    4s
///   inspect  C  off-letter   grind a little, measure, repeat, slowly                      8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (push) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_WorkshopGrinderQuest : vc_FitQuestBase
{
    [Header("Bench")]
    [SerializeField] private SpriteRenderer grinder;
    [Tooltip("Sharp wheel spin frames.")]
    [SerializeField] private Sprite[] grinderSpin;
    [SerializeField] private Sprite grinderDull;
    [SerializeField] private SpriteRenderer part;
    [SerializeField] private Sprite partRough;
    [SerializeField] private Sprite partSmooth;
    [SerializeField] private SpriteRenderer slot;
    [SerializeField] private Sprite slotEmpty;
    [SerializeField] private Sprite slotFull;
    [SerializeField] private Transform goggles;

    [Header("Effects")]
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] shineFrames;
    [SerializeField] private Sprite jig;
    [SerializeField] private Sprite ruler;
    [SerializeField] private Sprite crossMark;
    [SerializeField] private Sprite checkMark;

    [Header("Timing (seconds)")]
    [SerializeField] private float pushTime = 2f;
    [SerializeField] private float repairTime = 5f;
    [SerializeField] private float buildTime = 4f;
    [SerializeField] private float inspectTime = 8f;

    private Vector3 _partRest, _gogglesRest;
    private bool _sharp;
    private float _idleClock;
    private GameObject _jig;

    protected override Vector3 BarWorldPosition => (grinder != null ? grinder.transform.position : transform.position) + new Vector3(-0.5f, 1.9f, 0f);

    private void Awake()
    {
        if (part != null) _partRest = part.transform.position;
        if (goggles != null) _gogglesRest = goggles.localPosition;
    }

    protected override void OnBegin()
    {
        if (_jig != null) Destroy(_jig);
        _sharp = false;
        if (grinder != null) grinder.sprite = grinderDull;
        if (part != null) { part.gameObject.SetActive(true); part.transform.position = _partRest; part.sprite = partRough; }
        if (slot != null) slot.sprite = slotEmpty;
        _idleClock = 1.5f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Workshop Job",
            "Mang Erning needs a metal part ground to fit.",
            new[] { "Grind the part to size" });
    }

    // ── Idle life: goggles sway, the rough part doesn't fit ──────────────

    private void Update()
    {
        if (!Started || Done || Working) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(2.2f, 3.2f);
        if (goggles != null) StartCoroutine(Shake(goggles, _gogglesRest, 0.3f, 1));
        if (crossMark != null && slot != null) Fx(new[] { crossMark }, SlotMouth() + new Vector3(0f, 0.5f, 0f), 1.4f, 0.15f, 24);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("push")) { StartWork(PushRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("repair")) { StartWork(RepairRoutine()); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine()); return; }
        if (d.HasTag("inspect")) { StartWork(InspectRoutine()); return; }
        Toast("That won't shape metal.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (part != null && part.sprite != partSmooth) part.transform.position = _partRest;
        if (grinder != null) grinder.sprite = _sharp ? grinderSpin[0] : grinderDull;
    }

    private IEnumerator PushRoutine()
    {
        Toast("You feed the part steadily into the wheel...", pushTime);
        ShowBar(true);
        yield return Grind(pushTime - 0.6f, 0f, pushTime, true);
        yield return Fit(pushTime - 0.6f, pushTime);
        Finish("Smooth and square. It fits perfectly!");
    }

    private IEnumerator RepairRoutine()
    {
        Toast("You fix the dull grinding wheel first...", repairTime);
        ShowBar(true);
        Vector3 w = Wheel();
        float t = 0f;
        for (int i = 0; i < 5; i++)
        {
            Fx(hammerFrames, w + new Vector3(0.15f, 0.35f, 0f), 8f);
            SunodGame.Core.vc_Sfx.Play("hammer", 0.5f, 0.1f);
            Fx(sparkFrames, w, 14f, 0.1f);
            yield return BarStep(0.5f, t / repairTime, (t + 0.5f) / repairTime);
            t += 0.5f;
        }
        _sharp = true;
        if (grinder != null && grinderSpin != null && grinderSpin.Length > 0) grinder.sprite = grinderSpin[0];
        Fx(shineFrames, w + new Vector3(0f, 0.3f, 0f), 12f, 0.15f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        yield return Grind(repairTime - t - 0.6f, t, repairTime, true);
        yield return Fit(repairTime - 0.6f, repairTime);
        Finish("Sharp wheel, clean cut. The part fits!");
    }

    private IEnumerator BuildRoutine()
    {
        Toast("You build a guide jig first...", buildTime);
        ShowBar(true);
        float t = 0f;
        if (jig != null && grinder != null)
        {
            _jig = new GameObject("GuideJig");
            _jig.transform.SetParent(transform, true);
            _jig.transform.position = Snap(Wheel() + new Vector3(-0.75f, -0.25f, 0f));
            var sr = _jig.AddComponent<SpriteRenderer>();
            sr.sprite = jig; sr.sortingLayerName = sortingLayer; sr.sortingOrder = grinder.sortingOrder + 1;
        }
        for (int i = 0; i < 3; i++)
        {
            SunodGame.Core.vc_Sfx.Play("clack", 0.6f, 0.1f);
            if (_jig != null) Fx(dustFrames, _jig.transform.position, 10f, 0.15f);
            yield return BarStep(0.55f, t / buildTime, (t + 0.55f) / buildTime);
            t += 0.55f;
        }
        yield return Grind(buildTime - t - 0.6f, t, buildTime, true);
        yield return Fit(buildTime - 0.6f, buildTime);
        Finish("The jig kept it straight. Perfect fit!");
    }

    private IEnumerator InspectRoutine()
    {
        Toast("Grind a little, measure, repeat...", inspectTime);
        ShowBar(true);
        float t = 0f, step = (inspectTime - 0.6f) / 3f;
        for (int pass = 0; pass < 3; pass++)
        {
            yield return Grind(step * 0.55f, t, inspectTime, pass == 2);
            t += step * 0.55f;
            // back to the vise to measure
            yield return Slide(part.transform.position, _partRest, 0.2f);
            if (ruler != null) Fx(new[] { ruler }, _partRest + new Vector3(0f, 0.35f, 0f), 2.2f, 0f, 24);
            SunodGame.Core.vc_Sfx.Play("timer_tick", 0.5f);
            Sprite verdict = pass < 2 ? crossMark : checkMark;
            if (verdict != null) StartCoroutine(Later(0.3f, () => Fx(new[] { verdict }, _partRest + new Vector3(0.35f, 0.6f, 0f), 1.6f, 0.15f, 25)));
            yield return BarStep(step * 0.45f - 0.2f, t / inspectTime, (t + step * 0.45f) / inspectTime);
            t += step * 0.45f;
        }
        yield return Fit(t, inspectTime);
        Finish("Measured three times. It fits just right.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Spins the wheel with the part pressed against it; sparks spray.</summary>
    private IEnumerator Grind(float seconds, float t0, float total, bool finish)
    {
        if (part != null) yield return Slide(part.transform.position, Wheel() + new Vector3(-0.42f, -0.05f, 0f), 0.15f);
        float t = 0f, f = 0f, spark = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime; f += Time.deltaTime; spark += Time.deltaTime;
            if (f >= 0.05f && grinder != null && grinderSpin != null && grinderSpin.Length > 1)
            {
                f = 0f;
                grinder.sprite = _sharp ? (grinder.sprite == grinderSpin[0] ? grinderSpin[1] : grinderSpin[0])
                                        : (grinder.sprite == grinderDull ? grinderSpin[1] : grinderDull);
            }
            if (spark >= 0.1f)
            {
                spark = 0f;
                Fx(sparkFrames, Wheel() + new Vector3(-0.15f, -0.2f, 0f), 14f, -0.2f, 24);
            }
            if (Mathf.Repeat(t, 0.45f) < Time.deltaTime) SunodGame.Core.vc_Sfx.Play("grind", 0.45f, 0.1f);
            SetBar((t0 + t) / total);
            yield return null;
        }
        if (grinder != null) grinder.sprite = _sharp && grinderSpin != null ? grinderSpin[0] : grinderDull;
        if (finish && part != null) { part.sprite = partSmooth; Fx(shineFrames, part.transform.position + new Vector3(0f, 0.2f, 0f), 12f, 0.1f); }
    }

    private IEnumerator Fit(float t0, float total)
    {
        if (part == null || slot == null) { yield return BarStep(total - t0, t0 / total, 1f); yield break; }
        float dur = Mathf.Max(0.2f, total - t0 - 0.05f), t = 0f;
        Vector3 from = part.transform.position, to = SlotMouth();
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = t / dur;
            part.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.4f, 0f));
            SetBar((t0 + t) / total);
            yield return null;
        }
        part.gameObject.SetActive(false);
        slot.sprite = slotFull;
        SunodGame.Core.vc_Sfx.Play("clack", 0.7f);
        if (checkMark != null) Fx(new[] { checkMark }, to + new Vector3(0f, 0.6f, 0f), 1.5f, 0.2f, 25);
        Fx(shineFrames, to, 12f, 0.15f);
    }

    private IEnumerator Slide(Vector3 from, Vector3 to, float seconds)
    {
        float t = 0f;
        while (t < seconds && part != null)
        {
            t += Time.deltaTime;
            part.transform.position = Snap(Vector3.Lerp(from, to, t / seconds));
            yield return null;
        }
    }

    private Vector3 Wheel()
    {
        if (grinder == null) return transform.position;
        Bounds b = grinder.bounds;
        return new Vector3(b.min.x + b.size.x * 0.22f, b.min.y + b.size.y * 0.53f, 0f);
    }

    private Vector3 SlotMouth()
    {
        Bounds b = slot.bounds;
        return new Vector3(b.center.x, b.min.y + b.size.y * 0.53f, 0f);
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
