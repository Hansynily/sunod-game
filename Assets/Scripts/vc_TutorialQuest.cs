using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial quest - "Tala's Kite". Tala's kite is stuck in a tree and she won't leave the
/// path until she gets it back. ANY skill solves it, but every skill does it in its own
/// way, matched to what that skill actually does (keyed off its capability tag, with the
/// RIASEC letter as a fallback for skills added later):
///
///   build    - a ladder appears under the tree, you climb and bring the kite down
///   repair   - the snapped string is tied back (string grows to the kite), Tala reels it in
///   push     - you shake the tree until the kite drops
///   plan     - you wait for the right gust; the kite floats down
///   unlock   - the string appears, the knot wiggles loose, the kite comes in on it
///   survey   - a sparkle searches the tree, finds the snag; Tala tugs it the right way
///   craft    - a hook pole reaches up, hooks the kite and carries it down
///   paint    - the string is painted red so Tala can see it; she pulls the kite in
///   barrier  - you shape a soft cushion; the kite drops onto it
///   teach    - you show Tala gentle tugs; she does three, the kite jerks, then comes free
///   guide    - Tala climbs to the low branch step by step, gets it, climbs down
///   heal     - you patch Tala's knee (heart); feeling brave, she climbs up for it
///   persuade - you convince the janitor to lend his tall broom
///   map      - you read the wind; a gust lifts the kite free
///   charm    - your friendliness brings the janitor over to help
///   summon   - you call the janitor over to reach it
///   command  - you give the janitor clear steps; he follows them and gets the kite
///   direct   - arrows mark the route; the kite slides along them to Tala
///   collect  - you pull the kite straight down to you
///   inspect  - a closer look: it's only looped on a twig; one flick frees it
///
/// Rule for every reaction: the player must SEE the skill cause the result (a tool, a
/// person, a string being pulled), not just a message and a kite flying down.
/// When the kite is back, Tala cheers and steps off the path.
/// Tutorial-only - standalone, does NOT use vc_QuestRoom / vc_IQuestLogic.
/// </summary>
[DisallowMultipleComponent]
public class vc_TutorialQuest : MonoBehaviour
{
    [Header("Tala (blocks the path until she has her kite)")]
    [Tooltip("The kid standing on the path. Kept as 'obstacleBlocker' so the scene reference survives.")]
    [SerializeField] private GameObject obstacleBlocker;
    [SerializeField] private string kidName = "Tala";
    [Tooltip("Where Tala steps to once she has her kite (off the path).")]
    [SerializeField] private Vector3 kidStepAside = new Vector3(-1.6f, 0.4f, 0f);

    [Header("Tree + kite")]
    [SerializeField] private Transform tree;
    [SerializeField] private Transform kite;

    [Header("Props (shown by some skills)")]
    [SerializeField] private SpriteRenderer prop;
    [SerializeField] private Sprite ladderSprite;
    [SerializeField] private Sprite hookSprite;
    [SerializeField] private Sprite cushionSprite;
    [SerializeField] private Sprite arrowSprite;
    [SerializeField] private Sprite sparkleSprite;
    [SerializeField] private Sprite heartSprite;

    [Header("Helper NPC (persuade / summon) - leave disabled in scene")]
    [SerializeField] private GameObject helperNpcObject;
    [SerializeField] private vc_NPCController helperNpc;
    [SerializeField] private Transform npcWalkTarget;

    [Header("Tuning")]
    [SerializeField] private float animDuration = 0.5f;
    [SerializeField] private float npcArrivePause = 0.6f;

    public event Action Completed;

    /// <summary>Tala (the kid who needs help) and her name, for the flow controller's Help/Later prompt.</summary>
    public Transform Kid => obstacleBlocker != null ? obstacleBlocker.transform : null;
    public string KidName => kidName;

    private bool _active;
    private bool _solved;
    private bool _subscribed;

    private Vector3 KidPos => obstacleBlocker != null ? obstacleBlocker.transform.position : transform.position;
    private Vector3 KidHands => KidPos + new Vector3(0.35f, 0.9f, 0f);
    private Vector3 TreeFoot => tree != null ? tree.position : KidPos + new Vector3(1.5f, 0f, 0f);

    private Vector3 _propScale = Vector3.one;

    private void Awake()
    {
        if (prop != null) { _propScale = prop.transform.localScale; prop.enabled = false; }
    }

