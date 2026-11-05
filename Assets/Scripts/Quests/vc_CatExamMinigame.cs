using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// I quest (I4), minigame version of vc_CatCureQuest ("New" gameplay, see vc_GameplayMode):
/// "What's wrong with Mimi?"
/// No skill pressing and no countdown. A close-up exam view opens:
///   1. Spot the symptoms - 3 random parts of the cat show a small sign (watery eye, red spot
///      in the ear, runny nose, upset tummy, swollen paw, twitchy tail). Tap them. Tapping a
///      healthy part costs points. Ends when all 3 are found or "I'm done checking".
///   2. Pick the medicine - 3 bottles with picture labels; the one whose label matches the
///      symptoms is right. One try.
/// Score (0..100): +20 per symptom found, -5 per wrong tap, +40 right medicine.
/// Stars (still 0..5, so the item score / model input is unchanged):
///   90+ = 5, 70+ = 4, 50+ = 3, 30+ = 2, else 1.
/// </summary>
[DisallowMultipleComponent]
public class vc_CatExamMinigame : vc_FitQuestBase
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
    [Tooltip("48x48 close-up cat for the exam view (Art/Generated/Quests/exam_cat.png).")]
    [SerializeField] private Sprite examCat;

    [Header("Effects")]
    [SerializeField] private Sprite[] ringFrames;
    [SerializeField] private Sprite[] checkFrames;
    [SerializeField] private Sprite[] bulbFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Minigame")]
    [Tooltip("Screen pixels per exam-cat pixel.")]
    [SerializeField] private int examScale = 7;

    private const int PtsSymptom = 20, PtsWrongTap = 5, PtsMedicine = 40;

    // Tap spots on the 48x48 exam cat (pixel coords, y down).
    private struct Spot
    {
        public string Id, Type;
        public Vector2 C;
        public float R;
        public Spot(string id, string type, float x, float y, float r) { Id = id; Type = type; C = new Vector2(x, y); R = r; }
    }

    private static readonly Spot[] Spots =
    {
        new Spot("eyeL", "eye", 18.5f, 15.5f, 3.2f), new Spot("eyeR", "eye", 28.5f, 15.5f, 3.2f),
        new Spot("earL", "ear", 15.5f, 7.5f, 4f),    new Spot("earR", "ear", 32.5f, 7.5f, 4f),
        new Spot("nose", "nose", 24f, 21f, 2.8f),
        new Spot("belly", "belly", 24f, 35f, 6f),
        new Spot("pawL", "paw", 18.5f, 43f, 4f),     new Spot("pawR", "paw", 29.5f, 43f, 4f),
        new Spot("tail", "tail", 42.5f, 32f, 4.5f),
    };

    private static readonly Dictionary<string, string> SymptomName = new Dictionary<string, string>
    {
        { "eye", "Watery eye" }, { "ear", "Red spot in the ear" }, { "nose", "Runny nose" },
        { "belly", "Upset tummy" }, { "paw", "Swollen paw" }, { "tail", "Twitchy tail" },
    };

    private static readonly Dictionary<string, string> LabelName = new Dictionary<string, string>
    {
        { "eye", "Eye" }, { "ear", "Ear" }, { "nose", "Nose" }, { "belly", "Tummy" }, { "paw", "Paw" }, { "tail", "Tail" },
    };

    private static readonly string[] Types = { "eye", "ear", "nose", "belly", "paw", "tail" };

    private vc_MinigameOverlay _ui;
    private float _idleClock;
    private Vector3 _catRest;
    private bool _cured;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    // minigame state
    private readonly List<Spot> _sick = new List<Spot>();
    private readonly HashSet<string> _found = new HashSet<string>();
    private int _wrongTaps;
    private readonly List<(VisualElement el, Spot spot)> _fx = new List<(VisualElement, Spot)>();
    private VisualElement _exam;

    protected override Vector3 BarWorldPosition => (cat != null ? cat.transform.position : transform.position) + new Vector3(-0.5f, 1.6f, 0f);

    private void Awake()
    {
        if (cat != null) _catRest = cat.transform.position;
        _ui = GetComponent<vc_MinigameOverlay>();
        if (_ui == null) _ui = gameObject.AddComponent<vc_MinigameOverlay>();
    }

    protected override void OnBegin()
    {
        CleanSpawned();
        _cured = false;
        if (cat != null) { cat.color = sickColor; cat.transform.position = _catRest; }
        if (pestle != null) pestle.gameObject.SetActive(true);

        // No countdown for this quest: the stars come from the minigame.
        if (vc_QuestTimer.Instance != null)
        {
            vc_QuestTimer.Instance.ExternalHold = true;
            vc_QuestTimer.Instance.SetStarsVisible(false);
        }

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Cat Medicine",
            "Mimi the clinic cat is sick. Find out what's wrong with her, then pick the right medicine.",
            new[] { "Examine Mimi", "Pick the right medicine" });

        StartWork(Flow());
    }

    protected override void OnSkill(vc_SkillData d) => Toast("Examine Mimi first - look closely at what's wrong.", 2f);

    protected override void OnWorkCancelled()
    {
        CleanSpawned();
        _ui?.Close();
        if (cat != null && !_cured) cat.transform.position = _catRest;
    }

    // ── Idle life: the sick cat shivers and sniffles ─────────────────────

    private void Update()
    {
        if (!Started || Done || cat == null || _cured) return;
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

    // ── The minigame ─────────────────────────────────────────────────────

    private IEnumerator Flow()
    {
        yield return new WaitForSeconds(0.6f);

        PickSymptoms();
        _found.Clear();
        _wrongTaps = 0;

        // Step 1: spot the symptoms
        _ui.Open("What's wrong with Mimi?");
        _ui.SetMessage("Tap what looks wrong. Tapping a healthy part costs points.", vc_MinigameOverlay.Ink);
        UpdateStatus();
        BuildExam();
        bool doneChecking = false;
        Button done = vc_MinigameOverlay.MakeButton("I'm done checking", 28, () => doneChecking = true);
        done.style.marginTop = 14;
        _ui.Content.Add(done);

        while (_found.Count < _sick.Count && !doneChecking)
        {
            AnimateFx();
            yield return null;
        }
        float hold = 0.8f;
        while (hold > 0f) { hold -= Time.deltaTime; AnimateFx(); yield return null; }

        // Step 2: pick the medicine
        int pickedWorld = -1;
        bool right = false;
        BuildBottles((w, ok) => { if (pickedWorld < 0) { pickedWorld = w; right = ok; } });
        while (pickedWorld < 0) yield return null;
        _ui.Close();

        // Score -> stars (0..5)
        int pts = Mathf.Clamp(_found.Count * PtsSymptom - _wrongTaps * PtsWrongTap + (right ? PtsMedicine : 0), 0, 100);
        int stars = pts >= 90 ? 5 : pts >= 70 ? 4 : pts >= 50 ? 3 : pts >= 30 ? 2 : 1;
        vc_QuestTimer.Instance?.SetStarOverride(stars);
        Debug.Log($"[Minigame] I4 cat: found {_found.Count}/3, wrong taps {_wrongTaps}, medicine {(right ? "right" : "wrong")} -> {pts} pts, {stars} stars");

        string summary = $"Found {_found.Count} of 3 symptoms";
        if (right)
            yield return Deliver(pickedWorld, 0.9f, $"{summary} - and the right medicine! Mimi feels better!");
        else
            yield return DeliverWrong(pickedWorld, $"{summary}, but that wasn't the right medicine. Dr. Santos will take it from here.");
    }

    private void PickSymptoms()
    {
        _sick.Clear();
        var types = new List<string>(Types);
        for (int i = 0; i < 3; i++)
        {
            string t = types[Random.Range(0, types.Count)];
            types.Remove(t);
            var options = new List<Spot>();
            foreach (Spot s in Spots) if (s.Type == t) options.Add(s);
            _sick.Add(options[Random.Range(0, options.Count)]);
        }
    }

    private void UpdateStatus() => _ui.SetStatus($"Symptoms found: {_found.Count} / {_sick.Count}");

    private void BuildExam()
    {
        int s = examScale;
        _exam = new VisualElement { pickingMode = PickingMode.Position };
        _exam.style.width = 48 * s;
        _exam.style.height = 48 * s;
        _exam.style.flexShrink = 0;
        _exam.style.backgroundColor = new Color(0.93f, 0.89f, 0.80f);
        if (examCat != null) _exam.style.backgroundImage = new StyleBackground(examCat);
        _exam.RegisterCallback<PointerDownEvent>(e => OnTap(e.localPosition));
        _ui.Content.Add(_exam);

        _fx.Clear();
        foreach (Spot spot in _sick) AddSymptomFx(spot);
    }

    private VisualElement Px(float x, float y, float w, float h, Color c)
    {
        int s = examScale;
        var el = new VisualElement { pickingMode = PickingMode.Ignore };
        el.style.position = Position.Absolute;
        el.style.left = x * s; el.style.top = y * s;
        el.style.width = w * s; el.style.height = h * s;
        el.style.backgroundColor = c;
        _exam.Add(el);
        return el;
    }

    private void AddSymptomFx(Spot spot)
    {
        Color tear = new Color(0.30f, 0.55f, 0.95f), snot = new Color(0.62f, 0.80f, 1f);
        Color red = new Color(0.90f, 0.20f, 0.20f), green = new Color(0.35f, 0.70f, 0.25f);
        switch (spot.Type)
        {
            case "eye":   _fx.Add((Px(spot.C.x, 18f, 1.5f, 2f, tear), spot)); break;
            case "nose":  _fx.Add((Px(23.5f, 22f, 1.5f, 2f, snot), spot)); break;
            case "ear":   _fx.Add((Px(spot.C.x - 1f, 6.5f, 2f, 2f, red), spot)); break;
            case "paw":   _fx.Add((Px(spot.C.x - 3f, 41f, 6f, 4f, new Color(red.r, red.g, red.b, 0.5f)), spot)); break;
            case "belly":
                for (int i = 0; i < 3; i++) _fx.Add((Px(21f + i * 3f, 34f + (i == 1 ? -1f : 0f), 1f, 1f, green), spot));
                break;
            case "tail":
                _fx.Add((Px(45.5f, 24f, 1f, 3f, new Color(0.4f, 0.4f, 0.4f)), spot));
                _fx.Add((Px(46.5f, 28f, 1f, 3f, new Color(0.4f, 0.4f, 0.4f)), spot));
                break;
        }
    }

    private void AnimateFx()
    {
        float t = Time.time;
        int s = examScale;
        foreach (var (el, spot) in _fx)
        {
            if (el == null) continue;
            switch (spot.Type)
            {
                case "eye":
                case "nose":
                {   // a drop that runs down and fades, again and again
                    float k = (t * 0.7f) % 1f;
                    float y0 = spot.Type == "eye" ? 18f : 22f;
                    el.style.top = (y0 + k * 6f) * s;
                    el.style.opacity = 1f - k;
                    break;
                }
                case "ear":
                case "paw":
                    el.style.opacity = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(t * 5f));
                    break;
                case "belly":
                    el.style.opacity = 0.3f + 0.7f * (0.5f + 0.5f * Mathf.Sin(t * 4f));
                    el.style.translate = new Translate(Mathf.Sin(t * 9f) > 0 ? s * 0.5f : 0f, 0f);
                    break;
                case "tail":
                    el.style.opacity = Mathf.Sin(t * 7f) > 0.2f ? 1f : 0f;
                    break;
            }
        }
    }

    private void OnTap(Vector2 local)
    {
        if (_found.Count >= _sick.Count) return;
        Vector2 p = local / examScale;

        Spot? hit = null;
        float best = float.MaxValue;
        foreach (Spot s in Spots)
        {
            float d = Vector2.Distance(p, s.C);
            if (d <= s.R * 1.25f && d < best) { best = d; hit = s; }
        }
        if (hit == null) return;   // tapped fur/background: no effect

        Spot spot = hit.Value;
        if (_found.Contains(spot.Id)) return;

        bool sick = false;
        foreach (Spot s in _sick) if (s.Id == spot.Id) sick = true;

        if (sick)
        {
            _found.Add(spot.Id);
            MarkFound(spot);
            _ui.SetMessage($"{SymptomName[spot.Type]}! +{PtsSymptom}", vc_MinigameOverlay.Good);
            SunodGame.Core.vc_Sfx.Play("step_done", 0.6f, 0.08f);
        }
        else
        {
            _wrongTaps++;
            _ui.SetMessage($"That looks healthy. -{PtsWrongTap}", vc_MinigameOverlay.Bad);
            SunodGame.Core.vc_Sfx.Play("buzz", 0.35f);
        }
        UpdateStatus();
    }

    private void MarkFound(Spot spot)
    {
        int s = examScale;
        float r = spot.R + 1f;
        var ring = new VisualElement { pickingMode = PickingMode.Ignore };
        ring.style.position = Position.Absolute;
        ring.style.left = (spot.C.x - r) * s; ring.style.top = (spot.C.y - r) * s;
        ring.style.width = r * 2f * s; ring.style.height = r * 2f * s;
        float bw = 4f;
        ring.style.borderLeftWidth = bw; ring.style.borderRightWidth = bw; ring.style.borderTopWidth = bw; ring.style.borderBottomWidth = bw;
        Color g = vc_MinigameOverlay.Good;
        ring.style.borderLeftColor = g; ring.style.borderRightColor = g; ring.style.borderTopColor = g; ring.style.borderBottomColor = g;
        float rad = r * s;
        ring.style.borderTopLeftRadius = rad; ring.style.borderTopRightRadius = rad;
        ring.style.borderBottomLeftRadius = rad; ring.style.borderBottomRightRadius = rad;
        _exam.Add(ring);
    }

    private void BuildBottles(System.Action<int, bool> picked)
    {
        _ui.SetTitle("Pick the medicine");
        _ui.SetMessage("Which label matches what you found? One try.", vc_MinigameOverlay.Ink);
        _ui.SetStatus("");
        _ui.Content.Clear();

        // The right label lists the cat's real symptoms; each decoy swaps one of them.
        var rightLabel = new List<string>();
        foreach (Spot s in _sick) rightLabel.Add(s.Type);
        List<string> Decoy()
        {
            var d = new List<string>(rightLabel);
            var pool = new List<string>();
            foreach (string t in Types) if (!d.Contains(t)) pool.Add(t);
            d[Random.Range(0, d.Count)] = pool[Random.Range(0, pool.Count)];
            return d;
        }
        List<string> d1 = Decoy(), d2 = Decoy();
        for (int guard = 0; guard < 10 && SameSet(d1, d2); guard++) d2 = Decoy();

        // World bottles: a random colour is right each time, so "always green" can't be learned.
        var world = new List<int>();
        for (int i = 0; i < (bottles != null ? bottles.Length : 0); i++) world.Add(i);
        Shuffle(world);
        while (world.Count < 3) world.Add(world.Count > 0 ? world[0] : 0);
        Shuffle(rightLabel); Shuffle(d1); Shuffle(d2);
        var cards = new List<(int world, List<string> label, bool ok)>
        {
            (world[0], rightLabel, true),
            (world[1], d1, false),
            (world[2], d2, false),
        };
        for (int i = cards.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (cards[i], cards[j]) = (cards[j], cards[i]); }

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        _ui.Content.Add(row);
        foreach (var c in cards)
        {
            var card = c;
            Button b = vc_MinigameOverlay.MakeButton("", 28, () => picked(card.world, card.ok));
            b.style.width = 230; b.style.minHeight = 260;
            b.style.marginLeft = 10; b.style.marginRight = 10;
            b.style.flexDirection = FlexDirection.Column;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;

            var img = new VisualElement { pickingMode = PickingMode.Ignore };
            img.style.width = 96; img.style.height = 96;
            if (bottles != null && card.world < bottles.Length && bottles[card.world] != null)
                img.style.backgroundImage = new StyleBackground(bottles[card.world].sprite);
            img.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            b.Add(img);

            foreach (string t in card.label)
            {
                var pill = vc_MinigameOverlay.MakeLabel(LabelName[t], 28);
                pill.style.backgroundColor = new Color(0.87f, 0.82f, 0.95f);
                pill.style.marginTop = 6;
                pill.style.paddingLeft = 12; pill.style.paddingRight = 12;
                pill.style.paddingTop = 2; pill.style.paddingBottom = 4;
                b.Add(pill);
            }
            row.Add(b);
        }
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }

    private static bool SameSet(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (string x in a) if (!b.Contains(x)) return false;
        return true;
    }

    // ── World payoff ─────────────────────────────────────────────────────

    private IEnumerator Fly(int bottle, float seconds)
    {
        Vector3 from = bottles[bottle].transform.position + new Vector3(0f, 0.7f, 0f);
        Vector3 to = cat.transform.position + new Vector3(0f, 0.4f, 0f);
        GameObject v = Spawn("Vial", bottles[bottle].sprite, 27);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            if (v != null) v.transform.position = Snap(Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 1.0f, 0f));
            yield return null;
        }
        Destroy(v);
    }

    private IEnumerator Deliver(int bottle, float seconds, string msg)
    {
        yield return Fly(bottle, seconds);
        yield return Cure(msg);
    }

    private IEnumerator DeliverWrong(int bottle, string msg)
    {
        yield return Fly(bottle, 0.9f);
        Vector3 c = cat.transform.position;
        Fx(new[] { crossMark }, c + new Vector3(0f, 1.0f, 0f), 2f, 0.2f, 27);
        Fx(new[] { bubbleSick }, c + new Vector3(0f, 1.1f, 0f), 1.4f, 0.15f, 26);
        SunodGame.Core.vc_Sfx.Play("buzz", 0.4f);
        yield return new WaitForSeconds(1.2f);
        Finish(msg);
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

    private void CleanSpawned()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
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
