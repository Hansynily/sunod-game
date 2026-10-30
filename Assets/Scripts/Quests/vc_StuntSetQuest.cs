using System.Collections;
using UnityEngine;

/// <summary>
/// A quest (A7). Pull off the stunt for Direk Bong's student film. Any APPROPRIATE skill works -
/// how well it fits decides how fast it goes (and so the stars).
///
///   craft    A  best fit     choreograph and perform: run, roll, land; "Cut! Perfect!"     ~2s
///   barrier  A  workaround   shape a safe landing on the crash pad, then perform             4s
///   paint    A  workaround   storyboard it panel by panel, then perform                      5s
///   build    R  off-letter   build a bigger ramp, then jump                                  8s
///   push     R  off-letter   push the prop cart into a crash gag                             8s
///   plan     I  off-letter   work out the jump arc (dotted path), then perform               6s
///   teach    S  off-letter   coach the stunt double through practice moves, then perform     7s
///   command  E  off-letter   call the shots: places, camera, action! then perform            7s
///   anything else: a hint, no effect
/// </summary>
[DisallowMultipleComponent]
public class vc_StuntSetQuest : vc_FitQuestKit
{
    [Header("Set")]
    [SerializeField] private SpriteRenderer filmCamera;
    [SerializeField] private Sprite[] cameraFrames;
    [SerializeField] private SpriteRenderer clapboard;
    [SerializeField] private Sprite clapOpen;
    [SerializeField] private Sprite clapSnap;
    [SerializeField] private SpriteRenderer crashPad;
    [SerializeField] private SpriteRenderer ramp;
    [SerializeField] private Sprite rampSmall;
    [SerializeField] private Sprite rampBig;
    [SerializeField] private SpriteRenderer propCart;

    [Header("Stunt double")]
    [SerializeField] private SpriteRenderer stunt;
    [SerializeField] private Sprite[] runFrames;
    [SerializeField] private Sprite[] rollFrames;
    [SerializeField] private Sprite landSprite;

    [Header("Effects")]
    [SerializeField] private Sprite[] storyboards;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite shoutBubble;

    [Header("Timing (seconds)")]
    [SerializeField] private float craftTime = 2f;
    [SerializeField] private float barrierTime = 4f;
    [SerializeField] private float paintTime = 5f;
    [SerializeField] private float buildTime = 8f;
    [SerializeField] private float pushTime = 8f;
    [SerializeField] private float planTime = 6f;
    [SerializeField] private float teachTime = 7f;
    [SerializeField] private float commandTime = 7f;

    private Vector3 _stuntRest, _cartRest, _padScale;
    private float _recClock, _idleClock;

    protected override Vector3 BarWorldPosition => (crashPad != null ? crashPad.transform.position : transform.position) + new Vector3(-0.5f, 2f, 0f);

    private void Awake()
    {
        if (stunt != null) _stuntRest = stunt.transform.position;
        if (propCart != null) _cartRest = propCart.transform.position;
        if (crashPad != null) _padScale = crashPad.transform.localScale;
    }