    public void BeginTutorialQuest()
    {
        if (_active || _solved) return;
        _active = true;
        SubscribeToSkillManager();
        vc_FloatingMessage.Instance?.Show($"{kidName}'s kite is stuck in the tree. Use your skill to help!");
    }

    private void OnDestroy()
    {
        UnsubscribeFromSkillManager();
    }

    private void SubscribeToSkillManager()
    {
        if (vc_SkillManager.Instance == null || _subscribed) return;
        vc_SkillManager.Instance.SkillUsed += HandleSkillUsed;
        _subscribed = true;
    }

    private void UnsubscribeFromSkillManager()
    {
        if (vc_SkillManager.Instance == null || !_subscribed) return;
        vc_SkillManager.Instance.SkillUsed -= HandleSkillUsed;
        _subscribed = false;
    }

    private void HandleSkillUsed(int slotIndex, vc_PlayerSkill usedSkill)
    {
        if (!_active || _solved || usedSkill == null || usedSkill.SkillData == null) return;

        // Lock in immediately so a second press during the animation can't retrigger.
        _solved = true;
        _active = false;
        UnsubscribeFromSkillManager();

        StartCoroutine(Resolve(usedSkill.SkillData));
    }

    // ── one reaction per skill ────────────────────────────────────────────

    private static readonly string[] KnownTags =
    {
        "build", "repair", "push", "plan", "unlock", "survey", "craft", "paint", "barrier", "teach",
        "guide", "heal", "persuade", "map", "charm", "summon", "command", "direct", "collect", "inspect",
    };

    private static string PickTag(vc_SkillData s)
    {
        foreach (string t in KnownTags)
            if (s.HasTag(t)) return t;

        // A skill added later: fall back to a reaction of its kind.
        switch ((s.riaSecLetter ?? "").Trim().ToUpperInvariant())
        {
            case "R": return "build";
            case "I": return "survey";
            case "A": return "craft";
            case "S": return "teach";
            case "E": return "persuade";
            case "C": return "direct";
            default: return "inspect";
        }
    }

