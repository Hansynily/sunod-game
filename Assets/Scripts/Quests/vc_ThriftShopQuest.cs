using System.Collections;
using UnityEngine;

/// <summary>
/// E quest (E6). Run a shift at the thrift shop. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   command   E  best fit     manage the shift: racks sorted, shoppers buy, the till rings   ~2s
///   persuade  E  workaround   sell the vintage jacket                                        3s
///   charm     E  workaround   the shoppers warm up and buy                                   3s
///   map       E  workaround   rearrange the racks so shoppers flow through                   5s
///   paint     A  off-letter   style a window display on the mannequin; it draws people in    8s
///   collect   C  off-letter   fold and restock everything first, then sales come              8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (command) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_ThriftShopQuest : vc_FitQuestBase
{
    [Header("Shop")]
    [SerializeField] private SpriteRenderer[] racks;
    [SerializeField] private Sprite rackMessy;
    [SerializeField] private Sprite rackNeat;
    [Tooltip("Where each rack moves to on the map path (room-local offsets from its start).")]
    [SerializeField] private Vector3[] rackMoves = { new Vector3(-0.6f, 0.9f, 0f), new Vector3(0.6f, 0.9f, 0f) };
    [SerializeField] private SpriteRenderer mannequin;
    [SerializeField] private Sprite mannequinBare;
    [SerializeField] private Sprite mannequinStyled;
    [SerializeField] private SpriteRenderer till;

    [Header("Shoppers")]
    [SerializeField] private SpriteRenderer[] shoppers;
    [SerializeField] private Sprite[] shopperWalkA;
    [SerializeField] private Sprite[] shopperWalkB;
    [Tooltip("Where shoppers stand to pay (one spot per shopper).")]
    [SerializeField] private Transform[] paySpots;

    [Header("Effects")]
    [SerializeField] private Sprite[] coinFrames;
    [SerializeField] private Sprite[] splatFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite[] dustFrames;
    [SerializeField] private Sprite bag;
    [SerializeField] private Sprite jacket;
    [SerializeField] private Sprite pitchBubble;
    [SerializeField] private Sprite heartBubble;
    [SerializeField] private Sprite dotsBubble;

    [Header("Timing (seconds)")]
    [SerializeField] private float commandTime = 2f;
    [SerializeField] private float persuadeTime = 3f;
    [SerializeField] private float charmTime = 3f;
    [SerializeField] private float mapTime = 5f;
    [SerializeField] private float paintTime = 8f;
    [SerializeField] private float collectTime = 8f;
    [SerializeField] private float callTime = 4f;

    // Skill system v2 (QUEST_REDESIGN.md): Paint (prep) puts up a styled window display, then Map
    // rearranges the racks around it at once. Preference: Persuade (the vintage jacket).
    private static readonly vc_QuestSkillProfile Profile = new vc_QuestSkillProfile
    {
        npcName = "Ate Joan",
        presses = 4,
        bestFitTag = "command",
        bestFitSeconds = 2f,
        prepTag = "paint",
        prepUnlocksTag = "map",
        prepLine = "A styled window display goes up in the front...",
        preferredTag = "persuade",
        preferenceLine = "You sold the vintage jacket! Salamat, ha!",
        fastLines = new[]
        {
            ("persuade", "The vintage jacket is sold already!"),
            ("charm", "Everyone's buying, they love you!"),
            ("map", "The racks look so much better!"),
            ("summon", "The shop's packed! How?!"),
        },
    };

    protected override vc_QuestSkillProfile SkillProfile => Profile;

    private Vector3[] _rackRest, _shopperRest;
    private bool[] _sold;
    private float _idleClock, _dotsClock;

    protected override Vector3 BarWorldPosition => (till != null ? till.transform.position : transform.position) + new Vector3(-0.5f, 1.6f, 0f);

    private void Awake()
    {
        _rackRest = new Vector3[racks.Length];
        for (int i = 0; i < racks.Length; i++) _rackRest[i] = racks[i].transform.position;
        _shopperRest = new Vector3[shoppers.Length];
        for (int i = 0; i < shoppers.Length; i++) _shopperRest[i] = shoppers[i].transform.position;
        _sold = new bool[shoppers.Length];
    }

    protected override void OnBegin()
    {
        for (int i = 0; i < racks.Length; i++) { racks[i].transform.position = _rackRest[i]; racks[i].sprite = rackMessy; }
        for (int i = 0; i < shoppers.Length; i++) { shoppers[i].transform.position = _shopperRest[i]; shoppers[i].sprite = Walk(i)[0]; _sold[i] = false; }
        if (mannequin != null) mannequin.sprite = mannequinBare;
        _idleClock = 1f; _dotsClock = 2f;

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Thrift Shop",
            "Ate Joan: run a shift at the thrift shop for her. Someone keeps eyeing that vintage jacket, so a good sales pitch would make her day.",
            new[] { "Manage the shop" });
    }

    // ── Idle life: shoppers browse, unsure ───────────────────────────────

    private void Update()
    {
        if (!Started || Done || Working) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f)
        {
            _idleClock = Random.Range(1.4f, 2.4f);
            int i = Random.Range(0, shoppers.Length);
            if (!_sold[i]) StartCoroutine(Browse(i));
        }
        _dotsClock -= Time.deltaTime;
        if (_dotsClock <= 0f)
        {
            _dotsClock = Random.Range(2.4f, 3.4f);
            int i = Random.Range(0, shoppers.Length);
            if (!_sold[i] && dotsBubble != null) Fx(new[] { dotsBubble }, Top(i), 1.4f, 0.15f, 24);
        }
    }

    private IEnumerator Browse(int i)
    {
        Vector3 a = _shopperRest[i], b = a + new Vector3(i % 2 == 0 ? 0.3f : -0.3f, 0f, 0f);
        yield return Move(i, a, b, 0.4f);
        yield return new WaitForSeconds(0.4f);
        if (Working || Done) yield break;
        yield return Move(i, b, a, 0.4f);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("persuade")) { StartWork(PersuadeRoutine(PathTime(persuadeTime))); return; }
        if (d.HasTag("charm")) { StartWork(CharmRoutine(PathTime(charmTime))); return; }
        if (d.HasTag("map")) { StartWork(MapRoutine(PathTime(mapTime), CurrentInstant)); return; }
        if (d.HasTag("summon")) { StartWork(CallRoutine(PathTime(callTime))); return; }
        if (d.HasTag("paint")) { StartWork(PaintRoutine(PathTime(paintTime))); return; }
        if (d.HasTag("collect")) { StartWork(CollectRoutine(PathTime(collectTime))); return; }
        Toast("The shop needs a manager.", 2.2f);
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < shoppers.Length; i++)
            if (!_sold[i]) { shoppers[i].transform.position = _shopperRest[i]; shoppers[i].sprite = Walk(i)[0]; }
    }

    private IEnumerator CommandRoutine()
    {
        Toast("You take charge of the shift!", commandTime);
        ShowBar(true);
        NeatenRacks();
        yield return BarStep(0.4f, 0f, 0.4f / commandTime);
        yield return SellAll(0.4f, commandTime);
        Finish("Racks sorted, sales rung up. Great shift!");
    }

    private IEnumerator PersuadeRoutine(float persuadeTime)
    {
        float lead = 1.3f * persuadeTime / 3f;
        Toast("You pitch the vintage jacket...", persuadeTime);
        ShowBar(true);
        if (pitchBubble != null) Fx(new[] { pitchBubble }, Top(0) + new Vector3(-0.6f, 0f, 0f), 1f, 0.1f, 24);
        if (jacket != null) Fx(new[] { jacket }, Top(0) + new Vector3(0.1f, -0.3f, 0f), 0.8f, 0.2f, 25);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.1f);
        yield return BarStep(lead, 0f, lead / persuadeTime);
        yield return SellAll(lead, persuadeTime);
        Finish("Sold the vintage jacket - and more!");
    }

    private IEnumerator CharmRoutine(float charmTime)
    {
        float lead = 1.2f * charmTime / 3f;
        Toast("You greet the shoppers with a big smile...", charmTime);
        ShowBar(true);
        for (int i = 0; i < shoppers.Length; i++)
        {
            if (heartBubble != null) Fx(new[] { heartBubble }, Top(i), 1.2f, 0.25f, 24);
            StartCoroutine(Hop(i));
        }
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        yield return BarStep(lead, 0f, lead / charmTime);
        yield return SellAll(lead, charmTime);
        Finish("The shoppers loved you. Everyone bought something!");
    }

    private IEnumerator MapRoutine(float mapTime, bool oneGo)
    {
        Toast(oneGo ? "The racks rearrange around the new window display!" : "You rearrange the racks for better flow...", mapTime);
        ShowBar(true);
        float slide = 2.6f * mapTime / 5f, t = 0f;
        while (t < slide)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / slide);
            for (int i = 0; i < racks.Length; i++)
                racks[i].transform.position = Snap(_rackRest[i] + (i < rackMoves.Length ? rackMoves[i] : Vector3.zero) * k);
            if (Mathf.Repeat(t, 0.45f) < Time.deltaTime) SunodGame.Core.vc_Sfx.Play("scrape", 0.4f, 0.1f);
            SetBar(t / mapTime);
            yield return null;
        }
        NeatenRacks();
        yield return SellAll(slide, mapTime);
        Finish(oneGo ? "The racks fell into place around the display in one go!" : "The new layout works. Shoppers flow right to the till!");
    }

    // ── Call: "Sale today!" at the door, shoppers stream in ──────────────

    private IEnumerator CallRoutine(float callTime)
    {
        Toast("You call out at the door: \"Sale today!\"", Mathf.Min(2.5f, callTime));
        ShowBar(true);
        Vector3 doorPos = (till != null ? till.transform.position : transform.position) + new Vector3(-1.8f, -0.8f, 0f);
        if (pitchBubble != null) Fx(new[] { pitchBubble }, doorPos + new Vector3(0f, 1.6f, 0f), 1f, 0.15f, 25);
        SunodGame.Core.vc_Sfx.Play("bell", 0.6f);
        SunodGame.Core.vc_Sfx.Play("text_blip", 0.6f, 0.1f);
        for (int i = 0; i < shoppers.Length; i++)
        {
            if (!_sold[i] && heartBubble != null) Fx(new[] { heartBubble }, Top(i), 1.2f, 0.25f, 24);
            StartCoroutine(Hop(i));
        }
        // a passer-by drawn in from the street walks in with the crowd
        float comeSecs = callTime * 0.4f;
        Vector3 from = doorPos + new Vector3(-4.5f, 0f, 0f);
        SpawnHelper(from, 10);
        yield return HelperWalk(from, doorPos, comeSecs, tt => SetBar(tt / callTime));
        yield return SellAll(comeSecs, callTime);
        Finish("\"Sale today!\" The shoppers streamed in and bought up a storm!");
    }

    // ── Prep (Paint): a styled window display goes up ────────────────────

    protected override IEnumerator OnPrep(vc_SkillData prepSkill)
    {
        Toast(Profile.prepLine, 1.4f);
        Vector3 m = mannequin != null ? mannequin.bounds.center : transform.position;
        for (int i = 0; i < 4; i++)
        {
            Fx(splatFrames, m + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.4f, 0.4f), 0f), 10f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("brush", 0.4f, 0.1f);
            yield return new WaitForSeconds(0.2f);
        }
        if (mannequin != null) mannequin.sprite = mannequinStyled;
        Fx(sparkFrames, m + new Vector3(0.3f, 0.5f, 0f), 12f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        for (int i = 0; i < shoppers.Length; i++)
            if (!_sold[i] && heartBubble != null) Fx(new[] { heartBubble }, Top(i), 1.2f, 0.2f, 24);
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator PaintRoutine(float paintTime)
    {
        float sc = paintTime / 8f;
        float step = Mathf.Max(0.15f, 0.5f * sc), after = 1f * sc;
        Toast("You style a window display...", paintTime);
        ShowBar(true);
        float style = 4.5f * sc, t = 0f;
        Vector3 m = mannequin != null ? mannequin.bounds.center : transform.position;
        while (t < style)
        {
            Fx(splatFrames, m + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.4f, 0.4f), 0f), 10f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("brush", 0.4f, 0.1f);
            yield return BarStep(step, t / paintTime, (t + step) / paintTime);
            t += step;
        }
        if (mannequin != null) mannequin.sprite = mannequinStyled;
        Fx(sparkFrames, m + new Vector3(0.3f, 0.5f, 0f), 12f, 0.2f);
        SunodGame.Core.vc_Sfx.Play("chime", 0.5f);
        for (int i = 0; i < shoppers.Length; i++) if (dotsBubble != null && heartBubble != null) Fx(new[] { heartBubble }, Top(i), 1.5f, 0.2f, 24);
        yield return BarStep(after, t / paintTime, (t + after) / paintTime);
        t += after;
        yield return SellAll(t, paintTime);
        Finish("The window display drew them in!");
    }

    private IEnumerator CollectRoutine(float collectTime)
    {
        float sc = collectTime / 8f;
        float step = Mathf.Max(0.15f, 0.5f * sc);
        Toast("You fold and restock everything first...", collectTime);
        ShowBar(true);
        float fold = 5f * sc, t = 0f;
        while (t < fold)
        {
            int r = t < fold * 0.5f ? 0 : Mathf.Min(1, racks.Length - 1);
            if (racks.Length > 0) Fx(dustFrames, racks[r].transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0.2f, 0f), 10f, 0.15f);
            SunodGame.Core.vc_Sfx.Play("shuffle", 0.4f, 0.1f);
            yield return BarStep(step, t / collectTime, (t + step) / collectTime);
            t += step;
            if (Mathf.Abs(t - fold * 0.5f) < step * 0.52f && racks.Length > 0) { racks[0].sprite = rackNeat; Fx(sparkFrames, racks[0].bounds.center, 12f, 0.15f); }
        }
        NeatenRacks();
        yield return SellAll(t, collectTime);
        Finish("Neat racks, happy shoppers. Sales came in!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void NeatenRacks()
    {
        foreach (var r in racks)
        {
            if (r.sprite == rackNeat) continue;
            r.sprite = rackNeat;
            Fx(sparkFrames, r.bounds.center + new Vector3(0f, 0.3f, 0f), 12f, 0.15f);
        }
        SunodGame.Core.vc_Sfx.Play("shuffle", 0.5f);
    }

    /// <summary>Every shopper walks to the till and buys, filling the bar from t0 to total.</summary>
    private IEnumerator SellAll(float t0, float total)
    {
        int n = shoppers.Length;
        float per = (total - t0) / Mathf.Max(1, n), t = t0;
        for (int i = 0; i < n; i++)
        {
            Vector3 to = paySpots != null && i < paySpots.Length && paySpots[i] != null ? paySpots[i].position : _shopperRest[i];
            float walk = Mathf.Min(per * 0.6f, 0.8f);
            StartCoroutine(Move(i, shoppers[i].transform.position, to, walk));
            yield return BarStep(walk, t / total, (t + walk) / total);
            Sale(i);
            yield return BarStep(per - walk, (t + walk) / total, (t + per) / total);
            t += per;
        }
    }

    private void Sale(int i)
    {
        _sold[i] = true;
        if (till != null) Fx(coinFrames, till.bounds.center + new Vector3(0f, 0.4f, 0f), 12f, 0.4f, 24);
        if (bag != null) Fx(new[] { bag }, Top(i) + new Vector3(0f, -0.3f, 0f), 1.4f, 0.2f, 24);
        SunodGame.Core.vc_Sfx.Play("coin", 0.6f, 0.05f);
        SunodGame.Core.vc_Sfx.Play("bell", 0.4f);
        StartCoroutine(Hop(i));
    }

    private Sprite[] Walk(int i) => i % 2 == 0 ? shopperWalkA : shopperWalkB;

    private IEnumerator Move(int i, Vector3 from, Vector3 to, float seconds)
    {
        Sprite[] f = Walk(i);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            shoppers[i].transform.position = Snap(Vector3.Lerp(from, to, t / seconds));
            if (f != null && f.Length > 0) shoppers[i].sprite = f[(int)(t * 8f) % f.Length];
            yield return null;
        }
        shoppers[i].transform.position = to;
        if (f != null && f.Length > 0) shoppers[i].sprite = f[0];
    }

    private Vector3 Top(int i)
    {
        Bounds b = shoppers[i].bounds;
        return new Vector3(b.center.x, b.max.y + 0.2f, 0f);
    }

    private IEnumerator Hop(int i)
    {
        Transform tr = shoppers[i].transform;
        Vector3 p = tr.position;
        tr.position = p + new Vector3(0f, 2f / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (tr != null) tr.position = p;
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
