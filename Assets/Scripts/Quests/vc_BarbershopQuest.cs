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
            "The line is long at Mang Ramil's barbershop. Help run it.",
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
        if (d.HasTag("charm")) { StartWork(CharmRoutine()); return; }
        if (d.HasTag("persuade")) { StartWork(TonicRoutine()); return; }
        if (d.HasTag("direct")) { StartWork(QueueRoutine()); return; }
        if (d.HasTag("craft")) { StartWork(SelfCutRoutine()); return; }
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

    private void CallNext()
    {
        int i = NextWaiting();
        if (i < 0) return;
        if (FreeChair() < 0) { Toast("Both chairs are busy...", 1.4f); return; }
        _calls++;
        if (shoutBubble != null) Fx(new[] { shoutBubble }, Top(customers[i].transform.position) + new Vector3(0.6f, 0.3f, 0f), 1.4f, 0.1f, 25);
        SunodGame.Core.vc_Sfx.Play("bell", 0.6f);
        StartCoroutine(Serve(i, 0.5f));
        if (NextWaiting() >= 0)
        {
            Toast($"\"Next!\" ({_calls}/{callPresses})", 1.6f);
            return;
        }
        Finish("\"Next!\" Every customer got a fresh cut.");
    }

    // ── Charm: a happy line while the cuts go on ─────────────────────────

    private IEnumerator CharmRoutine()
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

    private IEnumerator TonicRoutine()
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

    // ── Direct: queue numbers, then in order ─────────────────────────────

    private IEnumerator QueueRoutine()
    {
        Toast("You hand out queue numbers...", directTime);
        ShowBar(true);
        float tagTime = 2.4f, t = 0f;
        float per = tagTime / Mathf.Max(1, customers.Length);
        for (int i = 0; i < customers.Length; i++)
        {
            if (_state[i] == 0 && queueTags != null && i < queueTags.Length)
                Fx(new[] { queueTags[i] }, Top(customers[i].transform.position), 0.9f, 0.1f, 24);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.05f);
            yield return BarStep(per, t / directTime, (t + per) / directTime);
            t += per;
        }
        yield return ServeAllInOrder(directTime - t, t, directTime, null);
        Finish("Numbered and orderly. The line is done!");
    }

    // ── Craft: cut it yourself, slowly ───────────────────────────────────

    private IEnumerator SelfCutRoutine()
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
