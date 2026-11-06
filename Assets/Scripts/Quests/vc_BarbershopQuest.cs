using System.Collections;
using UnityEngine;

/// <summary>
/// E quest (E4). Keep Mang Ramil's barbershop running. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   command  E  best fit     runs the shop: customers rotate through both chairs          ~2s
///   summon   E  workaround   calls "Next!", one customer per press                         x3
///   charm    E  workaround   keeps the waiting line happy (hearts) while the cuts go on    3s
///   persuade E  workaround   sells a hair tonic add-on while the line moves                4s
///   direct   C  off-letter   hands out queue numbers, then the line moves in order         7s
///   craft    A  off-letter   gives the haircuts themself, slowly                           8s
///   anything else: a hint, no effect
///
/// Each customer goes bench -> chair -> snip snip -> neat haircut -> out the door.
/// While a slow fix is running the best fit (command) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_BarbershopQuest : vc_FitQuestBase
{
    [Header("Shop")]
    [SerializeField] private SpriteRenderer pole;
    [SerializeField] private Sprite[] poleFrames;
    [Tooltip("Seat spots on the barber chairs (where a customer sits).")]
    [SerializeField] private Transform[] chairSeats;
    [Tooltip("Where served customers walk out to.")]
    [SerializeField] private Transform exitSpot;

    [Header("Customers")]
    [SerializeField] private SpriteRenderer[] customers;
    [SerializeField] private Sprite[] shaggy;
    [SerializeField] private Sprite[] neat;

    [Header("Effects")]
    [SerializeField] private Sprite[] scissorFrames;
    [SerializeField] private Sprite[] queueTags;
    [SerializeField] private Sprite heart;
    [SerializeField] private Sprite angryBubble;
    [SerializeField] private Sprite shoutBubble;
    [SerializeField] private Sprite tonic;
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float commandTime = 2f;
    [SerializeField] private int callPresses = 3;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private float persuadeTime = 4f;
    [SerializeField] private float directTime = 7f;
    [SerializeField] private float craftTime = 8f;
    [SerializeField] private float mapTime = 4f;

    // Skill system v2 (QUEST_REDESIGN.md): Direct (prep) hands out queue numbers by itself, then
    // Call brings everyone through in order at once. Preference: Charm (keep the line happy).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Mang Ramil",
        presses = 5,
        bestFitTag = "command",
        bestFitSeconds = 2f,
        prepTag = "direct",
        prepUnlocksTag = "summon",
        prepLine = "Queue-number tickets hand themselves out down the line...",
        preferredTag = "charm",
        preferenceLine = "Not one grumpy face left! Salamat, iho!",
        fastLines = new[]
        {
            ("summon", "The whole line, done!"),
            ("charm", "Nobody's grumpy anymore!"),
            ("persuade", "Sold out of tonic already!"),
            ("map", "The shop flows so much better now!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    // 0 waiting, 1 walking/in chair, 2 done
    private int[] _state;
    private Vector3[] _rest;
    private bool[] _chairBusy;
    private int _calls;
    private float _poleClock, _idleClock;
    private int _poleFrame;

    protected override Vector3 BarWorldPosition =>
        (chairSeats != null && chairSeats.Length > 0 ? Mid() : transform.position) + new Vector3(-0.5f, 2.2f, 0f);

    private Vector3 Mid()
    {
        Vector3 s = Vector3.zero;
        foreach (Transform c in chairSeats) s += c.position;
        return s / chairSeats.Length;
    }

    private void Awake()
    {
        int n = customers != null ? customers.Length : 0;
        _rest = new Vector3[n];
        _state = new int[n];
        for (int i = 0; i < n; i++) _rest[i] = customers[i].transform.position;
        _chairBusy = new bool[chairSeats != null ? chairSeats.Length : 0];
    }

    protected override void OnBegin()
    {
        StopAllCustomerMoves();
        _calls = 0;
        _idleClock = 1.5f;
        for (int i = 0; i < customers.Length; i++)
        {
            _state[i] = 0;
            customers[i].enabled = true;
            customers[i].transform.position = _rest[i];
            if (shaggy != null && i < shaggy.Length) customers[i].sprite = shaggy[i];
        }
        for (int c = 0; c < _chairBusy.Length; c++) _chairBusy[c] = false;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Barbershop Rush",
            "Mang Ramil: the line is long and the customers get grumpy when they wait. Help him run the shop. He loves a happy, smiling line.",
            new[] { "Keep the shop running" });
    }

    // ── Idle life: the pole spins, the line grumbles ─────────────────────

    private void Update()
    {
        if (pole != null && poleFrames != null && poleFrames.Length > 0)
        {
            _poleClock += Time.deltaTime;
            if (_poleClock >= 0.12f) { _poleClock = 0f; _poleFrame = (_poleFrame + 1) % poleFrames.Length; pole.sprite = poleFrames[_poleFrame]; }
        }
        if (!Started || Done || Working) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(1.8f, 2.8f);
        int i = Random.Range(0, customers.Length);
        if (_state[i] == 0)
        {
            if (angryBubble != null) Fx(new[] { angryBubble }, Top(customers[i].transform.position), 1.4f, 0.15f, 24);
            StartCoroutine(Shake(customers[i].transform, customers[i].transform.localPosition, 0.25f, 1));
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("summon")) { CallNext(); return; }
        if (d.HasTag("charm")) { StartWork(CharmRoutine(PathTime(charmTime))); return; }
        if (d.HasTag("persuade")) { StartWork(TonicRoutine(PathTime(persuadeTime))); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine(PathTime(mapTime))); return; }
        if (d.HasTag("direct")) { StartWork(QueueRoutine(PathTime(directTime))); return; }
        if (d.HasTag("craft")) { StartWork(SelfCutRoutine(PathTime(craftTime))); return; }
        Toast("The shop needs someone to run it.", 2.2f);
    }

    // ── Command: run the shop, both chairs at once ───────────────────────

    private IEnumerator CommandRoutine()
    {
        Toast("You run the shop like a pro!", commandTime);
        ShowBar(true);
        float cut = 0.55f, t = 0f;
        while (!AllDone() && t < 6f)
        {
            int i = NextWaiting();
            if (i >= 0 && FreeChair() >= 0) StartCoroutine(Serve(i, cut));
            t += Time.deltaTime;
            SetBar(Mathf.Min(0.95f, t / commandTime));
            yield return null;
        }
        Finish("The line is cleared. The shop runs smoothly!");
    }

    // ── Call: "Next!", one per press ─────────────────────────────────────

    // 3 presses at Lv1, 2 at Lv2, 1 at Lv3 or after the Direct prep (then the whole line goes through at once).
    private void CallNext()
    {
        _calls++;
        int need = Mathf.Max(1, PathPresses(callPresses));
        if (_calls >= need)
        {
            StartWork(CallFinishRoutine(need == 1));
            return;
        }

        int waiting = 0;
        for (int w = 0; w < customers.Length; w++) if (_state[w] == 0) waiting++;
        int share = Mathf.Max(1, Mathf.CeilToInt(waiting / (float)(need - _calls + 1)));
        for (int n = 0; n < share; n++)
        {
            int i = NextWaiting();
            if (i < 0 || FreeChair() < 0) break;
            CallOut(i);
            StartCoroutine(Serve(i, 0.5f));
        }
        Toast($"\"Next!\" ({_calls}/{need})", 1.6f);
    }

    private void CallOut(int i)
    {
        if (shoutBubble != null) Fx(new[] { shoutBubble }, Top(customers[i].transform.position) + new Vector3(0.6f, 0.3f, 0f), 1.4f, 0.1f, 25);
        SunodGame.Core.vc_Sfx.Play("bell", 0.6f);
    }

    // The last press: everyone still waiting is called through in order, then the quest ends.
    private IEnumerator CallFinishRoutine(bool oneGo)
    {
        Toast(oneGo ? "\"Next! Next! Next!\" The whole line comes through in order!" : "\"Next!\" You call the rest of the line...", 2f);
        ShowBar(true);
        float gap = oneGo ? 0.1f : 0.25f, cut = oneGo ? 0.45f : 0.5f;
        int k;
        while ((k = NextWaiting()) >= 0)
        {
            if (FreeChair() < 0) { yield return null; continue; }
            CallOut(k);
            StartCoroutine(Serve(k, cut));
            int served = 0;
            for (int s = 0; s < customers.Length; s++) if (_state[s] != 0) served++;
            SetBar(served / (float)Mathf.Max(1, customers.Length));
            yield return new WaitForSeconds(gap);
        }
        while (!AllDone()) yield return null;
        Finish(oneGo ? "\"Next!\" The whole line went through in one go!" : "\"Next!\" Every customer got a fresh cut.");
    }

    // ── Charm: a happy line while the cuts go on ─────────────────────────

    private IEnumerator CharmRoutine(float charmTime)
    {
        Toast("You keep the waiting line happy...", charmTime);
        ShowBar(true);
        yield return ServeAllInOrder(charmTime, 0f, charmTime, onEach: () =>
        {
            for (int k = 0; k < customers.Length; k++)
                if (_state[k] == 0 && heart != null) Fx(new[] { heart }, Top(customers[k].transform.position), 1.5f, 0.3f, 24);
            SunodGame.Core.vc_Sfx.Play("chime", 0.35f, 0.2f);
        });
        Finish("Nobody left. Happy customers all around!");
    }

    // ── Persuade: hair tonic add-on ──────────────────────────────────────

    private IEnumerator TonicRoutine(float persuadeTime)
    {
        Toast("You sell a hair tonic while they wait...", persuadeTime);
        ShowBar(true);
        yield return ServeAllInOrder(persuadeTime, 0f, persuadeTime, onEach: () =>
        {
            int k = NextWaiting();
            Vector3 at = k >= 0 ? customers[k].transform.position : Mid();
            if (tonic != null) Fx(new[] { tonic }, at + new Vector3(0.4f, 0.8f, 0f), 1.5f, 0.2f, 24);
            Fx(coinFrames, at + new Vector3(0.4f, 1.3f, 0f), 10f, 0.3f, 25);
            SunodGame.Core.vc_Sfx.Play("coin", 0.5f, 0.1f);
        });
        Finish("Line moved, and you sold the tonic too!");
    }

    // ── Map: rearrange the chairs and bench so the line flows ────────────

    private IEnumerator MapRoutine(float mapTime)
    {
        Toast("You rearrange the chairs and bench so the line flows...", Mathf.Min(2.5f, mapTime));
        ShowBar(true);
        float slide = Mathf.Max(0.4f, mapTime * 0.4f), t = 0f, scrape = 0f;
        Vector3 mid = chairSeats != null && chairSeats.Length > 0 ? Mid() : transform.position;
        var from = new Vector3[customers.Length];
        var to = new Vector3[customers.Length];
        for (int i = 0; i < customers.Length; i++)
        {
            from[i] = customers[i].transform.position;
            Vector3 dir = mid - from[i];
            dir.z = 0f;
            to[i] = dir.sqrMagnitude > 0.01f ? from[i] + dir.normalized * 0.5f : from[i];
        }
        if (chairSeats != null)
            foreach (Transform seat in chairSeats) StartCoroutine(Shake(seat, seat.localPosition, 0.5f, 1));
        while (t < slide)
        {
            t += Time.deltaTime;
            scrape += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / slide);
            for (int i = 0; i < customers.Length; i++)
                if (_state[i] == 0) customers[i].transform.position = Snap(Vector3.Lerp(from[i], to[i], k));
            if (scrape >= 0.4f)
            {
                scrape = 0f;
                SunodGame.Core.vc_Sfx.Play("scrape", 0.4f, 0.1f);
            }
            SetBar(t / mapTime);
            yield return null;
        }
        for (int i = 0; i < customers.Length; i++)
            if (_state[i] == 0)
            {
                customers[i].transform.position = to[i];
                Fx(sparkFrames, Top(to[i]) - new Vector3(0f, 0.5f, 0f), 12f, 0.1f);
            }
        yield return ServeAllInOrder(Mathf.Max(0.4f, mapTime - slide), slide, mapTime, null);
        Finish("The shop flows so much better. The line just glides through!");
    }

    // ── Prep (Direct): queue numbers hand themselves out down the line ───

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        float gap = 0.9f / Mathf.Max(1, customers.Length);
        for (int i = 0; i < customers.Length; i++)
        {
            if (_state[i] == 0 && queueTags != null && i < queueTags.Length)
                Fx(new[] { queueTags[i] }, Top(customers[i].transform.position), 1.4f, 0.15f, 24);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f, 0.05f);
            yield return new WaitForSeconds(gap);
        }
        yield return new WaitForSeconds(0.1f);
    }

    // ── Direct: queue numbers, then in order ─────────────────────────────

    private IEnumerator QueueRoutine(float directTime)
    {
        Toast("You hand out queue numbers...", directTime);
        ShowBar(true);
        float tagTime = 2.4f * directTime / 7f, t = 0f;
        float per = tagTime / Mathf.Max(1, customers.Length);
        for (int i = 0; i < customers.Length; i++)
        {
            if (_state[i] == 0 && queueTags != null && i < queueTags.Length)
                Fx(new[] { queueTags[i] }, Top(customers[i].transform.position), 0.9f, 0.1f, 24);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.05f);
            yield return BarStep(per, t / directTime, (t + per) / directTime);
            t += per;
        }
        yield return ServeAllInOrder(Mathf.Max(0.4f, directTime - t), t, directTime, null);
        Finish("Numbered and orderly. The line is done!");
    }

    // ── Craft: cut it yourself, slowly ───────────────────────────────────

    private IEnumerator SelfCutRoutine(float craftTime)
    {
        Toast("You pick up the scissors yourself...", craftTime);
        ShowBar(true);
        yield return ServeAllInOrder(craftTime, 0f, craftTime, null);
        Finish("Slow and careful, but they all look sharp!");
    }

    // ── Serving ──────────────────────────────────────────────────────────

    /// <summary>Serves every waiting customer one at a time over 'span' seconds.</summary>
    private IEnumerator ServeAllInOrder(float span, float t0, float total, System.Action onEach)
    {
        int left = 0;
        for (int i = 0; i < customers.Length; i++) if (_state[i] == 0) left++;
        float per = span / Mathf.Max(1, left), t = t0;
        int k;
        while ((k = NextWaiting()) >= 0)
        {
            onEach?.Invoke();
            float cut = Mathf.Max(0.4f, per - 0.9f);
            StartCoroutine(Serve(k, cut));
            yield return BarStep(per, t / total, (t + per) / total);
            t += per;
        }
        while (!AllDone()) yield return null;
    }

    private IEnumerator Serve(int i, float cutTime)
    {
        if (_state[i] != 0) yield break;
        int chair = FreeChair();
        if (chair < 0) yield break;
        _state[i] = 1;
        _chairBusy[chair] = true;
        Transform c = customers[i].transform;

        yield return Move(c, c.position, chairSeats[chair].position, 0.3f);

        float t = 0f, snip = 0f;
        while (t < cutTime)
        {
            t += Time.deltaTime; snip += Time.deltaTime;
            if (snip >= 0.18f)
            {
                snip = 0f;
                Fx(scissorFrames, Top(c.position) + new Vector3(Random.Range(-0.2f, 0.2f), -0.3f, 0f), 10f, 0f, 24);
                SunodGame.Core.vc_Sfx.Play("snip", 0.4f, 0.2f, 0.12f);
            }
            yield return null;
        }
        if (neat != null && i < neat.Length) customers[i].sprite = neat[i];
        Fx(sparkFrames, Top(c.position) - new Vector3(0f, 0.3f, 0f), 12f, 0.15f);
        SunodGame.Core.vc_Sfx.Play("step_done", 0.4f);
        _chairBusy[chair] = false;

        Vector3 exit = exitSpot != null ? exitSpot.position + new Vector3(0f, -0.25f * i, 0f) : c.position + Vector3.right;
        yield return Move(c, c.position, exit, 0.35f);
        _state[i] = 2;
    }

    private IEnumerator Move(Transform tr, Vector3 from, Vector3 to, float seconds)
    {
        float t = 0f;
        while (t < seconds && tr != null)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            tr.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI * 2f) > 0 ? 1f / 22f : 0f, 0f));
            yield return null;
        }
        if (tr != null) tr.position = to;
    }

    private void StopAllCustomerMoves()
    {
        StopAllCoroutines();
    }

    private int NextWaiting()
    {
        for (int i = 0; i < customers.Length; i++) if (_state[i] == 0) return i;
        return -1;
    }

    private int FreeChair()
    {
        for (int c = 0; c < _chairBusy.Length; c++) if (!_chairBusy[c]) return c;
        return -1;
    }

    private bool AllDone()
    {
        for (int i = 0; i < customers.Length; i++) if (_state[i] != 2) return false;
        return true;
    }

    private static Vector3 Top(Vector3 p) => p + new Vector3(0f, 1.4f, 0f);

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