    protected override void OnBegin()
    {
        ClearSpawned();
        ResetSet();
        _recClock = 0f; _idleClock = 1.2f;
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", "Stunt Double",
            "Direk Bong's student film needs a stunt. Can you pull it off?", new[] { "Perform the stunt" });
    }

    private void ResetSet()
    {
        if (stunt != null) { stunt.transform.position = _stuntRest; stunt.sprite = runFrames[1]; }
        if (propCart != null) propCart.transform.position = _cartRest;
        if (crashPad != null) crashPad.transform.localScale = _padScale;
        if (ramp != null) ramp.sprite = rampSmall;
        if (clapboard != null) clapboard.sprite = clapOpen;
    }

    // ── Idle life: the camera's REC light blinks, the stunt double warms up ──

    private void Update()
    {
        if (!Started || Done) return;
        _recClock += Time.deltaTime;
        if (_recClock >= 0.5f && filmCamera != null && cameraFrames.Length > 1)
        {
            _recClock = 0f;
            filmCamera.sprite = filmCamera.sprite == cameraFrames[0] ? cameraFrames[1] : cameraFrames[0];
        }
        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f && !Working)
        {
            _idleClock = Random.Range(1.4f, 2.4f);
            StartCoroutine(Hop(stunt != null ? stunt.transform : null, 3));
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("barrier")) { StartWork(BarrierRoutine()); return; }
        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        if (d.HasTag("build")) { StartWork(BuildRoutine()); return; }
        if (d.HasTag("push")) { StartWork(PushRoutine()); return; }
        if (d.HasTag("plan")) { StartWork(PlanRoutine()); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        Toast("That won't make a good stunt.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (stunt != null) { stunt.transform.position = _stuntRest; stunt.sprite = runFrames[1]; }
        if (propCart != null) propCart.transform.position = _cartRest;
        ClearSpawned();
    }

    private IEnumerator CraftRoutine()
    {
        Toast("Run, roll, land...", craftTime);
        ShowBar(true);
        var c = new float[1];
        yield return Perform(craftTime - 0.2f, c, craftTime, 0.7f);
        yield return Tick(craftTime - c[0], c, craftTime);
        Cut("Cut! Perfect take on the first try!");
    }

    private IEnumerator BarrierRoutine()
    {
        Toast("You shape a safe landing first...", barrierTime);
        ShowBar(true);
        var c = new float[1];
        for (int i = 0; i < 4; i++)
        {
            if (crashPad != null)
            {
                crashPad.transform.localScale = new Vector3(_padScale.x * (1f + 0.08f * (i + 1)), _padScale.y * (1f + 0.05f * (i + 1)), 1f);
                Fx(dustFrames, crashPad.bounds.center, 10f, 0.2f);
            }
            Sfx("plop", 0.5f);
            yield return Tick(0.45f, c, barrierTime);
        }
        yield return Perform(barrierTime - c[0] - 0.1f, c, barrierTime, 0.7f);
        yield return Tick(barrierTime - c[0], c, barrierTime);
        Cut("Soft landing, clean stunt. Cut!");
    }

    private IEnumerator PaintRoutine()
    {
        Toast("You storyboard the stunt, panel by panel...", paintTime);
        ShowBar(true);
        var c = new float[1];
        Vector3 at = filmCamera != null ? Above(filmCamera, 0.5f) + new Vector3(-0.6f, 0f, 0f) : transform.position;
        for (int i = 0; i < storyboards.Length; i++)
        {
            var p = Spawn(storyboards[i], at + new Vector3(i * 0.8f, 0f, 0f), 22, "Storyboard");
            StartCoroutine(PopIn(p.transform));
            Sfx("brush", 0.4f);
            yield return Tick(0.9f, c, paintTime);
        }
        yield return Perform(paintTime - c[0] - 0.1f, c, paintTime, 0.7f);
        yield return Tick(paintTime - c[0], c, paintTime);
        Cut("Just like the storyboard. Cut!");
    }

    private IEnumerator BuildRoutine()
    {
        Toast("You build a bigger ramp...", buildTime);
        ShowBar(true);
        var c = new float[1];
        Vector3 r = ramp != null ? ramp.bounds.center : transform.position;
        for (int i = 0; i < 9; i++)
        {
            Fx(hammerFrames, r + new Vector3(Random.Range(-0.4f, 0.4f), 0.3f, 0f), 8f);
            Sfx("hammer", 0.5f);
            if (i == 5 && ramp != null) { ramp.sprite = rampBig; Fx(dustFrames, r, 10f, 0.2f); }
            yield return Tick(0.6f, c, buildTime);
        }
        yield return Perform(buildTime - c[0] - 0.1f, c, buildTime, 1.2f);
        yield return Tick(buildTime - c[0], c, buildTime);
        Cut("Big ramp, big air. What a jump! Cut!");
    }

    private IEnumerator PushRoutine()
    {
        Toast("You push the prop cart for a crash gag...", pushTime);
        ShowBar(true);
        var c = new float[1];
        if (propCart == null) { yield return Tick(pushTime, c, pushTime); Cut("Crash! Cut!"); yield break; }
        Vector3 to = crashPad != null ? crashPad.bounds.center + new Vector3(-0.6f, 0f, 0f) : _cartRest + Vector3.right * 3f;
        float roll = pushTime - 1.4f, t = 0f;
        while (t < roll)
        {
            t += Time.deltaTime;
            float k = t / roll;
            propCart.transform.position = Snap(Vector3.Lerp(_cartRest, to, k * k));
            if (Mathf.Repeat(t, 0.5f) < Time.deltaTime) Sfx("scrape", 0.4f);
            SetBar(t / pushTime);
            yield return null;
        }
        c[0] = roll;
        Sfx("hammer", 0.9f, 0f);
        Fx(dustFrames, to, 10f, 0.3f);
        Fx(sparkFrames, to + new Vector3(0.3f, 0.4f, 0f), 12f, 0.3f);
        StartCoroutine(Shake(propCart.transform, propCart.transform.localPosition, 0.4f, 2));
        if (stunt != null) StartCoroutine(Hop(stunt.transform, 4));
        yield return Tick(pushTime - c[0], c, pushTime);
        Cut("CRASH! The gag looks amazing on camera. Cut!");
    }

    private IEnumerator PlanRoutine()
    {
        Toast("You work out the jump, step by step...", planTime);
        ShowBar(true);
        var c = new float[1];
        // a dotted arc appears from the ramp lip to the pad, one dot at a time
        if (stunt != null && crashPad != null && sparkFrames != null && sparkFrames.Length > 0)
        {
            Vector3 jump = ramp != null ? new Vector3(ramp.bounds.max.x, ramp.bounds.max.y - 0.1f, 0f) : _stuntRest + Vector3.right;
            Vector3 land = crashPad.bounds.center;
            const int dots = 7;
            for (int i = 1; i <= dots; i++)
            {
                float k = i / (float)(dots + 1);
                Vector3 p = Vector3.Lerp(jump, land, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.7f, 0f);
                var dot = Spawn(sparkFrames[sparkFrames.Length - 1], p, 21, "PlanDot");
                StartCoroutine(PopIn(dot.transform));
                Sfx("text_blip", 0.35f);
                yield return Tick(0.35f, c, planTime);
            }
        }
        StartCoroutine(Hop(stunt != null ? stunt.transform : null, 2));
        Sfx("idea", 0.5f);
        yield return Tick(0.5f, c, planTime);
        yield return Perform(planTime - c[0] - 0.1f, c, planTime, 0.7f);
        ClearSpawned();
        yield return Tick(planTime - c[0], c, planTime);
        Cut("Landed right on the planned spot. Cut!");
    }

    private IEnumerator TeachRoutine()
    {
        Toast("You coach the stunt double through each move...", teachTime);
        ShowBar(true);
        var c = new float[1];
        // three practice reps in place: tuck, roll, land (the double copies each one)
        for (int rep = 0; rep < 3 && stunt != null; rep++)
        {
            if (shoutBubble != null) Fx(new[] { shoutBubble }, Above(stunt), 1.5f, 0.2f, 24);
            Sfx("text_blip", 0.5f);
            yield return Tick(0.4f, c, teachTime);
            float t = 0f, len = 0.8f;
            while (t < len)
            {
                t += Time.deltaTime;
                stunt.sprite = rep == 2 ? landSprite : rollFrames[(int)(t * 10f) % rollFrames.Length];
                stunt.transform.position = Snap(_stuntRest + new Vector3(0f, Mathf.Sin(t / len * Mathf.PI) * 0.25f, 0f));
                SetBar((c[0] + t) / teachTime);
                yield return null;
            }
            stunt.transform.position = _stuntRest;
            stunt.sprite = runFrames[1];
            c[0] += len;
            Fx(dustFrames, _stuntRest, 10f, 0.15f);
            Sfx("plop", 0.4f);
            yield return Tick(0.3f, c, teachTime);
        }
        yield return Perform(teachTime - c[0] - 0.1f, c, teachTime, 0.7f);
        yield return Tick(teachTime - c[0], c, teachTime);
        Cut("All that practice paid off. Cut!");
    }

    private IEnumerator CommandRoutine()
    {
        Toast("Places! Camera! Action!", commandTime);
        ShowBar(true);
        var c = new float[1];
        // shout over the stunt double, the camera, then the clapboard
        SpriteRenderer[] crew = { stunt, filmCamera, clapboard };
        foreach (SpriteRenderer who in crew)
        {
            if (who == null) continue;
            if (shoutBubble != null) Fx(new[] { shoutBubble }, Above(who), 1f / 1.2f, 0.2f, 24);
            Sfx("text_blip", 0.6f);
            StartCoroutine(Hop(who.transform, 2));
            yield return Tick(1.3f, c, commandTime);
        }
        if (clapboard != null) { clapboard.sprite = clapSnap; Sfx("clack", 0.7f, 0f); }
        yield return Tick(0.3f, c, commandTime);
        if (clapboard != null) clapboard.sprite = clapOpen;
        yield return Perform(commandTime - c[0] - 0.1f, c, commandTime, 0.7f);
        yield return Tick(commandTime - c[0], c, commandTime);
        Cut("The crew followed your call perfectly. Cut!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Run up, roll through the air, land on the pad.</summary>
    private IEnumerator Perform(float seconds, float[] c, float total, float height)
    {
        if (stunt == null || crashPad == null) { yield return Tick(seconds, c, total); yield break; }
        float start = c[0];
        Vector3 from = _stuntRest;
        Vector3 jump = ramp != null ? new Vector3(ramp.bounds.max.x, ramp.bounds.max.y - 0.1f, 0f) : from + Vector3.right;
        Vector3 land = crashPad.bounds.center + new Vector3(0f, -0.15f, 0f);
        float run = seconds * 0.45f, air = seconds * 0.35f, rest = seconds - run - air, t = 0f;
        Sfx("cut_whoosh", 0.4f);
        while (t < run)
        {
            t += Time.deltaTime;
            stunt.transform.position = Snap(Vector3.Lerp(from, jump, t / run));
            stunt.sprite = runFrames[(int)(t * 10f) % runFrames.Length];
            SetBar((start + t) / total);
            yield return null;
        }
        float a = 0f;
        Sfx("cut_whoosh", 0.6f);
        while (a < air)
        {
            a += Time.deltaTime;
            float k = a / air;
            stunt.transform.position = Snap(Vector3.Lerp(jump, land, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * height, 0f));
            stunt.sprite = rollFrames[(int)(a * 14f) % rollFrames.Length];
            SetBar((start + run + a) / total);
            yield return null;
        }
        stunt.sprite = landSprite;
        Fx(dustFrames, land + new Vector3(0f, 0.15f, 0f), 10f, 0.25f);
        Sfx("plop", 0.8f, 0f);
        yield return Step(rest, (start + run + air) / total, (start + seconds) / total);
        stunt.sprite = runFrames[1];
        c[0] = start + seconds;
    }

    private void Cut(string msg)
    {
        if (clapboard != null) clapboard.sprite = clapSnap;
        Sfx("clack", 0.9f, 0f);
        if (shoutBubble != null && clapboard != null) Fx(new[] { shoutBubble }, Above(clapboard), 0.8f, 0.2f, 24);
        if (stunt != null) Fx(sparkFrames, Above(stunt), 12f, 0.2f);
        Finish(msg);
    }
}
