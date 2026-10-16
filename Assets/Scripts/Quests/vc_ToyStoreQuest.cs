using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// E quest (E8). Keep the toy store running. Any APPROPRIATE skill gets the shop humming - how
/// well it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   command  E  best fit     direct the staff: restock, ring up the register; a sale follows   ~2s
///   persuade E  workaround   sell the biggest teddy bear to the browsing shopper               3s
///   charm    E  workaround   a kid runs to the toy train; big sale                             3s
///   summon   E  workaround   call shoppers in, one per press; the third makes a rush           x3
///   map      E  workaround   rearrange the shelves so shoppers flow through; a sale            5s
///   direct   C  off-letter   tidy the messy stock onto the shelf first, then sales come       7s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (command) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_ToyStoreQuest : vc_FitQuestBase
{
    [Header("Store")]
    [SerializeField] private Transform toyShelf;
    [SerializeField] private Transform cashBox;
    [SerializeField] private Transform buySpot;
    [SerializeField] private Transform trainSpot;
    [SerializeField] private SpriteRenderer bigTeddy;
    [Tooltip("Messy stock boxes on the floor (tidied by Direct).")]
    [SerializeField] private Transform[] messItems;
    [SerializeField] private Vector3 shelfMoveOffset = new Vector3(-2.2f, 0f, 0f);

    [Header("People (town NPC walk frames, 6 each)")]
    [SerializeField] private Sprite[] lookShopper;
    [SerializeField] private Sprite[] lookKid;
    [SerializeField] private Sprite[] lookClerk;
    [SerializeField] private Sprite[] lookStaff;
    [SerializeField] private Sprite[] lookCallerA;
    [SerializeField] private Sprite[] lookCallerB;
    [SerializeField] private Sprite[] lookCallerC;
    [SerializeField] private Vector2 shopperRest = new Vector2(-2.9f, -0.4f);
    [SerializeField] private Vector2 clerkSpot = new Vector2(1.9f, 0.55f);

    [Header("Effects")]
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] exclaimFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] bubbleSmile;
    [SerializeField] private Sprite[] bubblePitch;
    [SerializeField] private Sprite[] bubbleShout;
    [SerializeField] private Sprite[] bubbleHeart;
    [SerializeField] private Sprite[] toyIcons;
    [SerializeField] private Sprite[] saleTag;
    [SerializeField] private Sprite[] teddyIcon;
    [SerializeField] private Sprite[] footprintFx;
    [SerializeField] private Sprite carryBox;

    [Header("Timing (seconds)")]
    [SerializeField] private float commandTime = 2f;
    [SerializeField] private float persuadeTime = 3f;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private int callPresses = 3;
    [SerializeField] private float mapTime = 5f;
    [SerializeField] private float directTime = 7f;

    private class Walker
    {
        public Transform tr;
        public SpriteRenderer sr;
        public Sprite[] look;
        public float scale = 1f;
        public Vector3 ground;
        public float footOffset;
        public bool moving;
        public void SetGround(Vector3 g)
        {
            ground = g;
            tr.position = new Vector3(Mathf.Round(g.x * 32f) / 32f, Mathf.Round((g.y + footOffset) * 32f) / 32f, g.z);
        }
    }

    private Walker _shopper, _clerk;
    private readonly List<Walker> _temp = new List<Walker>();
    private readonly List<Coroutine> _routes = new List<Coroutine>();
    private Vector3 _shelfRest, _teddyRest;
    private Vector3[] _messRest;
    private bool _shelfMoved, _teddySold;
    private int _callCount;
    private Vector3 _barPos;
    private readonly Sprite[][] _callerLooks = new Sprite[3][];

    protected override Vector3 BarWorldPosition => _barPos;

    private Vector3 BuyGround => new Vector3(buySpot.position.x, buySpot.position.y, 0f);
    private Vector3 TeddyGround => new Vector3(bigTeddy.transform.position.x + 1.0f, bigTeddy.transform.position.y + 0.05f, 0f);

    private void Awake()
    {
        _shelfRest = toyShelf.position;
        if (bigTeddy != null) _teddyRest = bigTeddy.transform.position;
        _messRest = new Vector3[messItems.Length];
        for (int i = 0; i < messItems.Length; i++) _messRest[i] = messItems[i].position;
    }

    protected override void OnBegin()
    {
        StopRoutes();
        ClearTemp();
        if (_shopper == null) _shopper = MakeWalker(lookShopper, 1f, "Shopper");
        if (_clerk == null) _clerk = MakeWalker(lookClerk, 1f, "Clerk");
        _callerLooks[0] = lookCallerA; _callerLooks[1] = lookCallerB; _callerLooks[2] = lookCallerC;

        toyShelf.position = _shelfRest;
        _shelfMoved = false;
        _teddySold = false;
        _callCount = 0;
        if (bigTeddy != null) { bigTeddy.gameObject.SetActive(true); bigTeddy.transform.position = _teddyRest; }
        for (int i = 0; i < messItems.Length; i++) { messItems[i].gameObject.SetActive(true); messItems[i].position = _messRest[i]; }

        _shopper.SetGround(new Vector3(shopperRest.x, shopperRest.y, 0f));
        _shopper.sr.sprite = _shopper.look[0];
        _clerk.SetGround(new Vector3(clerkSpot.x, clerkSpot.y, 0f));
        _clerk.sr.sprite = _clerk.look[0];
        _barPos = cashBox.position + new Vector3(-1.2f, 2.6f, 0f);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Toy Store Rush",
            "The toy store is getting busy. Help keep it running.",
            new[] { "Help run the store" });
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

    // ── Idle life: the clerk and the browsing shopper shift about ────────

    private void Update()
    {
        if (!Started || Done) return;
        float phase = Time.time % 1.6f;
        if (_clerk != null && !Working) Bob(_clerk, phase < 0.18f);
        if (_shopper != null && !Working && !_shopper.moving) Bob(_shopper, (Time.time + 0.7f) % 1.9f < 0.18f);
    }

    private static void Bob(Walker w, bool up)
    {
        w.tr.position = new Vector3(w.tr.position.x, Mathf.Round((w.ground.y + w.footOffset + (up ? 1f / 32f : 0f)) * 32f) / 32f, 0f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine()); return; }
        if (d.HasTag("charm")) { StartWork(CharmRoutine()); return; }
        if (d.HasTag("summon")) { CallPress(); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine()); return; }
        if (d.HasTag("direct")) { StartWork(DirectRoutine()); return; }
        Toast("The store needs someone to run it.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        if (_shopper == null) return;
        StopRoutes();
        ClearTemp();
        if (_shelfMoved) PlaceShelf(0f);
        _shelfMoved = false;
        _callCount = 0;
        if (bigTeddy != null) { bigTeddy.gameObject.SetActive(true); _teddySold = false; }
        for (int i = 0; i < messItems.Length; i++) { messItems[i].gameObject.SetActive(true); messItems[i].position = _messRest[i]; }
        _shopper.SetGround(new Vector3(shopperRest.x, shopperRest.y, 0f));
        _shopper.sr.sprite = _shopper.look[0];
        _clerk.SetGround(new Vector3(clerkSpot.x, clerkSpot.y, 0f));
    }

    // ── Command: the staff restock, the register rings, a sale follows ───

    private IEnumerator CommandRoutine()
    {
        Toast("You give the orders. The store hums...", commandTime);
        ShowBar(true);
        Say(bubbleShout, _clerk, 0.9f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f);

        Vector3 front = toyShelf.position + new Vector3(-0.5f, -0.75f, 0f);
        Walker staff = MakeTemp(lookStaff, 1f, front + new Vector3(-4.5f, -0.2f, 0f));
        Carry(staff, carryBox);
        Track(Walk(staff, front, commandTime * 0.5f, 0.1f));
        Track(Walk(_shopper, BuyGround, commandTime * 0.45f, commandTime * 0.3f));

        yield return BarStep(commandTime * 0.4f, 0f, 0.4f);
        SunodGame.Core.vc_Sfx.Play("coin", 0.6f);
        Fx(coinFrames, cashBox.position + new Vector3(0f, 0.8f, 0f), 12f, 0.5f, 24);
        yield return BarStep(commandTime * 0.2f, 0.4f, 0.6f);
        Restock();
        yield return BarStep(commandTime * 0.25f, 0.6f, 0.85f);
        Pay(_shopper, true);
        yield return BarStep(commandTime * 0.15f, 0.85f, 1f);
        Finish("You ran the store like a pro. Sold!");
    }

    private void Restock()
    {
        Vector3 top = toyShelf.position + new Vector3(0f, 1.2f, 0f);
        for (int i = 0; i < 3 && toyIcons != null; i++)
            Fx(new[] { toyIcons[i % toyIcons.Length] }, top + new Vector3(-0.5f + i * 0.5f, 0f, 0f), 1.6f, 0.45f, 24);
        Fx(sparkFrames, top + new Vector3(0f, -0.2f, 0f), 10f, 0.2f, 24);
        Fx(saleTag, top + new Vector3(0.9f, 0.1f, 0f), 1.6f, 0.4f, 24);
        SunodGame.Core.vc_Sfx.Play("plop", 0.6f, 0.1f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.4f);
    }

    // ── Persuade: sell the biggest teddy bear ────────────────────────────

    private IEnumerator PersuadeRoutine()
    {
        Toast("You pitch the biggest teddy bear...", persuadeTime);
        ShowBar(true);
        Say(bubblePitch, _shopper, 1.0f);
        Track(Walk(_shopper, TeddyGround, persuadeTime * 0.35f, persuadeTime * 0.12f));
        yield return BarStep(persuadeTime * 0.55f, 0f, 0.55f);

        Say(bubbleHeart, _shopper, 0.9f);
        SunodGame.Core.vc_Sfx.Play("cheer", 0.35f);
        if (bigTeddy != null) { _teddySold = true; Fx(sparkFrames, bigTeddy.transform.position + new Vector3(0f, 0.8f, 0f), 10f, 0.2f, 24); bigTeddy.gameObject.SetActive(false); }
        Carry(_shopper, teddyIcon != null && teddyIcon.Length > 0 ? teddyIcon[0] : null);
        Track(Walk(_shopper, BuyGround, persuadeTime * 0.25f, 0.1f));
        yield return BarStep(persuadeTime * 0.3f, 0.55f, 0.85f);
        Pay(_shopper, false);
        yield return BarStep(persuadeTime * 0.15f, 0.85f, 1f);
        Finish("You sold the biggest teddy bear in the store!");
    }

    // ── Charm: a kid runs to the toy train ───────────────────────────────

    private IEnumerator CharmRoutine()
    {
        Toast("You flash a friendly smile at a kid...", charmTime);
        ShowBar(true);
        Fx(bubbleSmile, BuyGround + new Vector3(-0.6f, 2.4f, 0f), 1.5f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f);
        Walker kid = MakeTemp(lookKid, 0.8f, new Vector3(-6.5f, trainSpot.position.y - 0.2f, 0f));
        Vector3 trainGround = new Vector3(trainSpot.position.x - 0.9f, trainSpot.position.y - 0.1f, 0f);
        Track(Walk(kid, trainGround, charmTime * 0.38f, 0.1f, 14f));
        yield return BarStep(charmTime * 0.45f, 0f, 0.45f);

        Say(bubbleHeart, kid, 0.9f);
        SunodGame.Core.vc_Sfx.Play("cheer", 0.35f);
        Fx(toyIcons != null && toyIcons.Length > 0 ? new[] { toyIcons[0] } : null, kid.ground + new Vector3(0.3f, 1.4f, 0f), 1.6f, 0.4f, 24);
        Track(Walk(kid, BuyGround + new Vector3(-0.5f, 0f, 0f), charmTime * 0.3f, 0.15f, 14f));
        yield return BarStep(charmTime * 0.42f, 0.45f, 0.87f);
        Pay(kid, true);
        StartCoroutine(DelayedCoin(kid, 0.25f));
        yield return BarStep(charmTime * 0.13f, 0.87f, 1f);
        Finish("The kid loved the train. Big sale!");
    }

    private IEnumerator DelayedCoin(Walker w, float delay)
    {
        yield return new WaitForSeconds(delay);
        Fx(coinFrames, w.ground + new Vector3(-0.2f, 1.2f * w.scale, 0f), 12f, 0.7f, 24);
        SunodGame.Core.vc_Sfx.Play("coin", 0.6f, 0.1f);
    }

    // ── Call: shoppers come in one per press ─────────────────────────────

    private void CallPress()
    {
        _callCount++;
        int idx = (_callCount - 1) % 3;
        Say(bubbleShout, _clerk, 0.8f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.15f);
        SunodGame.Core.vc_Sfx.Play("door_open", 0.4f);

        Vector3 spot = BuyGround + new Vector3(-0.9f * idx - 0.3f, 0.2f * (idx % 2), 0f);
        Walker w = MakeTemp(_callerLooks[idx], 1f, new Vector3(-7.2f, spot.y, 0f));
        Track(Walk(w, spot, 2.0f, 0f));

        if (_callCount < callPresses)
            Toast($"A shopper comes in! ({_callCount}/{callPresses})", 2f);
        else
            StartWork(CallRushRoutine());
    }

    private IEnumerator CallRushRoutine()
    {
        Toast("A rush of shoppers fills the store!", 3f);
        ShowBar(true);
        float t = 0f;
        // wait for every caller to reach the counter
        while (true)
        {
            bool anyMoving = false;
            foreach (Walker w in _temp) if (w.moving) anyMoving = true;
            if (!anyMoving) break;
            t += Time.deltaTime;
            SetBar(Mathf.Min(0.7f, t / 2.6f));
            yield return null;
        }
        yield return BarStep(0.3f, 0.7f, 0.85f);
        var buyers = new List<Walker>(_temp);
        foreach (Walker w in buyers)
        {
            Pay(w, false);
            yield return new WaitForSeconds(0.22f);
        }
        SunodGame.Core.vc_Sfx.Play("cheer", 0.5f);
        yield return BarStep(0.25f, 0.85f, 1f);
        Finish("You called them in. What a rush of sales!");
    }

    // ── Map: rearrange the shelf so shoppers flow through ────────────────

    private IEnumerator MapRoutine()
    {
        Toast("You rearrange the shelves for a better flow...", mapTime);
        ShowBar(true);
        _shelfMoved = true;
        float moveTime = mapTime * 0.5f, t = 0f, scrape = 0f;
        while (t < moveTime)
        {
            t += Time.deltaTime; scrape += Time.deltaTime;
            PlaceShelf(Mathf.SmoothStep(0f, 1f, t / moveTime));
            SetBar(t / mapTime);
            if (scrape >= 0.45f)
            {
                scrape = 0f;
                SunodGame.Core.vc_Sfx.Play("scrape", 0.4f, 0.1f);
                Fx(dustFrames, toyShelf.position + new Vector3(0.7f, -0.7f, 0f), 8f, 0.2f);
            }
            yield return null;
        }
        PlaceShelf(1f);

        // footprints show the new walkway to the counter, then a shopper strolls it
        float lane = BuyGround.y;
        for (int i = 0; i < 5; i++)
        {
            Fx(footprintFx, new Vector3(-3.8f + i * 1.45f, lane + (i % 2 == 0 ? 0.1f : -0.1f), 0f), 1.0f, 0f, 3);
            yield return BarStep(mapTime * 0.04f, (moveTime + i * mapTime * 0.04f) / mapTime, (moveTime + (i + 1) * mapTime * 0.04f) / mapTime);
        }
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.5f);
        float walk = mapTime * 0.22f;
        Track(Walk(_shopper, BuyGround, walk, 0f));
        float used = moveTime + mapTime * 0.2f;
        yield return BarStep(walk, used / mapTime, (used + walk) / mapTime);
        Pay(_shopper, true);
        yield return BarStep(Mathf.Max(0.05f, mapTime - used - walk), (used + walk) / mapTime, 1f);
        Finish("Shoppers flow past every shelf. Sold!");
    }

    private void PlaceShelf(float k) => toyShelf.position = Snap(_shelfRest + shelfMoveOffset * k);

    // ── Direct: tidy the messy stock, then sales come ────────────────────

    private IEnumerator DirectRoutine()
    {
        Toast("You sort the messy stock onto the shelf...", directTime);
        ShowBar(true);
        float elapsed = 0f;
        int n = messItems.Length;
        float per = (directTime - 2.1f) / Mathf.Max(1, n);
        for (int i = 0; i < n; i++)
        {
            Transform item = messItems[i];
            Vector3 from = item.position;
            Vector3 to = toyShelf.position + new Vector3(-0.6f + (i % 4) * 0.4f, 0.9f, 0f);
            float t = 0f, hop = per * 0.8f;
            while (t < hop)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / hop);
                item.position = Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.7f, 0f);
                SetBar((elapsed + t) / directTime);
                yield return null;
            }
            item.gameObject.SetActive(false);
            Fx(checkFrames, to + new Vector3(0f, 0.4f, 0f), 2.5f, 0.3f, 24);
            Fx(sparkFrames, to, 10f, 0.15f, 24);
            SunodGame.Core.vc_Sfx.Play("plop", 0.5f, 0.12f);
            yield return BarStep(per * 0.2f, (elapsed + hop) / directTime, (elapsed + per) / directTime);
            elapsed += per;
        }

        Restock();
        Say(exclaimFrames, _shopper, 0.8f);
        yield return BarStep(0.5f, elapsed / directTime, (elapsed + 0.5f) / directTime);
        elapsed += 0.5f;
        Track(Walk(_shopper, BuyGround, 1.2f, 0f));
        yield return BarStep(1.2f, elapsed / directTime, (elapsed + 1.2f) / directTime);
        elapsed += 1.2f;
        Pay(_shopper, true);
        yield return BarStep(Mathf.Max(0.05f, directTime - elapsed), elapsed / directTime, 1f);
        Finish("A tidy store sells itself. Sold!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Walker MakeTemp(Sprite[] look, float scale, Vector3 ground)
    {
        Walker w = MakeWalker(look, scale, "Person");
        w.SetGround(ground);
        _temp.Add(w);
        return w;
    }

    private void ClearTemp()
    {
        foreach (Walker w in _temp) if (w.tr != null) Destroy(w.tr.gameObject);
        _temp.Clear();
    }

    private void Track(IEnumerator r) => _routes.Add(StartCoroutine(r));

    private void StopRoutes()
    {
        foreach (Coroutine r in _routes) if (r != null) StopCoroutine(r);
        _routes.Clear();
        if (_shopper != null) _shopper.moving = false;
    }

    private IEnumerator Walk(Walker w, Vector3 to, float seconds, float delay, float fps = 8f)
    {
        w.moving = true;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Vector3 from = w.ground;
        w.sr.flipX = to.x < from.x;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            w.SetGround(Vector3.Lerp(from, to, t / seconds));
            w.sr.sprite = w.look[(int)(t * fps) % w.look.Length];
            yield return null;
        }
        w.SetGround(to);
        w.sr.sprite = w.look[0];
        w.sr.flipX = false;
        w.moving = false;
    }

    private void Say(Sprite[] bubble, Walker w, float seconds)
    {
        if (w == null || bubble == null || bubble.Length == 0) return;
        Fx(bubble, w.ground + new Vector3(0.5f, 1.5f * w.scale + 0.4f, 0f), bubble.Length / Mathf.Max(0.1f, seconds), 0.15f, 24);
    }

    private void Carry(Walker w, Sprite item)
    {
        if (w == null || item == null) return;
        var go = new GameObject("Carry");
        go.transform.SetParent(w.tr, false);
        go.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = item; sr.sortingLayerName = sortingLayer; sr.sortingOrder = 14;
    }

    private void Pay(Walker w, bool ring)
    {
        Vector3 head = w.ground + new Vector3(0.2f, 1.5f * w.scale, 0f);
        Fx(coinFrames, head, 12f, 0.7f, 24);
        Fx(saleTag, head + new Vector3(-0.5f, 0.1f, 0f), 1.6f, 0.5f, 24);
        SunodGame.Core.vc_Sfx.Play("coin", 0.8f, 0.05f);
        if (ring && cashBox != null) Fx(sparkFrames, cashBox.position + new Vector3(0f, 0.5f, 0f), 10f, 0.2f, 24);
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
}