    private IEnumerator Resolve(vc_SkillData skill)
    {
        string k = kidName;
        switch (PickTag(skill))
        {
            case "build":
                Say("You build a quick ladder and climb up to free the kite.");
                yield return ShowProp(ladderSprite, TreeFoot + new Vector3(-0.2f, 0.9f, 0f));
                yield return KiteArc(KidHands, 0.9f);
                break;

            case "repair":
                Say($"The kite's string snapped. You tie it back together, then {k} reels it in.");
                yield return GrowString(0.6f);
                yield return Tug(2);
                yield return KitePullOnString(KidHands, 0.8f);
                break;

            case "push":
                Say("You give the tree a good shake... down comes the kite!");
                yield return Shake(tree, 0.6f, 0.12f);
                yield return KiteArc(KidHands, 0.6f);
                break;

            case "plan":
                Say("You wait for the right gust of wind. The kite lifts free and floats down.");
                yield return new WaitForSeconds(0.4f);
                yield return KiteFloat(KidHands, 1.6f);
                break;

            case "unlock":
                Say("The string is tangled in a knot. You work it loose, loop by loop.");
                yield return GrowString(0.3f);
                yield return Wobble(kite, 0.8f, 18f);
                yield return KitePullOnString(KidHands, 0.7f);
                break;

            case "survey":
                Say($"You search the tree branch by branch... there! One twig holds the string. {k} tugs it the right way.");
                yield return Search(0.6f);
                yield return ShowProp(sparkleSprite, KitePos + new Vector3(0.25f, -0.25f, 0f), blink: true);
                yield return GrowString(0.3f);
                yield return Tug(1);
                yield return KitePullOnString(KidHands, 0.7f);
                break;

            case "craft":
                Say("You craft a hook pole from a long stick, reach up, and hook the kite down.");
                yield return ReachWithProp(hookSprite, 0.6f, 0.8f);
                break;

            case "paint":
                Say($"You paint the kite's string bright red so {k} can see it in the leaves, and she pulls it free.");
                yield return Tint(kite, new Color(1f, 0.55f, 0.55f), 0.4f);
                yield return GrowString(0.5f, new Color32(220, 60, 60, 255));
                yield return Tug(1);
                yield return KitePullOnString(KidHands, 0.7f);
                break;

            case "barrier":
                Say("You shape a soft cushion under the tree. The kite drops safely onto it.");
                yield return ShowProp(cushionSprite, TreeFoot + new Vector3(-0.3f, 0.15f, 0f), keep: true);
                yield return KiteArc(TreeFoot + new Vector3(-0.3f, 0.35f, 0f), 0.6f);
                yield return KitePull(KidHands, 0.5f);
                HideProp();
                break;

            case "teach":
                Say($"You show {k} the trick: small, gentle tugs, not one big yank. She tries it herself... tug, tug, free!");
                yield return GrowString(0.4f);
                yield return Tug(3);
                yield return KitePullOnString(KidHands, 0.8f);
                break;

            case "guide":
                Say($"You guide {k} up the low branch, one careful step at a time.");
                yield return KidClimb();
                break;

            case "heal":
                Say($"{k} scraped her knee trying to climb. You patch it up. Feeling brave, she climbs up for her kite.");
                yield return ShowProp(heartSprite, KidPos + new Vector3(0f, 1.5f, 0f), blink: true);
                yield return KidClimb();
                break;

            case "persuade":
                Say("You convince the janitor to lend a hand with his tall broom.");
                yield return HelperReach();
                yield return KiteArc(KidHands, 0.6f);
                break;

            case "map":
                Say("You read which way the wind blows. One gust lifts the kite free and it drifts down.");
                yield return KiteFloat(KidHands, 1.4f);
                break;

            case "charm":
                Say($"Your friendly wave gets the janitor smiling. He happily comes over and gets the kite for {k}.");
                yield return ShowProp(heartSprite, PlayerPos + new Vector3(0f, 1.3f, 0f), blink: true);
                yield return HelperReach();
                yield return KiteArc(KidHands, 0.6f);
                break;

            case "summon":
                Say("You call the janitor over. He reaches up and gets the kite.");
                yield return HelperReach();
                yield return KiteArc(KidHands, 0.6f);
                break;

            case "command":
                Say("You give the janitor clear steps: bring the broom, stand there, reach up. He follows them and the kite comes down.");
                yield return HelperReach();
                yield return Shake(tree, 0.3f, 0.05f);
                yield return KiteArc(KidHands, 0.7f);
                break;

            case "direct":
                Say($"You mark the way with arrows. {k} pulls exactly along them, and the kite slides off the branch.");
                yield return ArrowPath(3);
                break;

            case "collect":
                Say("You pull the kite straight down to you, then hand it over.");
                yield return KitePull(PlayerPos + new Vector3(0f, 0.8f, 0f), 0.7f);
                yield return KitePull(KidHands, 0.5f);
                break;

            default: // inspect
                Say("You take a closer look. The string is only looped on a twig. One flick and it's free!");
                yield return Search(0.4f);
                yield return ShowProp(sparkleSprite, KitePos + new Vector3(-0.2f, -0.3f, 0f), blink: true);
                yield return Wobble(kite, 0.25f, 14f);
                yield return KiteArc(KidHands, 0.6f);
                break;
        }

        HideString();
        yield return KidCelebrateAndMove();
        CompleteQuest();
    }

    // ── animation helpers ─────────────────────────────────────────────────

    private Vector3 KitePos => kite != null ? kite.position : TreeFoot + new Vector3(0f, 2f, 0f);

    private static Vector3 PlayerPos
    {
        get
        {
            PlayerController p = FindFirstObjectByType<PlayerController>();
            return p != null ? p.transform.position : Vector3.zero;
        }
    }

    private void Say(string text) => vc_FloatingMessage.Instance?.Show(text);

