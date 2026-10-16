using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// E quest (E7). Sell the house to the buyer. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   persuade E  best fit     close the deal; a SOLD banner drops on the sign           ~1.5s
///   charm    E  workaround   the buyer warms up, then buys                              3s
///   map      E  workaround   show them the view from the best room                      4s
///   command  E  workaround   lead the tour like a pro                                   4s
///   paint    A  off-letter   freshen up the walls to stage the house                    8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (persuade) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_OpenHouseQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer buyer;
    [SerializeField] private Sprite[] buyerWalk;
    [SerializeField] private Transform sign;
    [SerializeField] private Transform window;
    [SerializeField] private Transform bestRoom;
    [Tooltip("Stops on the tour, in order (the last is the best room).")]
    [SerializeField] private Transform[] tourStops;

    [Header("Art")]
    [SerializeField] private Sprite soldBanner;
    [SerializeField] private Sprite keyRing;
    [SerializeField] private Sprite bubbleQuestion;
    [SerializeField] private Sprite bubbleSmile;
    [SerializeField] private Sprite bubbleDeal;
    [SerializeField] private Sprite bubbleDots;
    [SerializeField] private Sprite paintStripe;
    [SerializeField] private Sprite paintRoller;

    [Header("Effects")]
    [SerializeField] private Sprite[] heartFrames;
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] checkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float persuadeTime = 1.5f;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private float mapTime = 4f;
    [SerializeField] private float commandTime = 4f;
    [SerializeField] private float paintTime = 8f;

    private Vector3 _buyerStart, _signRest;
    private float _elapsed, _total = 1f;
    private float _idleClock, _swayClock;
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private GameObject _banner;

    protected override Vector3 BarWorldPosition => (sign != null ? sign.position : transform.position) + new Vector3(-0.5f, 1.5f, 0f);

    private void Awake()
    {
        if (buyer != null) _buyerStart = buyer.transform.position;
        if (sign != null) _signRest = sign.localPosition;
    }

    protected override void OnBegin()
    {
        CleanSpawned();
        if (_banner != null) Destroy(_banner);
        _banner = null;
        if (buyer != null)
        {
            buyer.gameObject.SetActive(true);
            buyer.transform.position = _buyerStart;
            buyer.flipX = false;
            buyer.color = Color.white;
            if (buyerWalk != null && buyerWalk.Length > 0) buyer.sprite = buyerWalk[0];
        }
        if (sign != null) sign.localPosition = _signRest;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Open House",
            "A buyer is looking around. Sell them the house.",
            new[] { "Sell the house" });
    }

    // ── Idle life: the buyer looks around, the sign sways ────────────────

    private void Update()
    {
        if (!Started || Done || buyer == null) return;

        _swayClock -= Time.deltaTime;
        if (_swayClock <= 0f && sign != null)
        {
            _swayClock = 2.2f;
            StartCoroutine(Sway());
        }

        if (Working) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f)
        {
            _idleClock = 2.8f;
            Fx(new[] { bubbleQuestion }, buyer.transform.position + new Vector3(0f, 1.2f, 0f), 1.4f, 0.15f, 26);
            buyer.flipX = !buyer.flipX;
        }
    }

    private IEnumerator Sway()
    {
        for (int i = 0; i < 3; i++)
        {
            if (sign == null || _banner != null) yield break;
            sign.localPosition = _signRest + new Vector3((i % 2 == 0 ? 1 : -1) / 22f, 0f, 0f);
            yield return new WaitForSeconds(0.12f);
        }
        if (sign != null && _banner == null) sign.localPosition = _signRest;
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine()); return; }
        if (BlockedByWork()) return;

        if (d.HasTag("charm")) { StartWork(CharmRoutine()); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine()); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }

        Toast("That won't sell a house.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        CleanSpawned();
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

    // ── Persuade: close the deal ─────────────────────────────────────────

    private IEnumerator PersuadeRoutine()
    {
        Toast("You make your pitch...", persuadeTime);
        Timeline(persuadeTime);
        Vector3 b = buyer.transform.position;
        Fx(new[] { bubbleDots }, b + new Vector3(0f, 1.2f, 0f), 3f, 0.1f, 26);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f);
        yield return Wait(persuadeTime * 0.4f);
        Fx(new[] { bubbleDeal }, b + new Vector3(0f, 1.2f, 0f), 2f, 0.15f, 26);
        yield return Wait(persuadeTime * 0.3f);
        yield return Sell("Deal! They bought the house.");
    }

    // ── Charm: the buyer warms up ────────────────────────────────────────

    private IEnumerator CharmRoutine()
    {
        Toast("You turn on the charm...", charmTime);
        Timeline(charmTime);
        Vector3 b = buyer.transform.position;
        float per = charmTime * 0.7f / 3f;
        for (int i = 0; i < 3; i++)
        {
            Fx(heartFrames, b + new Vector3(-0.3f + i * 0.3f, 0.9f, 0f), 3f, 0.6f, 26);
            Fx(sparkFrames, b + new Vector3(0f, 0.9f, 0f), 10f, 0.2f, 27);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.15f);
            yield return Wait(per);
        }
        Fx(new[] { bubbleSmile }, b + new Vector3(0f, 1.2f, 0f), 1.5f, 0.15f, 26);
        yield return Wait(charmTime * 0.1f);
        yield return Sell("They warmed right up. Sold!");
    }

    // ── Map: show them the view from the best room ───────────────────────

    private IEnumerator MapRoutine()
    {
        Toast("You show them the view...", mapTime);
        Timeline(mapTime);
        yield return WalkTo(bestRoom.position + new Vector3(0f, -0.9f, 0f), mapTime * 0.5f);
        float per = mapTime * 0.3f / 4f;
        for (int i = 0; i < 4; i++)
        {
            Fx(sparkFrames, window.position + new Vector3(-0.5f + i * 0.35f, 0.2f + (i % 2) * 0.3f, 0f), 10f, 0.2f, 27);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.35f, 0.2f);
            yield return Wait(per);
        }
        Fx(new[] { bubbleSmile }, buyer.transform.position + new Vector3(0f, 1.2f, 0f), 1.5f, 0.15f, 26);
        yield return Wait(mapTime * 0.1f);
        yield return Sell("The buyer loves the view! Sold!");
    }

    // ── Command: lead the tour ───────────────────────────────────────────

    private IEnumerator CommandRoutine()
    {
        Toast("You lead the tour...", commandTime);
        Timeline(commandTime);
        int n = tourStops.Length;
        float walkTotal = commandTime * 0.75f, per = walkTotal / n;
        for (int i = 0; i < n; i++)
        {
            Vector3 stop = tourStops[i].position + new Vector3(0f, -0.8f, 0f);
            yield return WalkTo(stop, per * 0.7f);
            Fx(new[] { bubbleDots }, buyer.transform.position + new Vector3(0f, 1.2f, 0f), 3f, 0.1f, 26);
            Fx(checkFrames, tourStops[i].position + new Vector3(0f, 0.6f, 0f), 3f, 0.3f, 27);
            SunodGame.Core.vc_Sfx.Play("text_blip", 0.4f, 0.1f);
            yield return Wait(per * 0.3f);
        }
        Fx(new[] { bubbleSmile }, buyer.transform.position + new Vector3(0f, 1.2f, 0f), 1.5f, 0.15f, 26);
        yield return Wait(commandTime * 0.1f);
        yield return Sell("You led the tour like a pro. Sold!");
    }

    // ── Paint: stage the house with fresh walls ──────────────────────────

    private IEnumerator PaintRoutine()
    {
        Toast("You freshen up the walls...", paintTime);
        Timeline(paintTime);
        Vector3 w = window.position;
        GameObject roller = Spawn("Roller", paintRoller, 27);
        int rows = 4;
        float paintPhase = paintTime * 0.65f, per = paintPhase / rows;
        for (int r = 0; r < rows; r++)
        {
            float y = w.y - 0.55f + r * 0.28f;
            GameObject stripe = Spawn("Paint", paintStripe, 12);
            float x0 = w.x - 2.6f, x1 = w.x + 2.6f, t = 0f;
            while (t < per)
            {
                t += Time.deltaTime; Tick(Time.deltaTime);
                float k = Mathf.Clamp01(t / per);
                float x = Mathf.Lerp(x0, x1, k);
                roller.transform.position = Snap(new Vector3(x, y + 0.1f, 0f));
                stripe.transform.position = Snap(new Vector3((x0 + x) / 2f, y, 0f));
                stripe.transform.localScale = new Vector3(Mathf.Max(0.05f, (x - x0) / (24f / 22f)), 1f, 1f);
                yield return null;
            }
            SunodGame.Core.vc_Sfx.Play("shuffle", 0.3f, 0.1f);
        }
        Destroy(roller);
        Fx(sparkFrames, w + new Vector3(0f, 0.3f, 0f), 10f, 0.3f, 27);

        yield return WalkTo(bestRoom.position + new Vector3(0f, -0.9f, 0f), paintTime * 0.25f);
        Fx(new[] { bubbleSmile }, buyer.transform.position + new Vector3(0f, 1.2f, 0f), 1.5f, 0.15f, 26);
        yield return Wait(paintTime * 0.08f);
        yield return Sell("The fresh paint staged the house. Sold!");
    }

    // ── Shared ───────────────────────────────────────────────────────────

    private IEnumerator Sell(string msg)
    {
        Vector3 b = buyer.transform.position;
        // keys fly from the sign to the buyer
        Vector3 from = sign.position + new Vector3(0f, 0.8f, 0f), to = b + new Vector3(0f, 0.6f, 0f);
        GameObject key = Spawn("Keys", keyRing, 27);
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.45f);
            if (key != null) key.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.6f, 0f));
            yield return null;
        }
        Destroy(key);
        Fx(coinFrames, b + new Vector3(-0.3f, 0.9f, 0f), 6f, 0.6f, 27);
        Fx(coinFrames, b + new Vector3(0.3f, 0.9f, 0f), 6f, 0.7f, 27);
        Fx(sparkFrames, b + new Vector3(0f, 0.8f, 0f), 10f, 0.3f, 27);
        SunodGame.Core.vc_Sfx.Play("bell", 0.7f);

        // SOLD banner drops onto the sign
        _banner = new GameObject("SoldBanner");
        _banner.transform.SetParent(transform, true);
        var sr = _banner.AddComponent<SpriteRenderer>();
        sr.sprite = soldBanner; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 27;
        Vector3 rest = sign.position + new Vector3(0f, 0.55f, 0f);
        float d = 0f;
        while (d < 0.35f)
        {
            d += Time.deltaTime;
            float k = Mathf.Clamp01(d / 0.35f);
            _banner.transform.position = Snap(rest + new Vector3(0f, (1f - k * k) * 1.6f, 0f));
            yield return null;
        }
        _banner.transform.position = Snap(rest);
        SunodGame.Core.vc_Sfx.Play("stamp", 0.8f);
        Fx(sparkFrames, rest, 10f, 0.2f, 28);
        yield return new WaitForSeconds(0.25f);
        Finish(msg);
    }

    private IEnumerator WalkTo(Vector3 to, float seconds)
    {
        Vector3 from = buyer.transform.position;
        float t = 0f;
        seconds = Mathf.Max(0.2f, seconds);
        while (t < seconds)
        {
            t += Time.deltaTime; Tick(Time.deltaTime);
            Vector3 next = Vector3.Lerp(from, to, t / seconds);
            Vector3 delta = next - buyer.transform.position;
            if (Mathf.Abs(delta.x) > 0.0005f) buyer.flipX = delta.x < 0f;
            buyer.transform.position = Snap(next);
            if (buyerWalk != null && buyerWalk.Length > 0) buyer.sprite = buyerWalk[(int)(Time.time * 8f) % buyerWalk.Length];
            yield return null;
        }
        if (buyerWalk != null && buyerWalk.Length > 0) buyer.sprite = buyerWalk[0];
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
