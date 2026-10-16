using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// E quest (E2). Sell a snack at the school stall. Any APPROPRIATE skill makes a sale - how
/// well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   persuade  E  best fit     pitch the cookies; the browsing shopper buys               ~1.5s
///   charm     E  workaround   a big smile; a kid wanders over and buys                    3s
///   summon    E  workaround   call out "Fresh cookies!"; a customer walks up              4s
///   map       E  workaround   move the stall to where people pass; a shopper buys         5s
///   craft     A  off-letter   build an eye-catching sign; the shopper notices and buys    8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (persuade) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_SnackStallQuest : vc_FitQuestBase
{
    [Header("Stall")]
    [Tooltip("Everything that moves with the stall (stall, jars, cash box, price sign).")]
    [SerializeField] private Transform[] stallParts;
    [SerializeField] private Transform buySpot;
    [SerializeField] private Transform jars;
    [SerializeField] private Vector3 stallMoveOffset = new Vector3(0f, -1.3f, 0f);
    [SerializeField] private SpriteRenderer fancySign;
    [SerializeField] private Sprite[] signStages;

    [Header("People (town NPC walk frames, 6 each)")]
    [SerializeField] private Sprite[] lookShopper;
    [SerializeField] private Sprite[] lookKid;
    [SerializeField] private Sprite[] lookCaller;
    [SerializeField] private Sprite[] lookPasserA;
    [SerializeField] private Sprite[] lookPasserB;
    [SerializeField] private float laneY = -2.2f;
    [SerializeField] private Vector2 shopperRest = new Vector2(-1.4f, -1.5f);

    [Header("Effects")]
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite[] steamFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] hammerFrames;
    [SerializeField] private Sprite[] exclaimFrames;
    [SerializeField] private Sprite[] bubbleSmile;
    [SerializeField] private Sprite[] bubblePitch;
    [SerializeField] private Sprite[] bubbleShout;
    [SerializeField] private Sprite[] bubbleHeart;
    [SerializeField] private Sprite[] cookieIcon;

    [Header("Timing (seconds)")]
    [SerializeField] private float persuadeTime = 1.5f;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private float callTime = 4f;
    [SerializeField] private float mapTime = 5f;
    [SerializeField] private float craftTime = 8f;

    private class Walker
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Sprite[] look;
        public float scale = 1f;
        public Vector3 ground;
        public float footOffset;
        public void SetGround(Vector3 g)
        {
            ground = g;
            tr.position = new Vector3(Mathf.Round(g.x * 32f) / 32f, Mathf.Round((g.y + footOffset) * 32f) / 32f, g.z);
        }
    }

    private Walker _shopper, _passA, _passB;
    private readonly List<Walker> _temp = new List<Walker>();
    private readonly List<Coroutine> _routes = new List<Coroutine>();
    private Vector3[] _partRest;
    private Vector3 _buyRest;
    private bool _stallMoved;
    private float _steamClock, _frozenUntil;
    private float _passAx, _passBx;
    private Vector3 _barPos;

    protected override Vector3 BarWorldPosition => _barPos;

    private Vector3 BuyGround => new Vector3(buySpot.position.x, buySpot.position.y, 0f);

    private void Awake()
    {
        if (stallParts != null)
        {
            _partRest = new Vector3[stallParts.Length];
            for (int i = 0; i < stallParts.Length; i++) _partRest[i] = stallParts[i].position;
        }
        if (buySpot != null) _buyRest = buySpot.position;
    }

    protected override void OnBegin()
    {
        StopRoutes();
        foreach (Walker w in _temp) if (w.tr != null) Destroy(w.tr.gameObject);
        _temp.Clear();
        if (_shopper == null) _shopper = MakeWalker(lookShopper, 1f, "Shopper");
        if (_passA == null) _passA = MakeWalker(lookPasserA, 1f, "PasserA");
        if (_passB == null) _passB = MakeWalker(lookPasserB, 1f, "PasserB");

        for (int i = 0; i < stallParts.Length; i++) stallParts[i].position = _partRest[i];
        buySpot.position = _buyRest;
        _stallMoved = false;
        if (fancySign != null) fancySign.gameObject.SetActive(false);

        _shopper.SetGround(new Vector3(shopperRest.x, shopperRest.y, 0f));
        _shopper.sr.sprite = _shopper.look[0];
        _passAx = -6f; _passBx = 2.5f; _frozenUntil = 0f; _steamClock = 0f;
        _barPos = _partRest[0] + new Vector3(-0.9f, 2.1f, 0f);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Snack Stall",
            "The cookie stall needs a customer. Make a sale!",
            new[] { "Sell a snack" });
    }

    private Walker MakeWalker(Sprite[] look, float scale, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = look[0]; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 13;
        return new Walker { tr = go.transform, sr = sr, look = look, scale = scale, footOffset = -look[0].bounds.min.y * scale };
    }

    // ── Idle life: steam, bobbing shopper, passers-by ────────────────────

    private void Update()
    {
        if (!Started || Done) return;

        _steamClock -= Time.deltaTime;
        if (_steamClock <= 0f && jars != null)
        {
            _steamClock = 0.9f;
            Fx(steamFrames, jars.position + new Vector3(Random.Range(-0.2f, 0.3f), 0.85f, 0f), 4f, 0.45f, 14);
        }

        bool frozen = Time.time < _frozenUntil;
        MovePasser(_passA, ref _passAx, 1.1f, frozen);
        MovePasser(_passB, ref _passBx, 0.8f, frozen);

        // the browsing shopper shifts weight now and then
        if (_shopper != null && _shopper.ground.y == shopperRest.y && _shopper.ground.x == shopperRest.x && !Working)
            _shopper.tr.position = new Vector3(_shopper.tr.position.x, Mathf.Round((_shopper.ground.y + _shopper.footOffset + (Time.time % 1.2f < 0.2f ? 1f / 32f : 0f)) * 32f) / 32f, 0f);
    }

    private void MovePasser(Walker w, ref float x, float speed, bool frozen)
    {
        if (w == null) return;
        if (!frozen)
        {
            x += speed * Time.deltaTime;
            if (x > 8f) x = -8f;
            w.sr.sprite = w.look[(int)(Time.time * 8f) % w.look.Length];
        }
        else w.sr.sprite = w.look[0];
        w.SetGround(new Vector3(x, laneY, 0f));
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("charm")) { StartWork(CharmRoutine()); return; }
        if (d.HasTag("summon")) { StartWork(CallRoutine()); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine()); return; }
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }
        Toast("That won't sell any snacks.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_shopper == null) return;
        StopRoutes();
        foreach (Walker w in _temp) if (w.tr != null) Destroy(w.tr.gameObject);
        _temp.Clear();
        if (_stallMoved) PlaceStall(1f);
        _shopper.SetGround(new Vector3(shopperRest.x, shopperRest.y, 0f));
        _shopper.sr.sprite = _shopper.look[0];
    }

    // ── Persuade ─────────────────────────────────────────────────────────

    private IEnumerator PersuadeRoutine()
    {
        Toast("You pitch the cookies...", persuadeTime);
        ShowBar(true);
        Say(bubblePitch, _shopper, 0.9f);
        Track(Walk(_shopper, BuyGround, persuadeTime * 0.65f, persuadeTime * 0.2f));
        yield return BarStep(persuadeTime * 0.85f, 0f, 0.85f);
        Pay(_shopper);
        yield return BarStep(persuadeTime * 0.15f, 0.85f, 1f);
        Finish("Sold! You told them the cookies are yummy.");
    }

    // ── Charm ────────────────────────────────────────────────────────────

    private IEnumerator CharmRoutine()
    {
        Toast("You flash a big, friendly smile...", charmTime);
        ShowBar(true);
        Vector3 spotTop = BuyGround + new Vector3(0f, 2.3f, 0f);
        Fx(bubbleSmile, spotTop + new Vector3(0.9f, 0f, 0f), 1.5f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f);
        Walker kid = MakeTemp(lookKid, 0.8f, new Vector3(-5f, BuyGround.y, 0f));
        Track(Walk(kid, BuyGround + new Vector3(-0.6f, 0f, 0f), charmTime * 0.7f, 0.2f));
        yield return BarStep(charmTime * 0.9f, 0f, 0.9f);
        Say(bubbleHeart, kid, 0.8f);
        Pay(kid);
        yield return BarStep(charmTime * 0.1f, 0.9f, 1f);
        Finish("Your big smile works! A kid comes to buy.");
    }

    // ── Call ─────────────────────────────────────────────────────────────

    private IEnumerator CallRoutine()
    {
        Toast("\"Fresh cookies!\" You call out...", callTime);
        ShowBar(true);
        Vector3 spotTop = BuyGround + new Vector3(0f, 2.3f, 0f);
        Fx(bubbleShout, spotTop + new Vector3(0.9f, 0f, 0f), 1.2f, 0.2f, 24);
        for (int i = 0; i < 3; i++) { SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.15f); yield return BarStep(0.15f, i * 0.04f, (i + 1) * 0.04f); }
        Walker c = MakeTemp(lookCaller, 1f, new Vector3(-7f, BuyGround.y, 0f));
        Track(Walk(c, BuyGround + new Vector3(-0.6f, 0f, 0f), callTime * 0.8f, 0f));
        yield return BarStep(callTime * 0.88f, 0.12f, 0.95f);
        Pay(c);
        yield return BarStep(callTime * 0.12f, 0.95f, 1f);
        Finish("You called them over. Sold!");
    }

    // ── Map ──────────────────────────────────────────────────────────────

    private IEnumerator MapRoutine()
    {
        Toast("You move the stall to the busy path...", mapTime);
        ShowBar(true);
        _stallMoved = true;
        float moveTime = mapTime * 0.6f, t = 0f, scrape = 0f;
        while (t < moveTime)
        {
            t += Time.deltaTime; scrape += Time.deltaTime;
            PlaceStall(Mathf.SmoothStep(0f, 1f, t / moveTime));
            SetBar(t / mapTime);
            if (scrape >= 0.45f)
            {
                scrape = 0f;
                SunodGame.Core.vc_Sfx.Play("scrape", 0.4f, 0.1f);
                Fx(dustFrames, stallParts[0].position + new Vector3(-0.6f, 0.15f, 0f), 8f, 0.2f);
            }
            yield return null;
        }
        PlaceStall(1f);

        // passers-by notice the stall in its new spot
        _frozenUntil = Time.time + 1.2f;
        Say(exclaimFrames, _passA, 0.8f);
        Say(exclaimFrames, _passB, 0.8f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f);
        float walk = mapTime * 0.3f;
        Track(Walk(_shopper, BuyGround, walk, 0f));
        yield return BarStep(walk, moveTime / mapTime, (moveTime + walk) / mapTime);
        Pay(_shopper);
        yield return BarStep(mapTime - moveTime - walk, (moveTime + walk) / mapTime, 1f);
        Finish("You moved the stall where people walk. Sold!");
    }

    private void PlaceStall(float k)
    {
        for (int i = 0; i < stallParts.Length; i++)
            stallParts[i].position = Snap(_partRest[i] + stallMoveOffset * k);
        buySpot.position = _buyRest + stallMoveOffset * k;
    }

    // ── Craft: build a sign, the shopper notices ─────────────────────────

    private IEnumerator CraftRoutine()
    {
        Toast("You make an eye-catching sign...", craftTime);
        ShowBar(true);
        float elapsed = 0f;
        if (fancySign != null) fancySign.gameObject.SetActive(true);
        float[] stageAt = { 0.2f, 1.9f, 3.6f };
        for (int s = 0; s < 3; s++)
        {
            float wait = stageAt[s] - elapsed;
            if (wait > 0f) { yield return BarStep(wait, elapsed / craftTime, stageAt[s] / craftTime); elapsed = stageAt[s]; }
            if (fancySign != null && signStages != null && s < signStages.Length)
            {
                fancySign.sprite = signStages[s];
                StartCoroutine(PopScale(fancySign.transform));
                Fx(hammerFrames, fancySign.transform.position + new Vector3(0.5f, 1.3f, 0f), 8f);
                Fx(s == 2 ? sparkFrames : dustFrames, fancySign.transform.position + new Vector3(0f, 0.9f, 0f), 10f, 0.2f);
            }
            SunodGame.Core.vc_Sfx.Play(s == 2 ? "step_done" : "hammer", 0.6f, 0.1f);
        }
        yield return BarStep(5.0f - elapsed, elapsed / craftTime, 5.0f / craftTime);
        elapsed = 5.0f;

        _frozenUntil = Time.time + 1.4f;
        Say(exclaimFrames, _passA, 0.9f);
        Say(exclaimFrames, _passB, 0.9f);
        Say(bubbleHeart, _shopper, 1.0f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f);
        float walk = 2.2f;
        Track(Walk(_shopper, BuyGround, walk, 0.6f));
        yield return BarStep(walk + 0.6f, elapsed / craftTime, (elapsed + walk + 0.6f) / craftTime);
        elapsed += walk + 0.6f;
        Pay(_shopper);
        yield return BarStep(Mathf.Max(0.05f, craftTime - elapsed), elapsed / craftTime, 1f);
        Finish("Your eye-catching sign worked. Sold!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Walker MakeTemp(Sprite[] look, float scale, Vector3 ground)
    {
        Walker w = MakeWalker(look, scale, "Customer");
        w.SetGround(ground);
        _temp.Add(w);
        return w;
    }

    private void Track(IEnumerator r) => _routes.Add(StartCoroutine(r));

    private void StopRoutes()
    {
        foreach (Coroutine r in _routes) if (r != null) StopCoroutine(r);
        _routes.Clear();
    }

    private IEnumerator Walk(Walker w, Vector3 to, float seconds, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Vector3 from = w.ground;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            w.SetGround(Vector3.Lerp(from, to, t / seconds));
            w.sr.sprite = w.look[(int)(t * 8f) % w.look.Length];
            yield return null;
        }
        w.SetGround(to);
        w.sr.sprite = w.look[0];
    }

    private void Say(Sprite[] bubble, Walker w, float seconds)
    {
        if (w == null || bubble == null || bubble.Length == 0) return;
        Fx(bubble, w.ground + new Vector3(0.5f, 1.5f * w.scale + 0.4f, 0f), bubble.Length / Mathf.Max(0.1f, seconds), 0.15f, 24);
    }

    private void Pay(Walker w)
    {
        Vector3 head = w.ground + new Vector3(0.2f, 1.5f * w.scale, 0f);
        Fx(coinFrames, head, 12f, 0.7f, 24);
        Fx(cookieIcon, head + new Vector3(-0.5f, 0.1f, 0f), 1.6f, 0.5f, 24);
        SunodGame.Core.vc_Sfx.Play("coin", 0.8f);
        StartCoroutine(Leave(w));
    }

    private IEnumerator Leave(Walker w)
    {
        yield return new WaitForSeconds(0.6f);
        Vector3 to = w.ground + new Vector3(-3.5f, -0.5f, 0f);
        yield return Walk(w, to, 2.2f, 0f);
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

    private IEnumerator PopScale(Transform tr)
    {
        float[] s = { 0.5f, 0.8f, 1.15f, 1f };
        foreach (float k in s)
        {
            if (tr == null) yield break;
            tr.localScale = Vector3.one * k;
            yield return new WaitForSeconds(0.06f);
        }
    }
}