    private IEnumerator KiteArc(Vector3 to, float duration)
    {
        if (kite == null) yield break;
        Vector3 from = kite.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += Mathf.Sin(k * Mathf.PI) * 0.6f;
            kite.position = p;
            kite.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * 20f);
            yield return null;
        }
        kite.position = to;
        kite.localRotation = Quaternion.identity;
    }

    private IEnumerator KiteFloat(Vector3 to, float duration)
    {
        if (kite == null) yield break;
        Vector3 from = kite.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            Vector3 p = Vector3.Lerp(from, to, k * k * (3f - 2f * k));
            p.x += Mathf.Sin(k * Mathf.PI * 3f) * 0.5f * (1f - k);
            p.y += Mathf.Sin(k * Mathf.PI) * 0.8f;
            kite.position = p;
            kite.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 3f) * 25f);
            yield return null;
        }
        kite.position = to;
        kite.localRotation = Quaternion.identity;
    }

    private IEnumerator KitePull(Vector3 to, float duration)
    {
        if (kite == null) yield break;
        Vector3 from = kite.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            kite.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        kite.position = to;
    }

    private IEnumerator Wobble(Transform target, float duration, float degrees)
    {
        if (target == null) yield break;
        Quaternion baseRot = target.localRotation;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            target.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 40f) * degrees * (1f - t / duration));
            yield return null;
        }
        target.localRotation = baseRot;
    }

    private IEnumerator Shake(Transform target, float duration, float amount)
    {
        if (target == null) yield break;
        Vector3 basePos = target.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            target.position = basePos + new Vector3(Mathf.Sin(t * 70f) * amount, 0f, 0f);
            yield return null;
        }
        target.position = basePos;
    }

    private IEnumerator Hop(Transform target, int hops)
    {
        if (target == null) yield break;
        Vector3 basePos = target.position;
        const float hopTime = 0.25f;
        for (int h = 0; h < hops; h++)
            for (float t = 0f; t < hopTime; t += Time.deltaTime)
            {
                target.position = basePos + new Vector3(0f, Mathf.Sin(t / hopTime * Mathf.PI) * 0.3f, 0f);
                yield return null;
            }
        target.position = basePos;
    }

    private IEnumerator Tint(Transform target, Color color, float duration)
    {
        SpriteRenderer sr = target != null ? target.GetComponentInChildren<SpriteRenderer>() : null;
        if (sr == null) yield break;
        Color from = sr.color;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            sr.color = Color.Lerp(from, color, t / duration);
            yield return null;
        }
        sr.color = color;
    }

    /// <summary>Pops a prop sprite in at a spot (scale up), holds it, then hides it unless keep.</summary>
    private IEnumerator ShowProp(Sprite sprite, Vector3 at, bool blink = false, bool keep = false)
    {
        if (prop == null || sprite == null) yield break;
        prop.sprite = sprite;
        prop.transform.position = at;
        prop.enabled = true;
        Vector3 full = _propScale;
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            prop.transform.localScale = full * Mathf.Lerp(0.2f, 1f, t / 0.2f);
            yield return null;
        }
        prop.transform.localScale = full;

        if (blink)
            for (int i = 0; i < 3; i++)
            {
                prop.enabled = false; yield return new WaitForSeconds(0.08f);
                prop.enabled = true;  yield return new WaitForSeconds(0.12f);
            }
        else
            yield return new WaitForSeconds(0.35f);

        if (!keep) prop.enabled = false;
    }

    private void HideProp()
    {
        if (prop != null) prop.enabled = false;
    }

    // ── kite string: a dotted line from Tala's hands to the kite ───────────
    // Makes "pull", "tug" and "tie" readable: the player sees the string, sees Tala lean
    // back on it, and sees the kite jerk with each tug.

    private const int StringDots = 14;
    private readonly System.Collections.Generic.List<SpriteRenderer> _stringDots = new System.Collections.Generic.List<SpriteRenderer>();
    private static Sprite _pixel;

    private static Sprite Pixel
    {
        get
        {
            if (_pixel != null) return _pixel;
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 16f);
            return _pixel;
        }
    }

    private void EnsureString(Color color)
    {
        SpriteRenderer kiteSr = kite != null ? kite.GetComponentInChildren<SpriteRenderer>() : null;
        while (_stringDots.Count < StringDots)
        {
            var go = new GameObject("KiteString");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Pixel;
            if (kiteSr != null) { sr.sortingLayerID = kiteSr.sortingLayerID; sr.sortingOrder = kiteSr.sortingOrder - 1; }
            _stringDots.Add(sr);
        }
        foreach (SpriteRenderer d in _stringDots) d.color = color;
    }

    /// <summary>Places the string dots between the hands and the kite; visible = fraction drawn (from the hands).</summary>
    private void LayString(float visible)
    {
        if (kite == null) return;
        Vector3 a = KidHands, b = kite.position;
        for (int i = 0; i < _stringDots.Count; i++)
        {
            float t = (i + 0.5f) / _stringDots.Count;
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y -= Mathf.Sin(t * Mathf.PI) * 0.18f;   // a little slack
            _stringDots[i].transform.position = p;
            _stringDots[i].enabled = t <= visible;
        }
    }

    private void HideString()
    {
        foreach (SpriteRenderer d in _stringDots) if (d != null) d.enabled = false;
    }

    /// <summary>The string appears, running from Tala's hands up to the kite.</summary>
    private IEnumerator GrowString(float duration, Color? color = null)
    {
        EnsureString(color ?? new Color32(250, 244, 230, 255));
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            LayString(t / duration);
            yield return null;
        }
        LayString(1f);
    }

    /// <summary>Tala leans back on the string; the kite jerks toward her each time.</summary>
    private IEnumerator Tug(int tugs)
    {
        Transform kid = obstacleBlocker != null ? obstacleBlocker.transform : null;
        if (kid == null || kite == null) yield break;
        float away = Mathf.Sign(KidPos.x - TreeFoot.x);
        if (away == 0f) away = -1f;
        Vector3 home = kid.position;
        for (int n = 0; n < tugs; n++)
        {
            Vector3 kiteFrom = kite.position;
            Vector3 kiteTo = Vector3.Lerp(kiteFrom, KidHands, 0.12f);
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                float k = t / 0.14f;
                kid.position = home + new Vector3(away * 0.16f * k, 0f, 0f);
                kite.position = Vector3.Lerp(kiteFrom, kiteTo, k);
                kite.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * 15f);
                LayString(1f);
                yield return null;
            }
            for (float t = 0f; t < 0.16f; t += Time.deltaTime)
            {
                kid.position = home + new Vector3(away * 0.16f * (1f - t / 0.16f), 0f, 0f);
                LayString(1f);
                yield return null;
            }
            kid.position = home;
            kite.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(0.12f);
        }
    }

    /// <summary>KitePull with the string staying attached (it shortens as the kite comes in).</summary>
    private IEnumerator KitePullOnString(Vector3 to, float duration)
    {
        if (kite == null) yield break;
        Vector3 from = kite.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            kite.position = Vector3.Lerp(from, to, 1f - (1f - k) * (1f - k));
            LayString(1f);
            yield return null;
        }
        kite.position = to;
        HideString();
    }

    /// <summary>A sparkle sweeps over the tree, branch by branch, and settles on the kite.</summary>
    private IEnumerator Search(float duration)
    {
        if (prop == null || sparkleSprite == null) yield break;
        prop.sprite = sparkleSprite;
        prop.transform.localScale = _propScale;
        prop.enabled = true;
        Vector3 low = TreeFoot + new Vector3(0f, 0.6f, 0f);
        Vector3 high = KitePos;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            Vector3 p = Vector3.Lerp(low, high, k);
            p.x += Mathf.Sin(k * Mathf.PI * 3f) * 0.55f * (1f - k);   // zig-zag across the branches
            prop.transform.position = p;
            yield return null;
        }
        prop.enabled = false;
    }

    /// <summary>
    /// A tool appears in hand, reaches up to the kite, hooks it and brings it back down with it
    /// (craft: the hook pole). The kite rides on the tool on the way down.
    /// </summary>
    private IEnumerator ReachWithProp(Sprite sprite, float reach, float back)
    {
        if (prop == null || sprite == null || kite == null) { yield return KitePull(KidHands, 0.8f); yield break; }
        Vector3 hand = KidHands + new Vector3(0.2f, 0f, 0f);
        prop.sprite = sprite;
        prop.transform.position = hand;
        prop.enabled = true;
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            prop.transform.localScale = _propScale * Mathf.Lerp(0.2f, 1f, t / 0.2f);
            yield return null;
        }
        prop.transform.localScale = _propScale;

        Vector3 tip = kite.position + new Vector3(0f, -0.15f, 0f);
        for (float t = 0f; t < reach; t += Time.deltaTime)
        {
            float k = t / reach;
            prop.transform.position = Vector3.Lerp(hand, tip, k * k * (3f - 2f * k));
            yield return null;
        }
        prop.transform.position = tip;
        yield return Wobble(kite, 0.25f, 12f);   // hooked

        Vector3 kiteOffset = kite.position - prop.transform.position;
        for (float t = 0f; t < back; t += Time.deltaTime)
        {
            float k = t / back;
            prop.transform.position = Vector3.Lerp(tip, hand, k * k * (3f - 2f * k));
            kite.position = prop.transform.position + kiteOffset * (1f - k);
            yield return null;
        }
        kite.position = KidHands;
        prop.enabled = false;
    }

    /// <summary>Tala climbs to the low branch, takes her kite, and climbs back down.</summary>
    private IEnumerator KidClimb()
    {
        Transform kid = obstacleBlocker != null ? obstacleBlocker.transform : null;
        if (kid == null || kite == null) { yield return KiteArc(KidHands, 0.6f); yield break; }
        Vector3 home = kid.position;
        Vector3 branch = Vector3.Lerp(home, new Vector3(TreeFoot.x - 0.4f, kite.position.y - 1.0f, home.z), 0.85f);
        // up in three little steps
        for (int s = 1; s <= 3; s++)
        {
            Vector3 from = kid.position, to = Vector3.Lerp(home, branch, s / 3f);
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                float k = t / 0.22f;
                kid.position = Vector3.Lerp(from, to, k) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 0.12f, 0f);
                yield return null;
            }
            kid.position = to;
        }
        yield return KitePull(KidHands, 0.4f);
        kite.SetParent(kid, true);
        Vector3 top = kid.position;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            kid.position = Vector3.Lerp(top, home, t / 0.5f);
            yield return null;
        }
        kid.position = home;
    }

    /// <summary>Arrows pop in one by one from the kite to Tala; the kite then slides along them.</summary>
    private IEnumerator ArrowPath(int count)
    {
        if (kite == null) yield break;
        Vector3 from = kite.position, to = KidHands;
        Vector3 mid = Vector3.Lerp(from, to, 0.5f) + new Vector3(0f, 0.5f, 0f);
        System.Func<float, Vector3> curve = k => (1 - k) * (1 - k) * from + 2 * (1 - k) * k * mid + k * k * to;

        var arrows = new System.Collections.Generic.List<SpriteRenderer>();
        if (prop != null && arrowSprite != null)
        {
            for (int i = 0; i < count; i++)
            {
                float k = (i + 1f) / (count + 1f);
                SpriteRenderer a = i == 0 ? prop : Instantiate(prop, prop.transform.parent);
                a.sprite = arrowSprite;
                a.transform.position = curve(k);
                Vector3 dir = curve(Mathf.Min(1f, k + 0.05f)) - curve(k);
                a.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                a.transform.localScale = _propScale;
                a.enabled = true;
                arrows.Add(a);
                SunodGame.Core.vc_Sfx.Play("page_next", 0.5f);
                yield return new WaitForSeconds(0.22f);
            }
        }

        yield return new WaitForSeconds(0.2f);
        const float slide = 1.0f;
        for (float t = 0f; t < slide; t += Time.deltaTime)
        {
            float k = t / slide;
            kite.position = curve(k);
            for (int i = 0; i < arrows.Count; i++)
                if (k >= (i + 1f) / (count + 1f)) arrows[i].enabled = false;   // the kite passes each arrow
            yield return null;
        }
        kite.position = to;
        for (int i = 0; i < arrows.Count; i++)
        {
            if (arrows[i] == prop) { prop.enabled = false; prop.transform.rotation = Quaternion.identity; }
            else Destroy(arrows[i].gameObject);
        }
    }

    private IEnumerator HelperReach()
    {
        if (helperNpcObject != null) helperNpcObject.SetActive(true);
        Vector3 target = npcWalkTarget != null ? npcWalkTarget.position : TreeFoot + new Vector3(-0.8f, -0.3f, 0f);
        if (helperNpc != null)
        {
            helperNpc.WalkToPoint(target);
            float until = Time.time + 6f;
            while (!helperNpc.HasReachedDestination() && Time.time < until) yield return null;
        }
        yield return new WaitForSeconds(npcArrivePause);
    }

    /// <summary>Tala hugs her kite, hops, and steps off the path (the path is now clear).</summary>
    private IEnumerator KidCelebrateAndMove()
    {
        if (obstacleBlocker == null) yield break;
        Transform kid = obstacleBlocker.transform;
        if (kite != null) kite.SetParent(kid, true);

        yield return Hop(kid, 2);

        // Off the path: no longer blocks the player.
        foreach (Collider2D c in obstacleBlocker.GetComponentsInChildren<Collider2D>())
            if (!c.isTrigger) c.enabled = false;

        Vector3 from = kid.position, to = from + kidStepAside;
        for (float t = 0f; t < animDuration; t += Time.deltaTime)
        {
            kid.position = Vector3.Lerp(from, to, t / animDuration);
            yield return null;
        }
        kid.position = to;
    }

    private void CompleteQuest()
    {
        // Objective check-off is owned by vc_TutorialFlowController.
        vc_FloatingMessage.Instance?.Show($"{kidName} got her kite back! The path is clear.");
        Completed?.Invoke();
    }
}
