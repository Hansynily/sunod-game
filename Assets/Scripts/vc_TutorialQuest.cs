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
///   repair   - you fix the snapped string and reel the kite in
///   push     - you shake the tree until the kite drops
///   plan     - you wait for the right gust; the kite floats down
///   unlock   - you untangle the knot and the kite slides free
///   survey   - a sparkle shows where the string is snagged; it wiggles loose
///   craft    - you make a hook pole and pull the kite down
///   paint    - you paint the tail bright so Tala sees where to pull
///   barrier  - you shape a soft cushion; the kite drops onto it
///   teach    - you show Tala how to tug gently; she frees it herself
///   guide    - you guide Tala up the low branch safely
///   heal     - you patch Tala's scraped knee; feeling brave, she gets it
///   persuade - you convince the janitor to lend his tall broom
///   map      - you read the wind; a gust lifts the kite free
///   charm    - you cheer the kids up; a tall kid happily grabs it
///   summon   - you call the janitor over to reach it
///   command  - you get the bigger kids to form a human ladder
///   direct   - arrows show Tala which way to pull
///   collect  - you pull the kite straight down to you
///   inspect  - a closer look: it's only looped on a twig; one flick frees it
///
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
                Say($"The kite's string snapped. You tie it back together and reel it in for {k}.");
                yield return Wobble(kite, 0.5f, 10f);
                yield return KitePull(KidHands, 0.9f);
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
                yield return Wobble(kite, 0.8f, 18f);
                yield return KitePull(KidHands, 0.7f);
                break;

            case "survey":
                Say("You look around and spot exactly where the string is snagged.");
                yield return ShowProp(sparkleSprite, KitePos + new Vector3(0.3f, -0.2f, 0f), blink: true);
                yield return Wobble(kite, 0.4f, 12f);
                yield return KiteArc(KidHands, 0.7f);
                break;

            case "craft":
                Say("You craft a hook from a long stick and pull the kite down.");
                yield return ShowProp(hookSprite, KidPos + new Vector3(0.9f, 1.3f, 0f));
                yield return KitePull(KidHands, 0.9f);
                break;

            case "paint":
                Say($"You paint the kite's tail bright red so {k} can see where to pull.");
                yield return Tint(kite, new Color(1f, 0.55f, 0.55f), 0.5f);
                yield return KitePull(KidHands, 0.8f);
                break;

            case "barrier":
                Say("You shape a soft cushion under the tree. The kite drops safely onto it.");
                yield return ShowProp(cushionSprite, TreeFoot + new Vector3(-0.3f, 0.15f, 0f), keep: true);
                yield return KiteArc(TreeFoot + new Vector3(-0.3f, 0.35f, 0f), 0.6f);
                yield return KitePull(KidHands, 0.5f);
                HideProp();
                break;

            case "teach":
                Say($"You show {k} how to tug the string gently. She frees it herself!");
                yield return Hop(obstacleBlocker != null ? obstacleBlocker.transform : null, 2);
                yield return KitePull(KidHands, 1.0f);
                break;

            case "guide":
                Say($"You guide {k} up the low branch, one careful step at a time.");
                yield return Hop(obstacleBlocker != null ? obstacleBlocker.transform : null, 1);
                yield return KiteArc(KidHands, 0.6f);
                break;

            case "heal":
                Say($"{k} scraped her knee trying to climb. You patch it up. Feeling brave, she gets her kite.");
                yield return ShowProp(heartSprite, KidPos + new Vector3(0f, 1.5f, 0f), blink: true);
                yield return KitePull(KidHands, 0.8f);
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
                Say($"You cheer everyone up. A tall kid happily grabs the kite for {k}.");
                yield return ShowProp(heartSprite, KidPos + new Vector3(0.6f, 1.5f, 0f), blink: true);
                yield return KiteArc(KidHands, 0.6f);
                break;

            case "summon":
                Say("You call the janitor over. He reaches up and gets the kite.");
                yield return HelperReach();
                yield return KiteArc(KidHands, 0.6f);
                break;

            case "command":
                Say("You get the bigger kids to make a human ladder. Teamwork!");
                yield return Shake(tree, 0.3f, 0.05f);
                yield return KiteArc(KidHands, 0.7f);
                break;

            case "direct":
                Say($"You point {k} which way to pull. The kite slides right off the branch.");
                yield return ShowProp(arrowSprite, KidPos + new Vector3(0.8f, 0.9f, 0f), blink: true);
                yield return KitePull(KidHands, 0.8f);
                break;

            case "collect":
                Say("You pull the kite straight down to you, then hand it over.");
                yield return KitePull(PlayerPos + new Vector3(0f, 0.8f, 0f), 0.7f);
                yield return KitePull(KidHands, 0.5f);
                break;

            default: // inspect
                Say("You take a closer look. The string is only looped on a twig. One flick and it's free!");
                yield return ShowProp(sparkleSprite, KitePos + new Vector3(-0.2f, -0.3f, 0f), blink: true);
                yield return KiteArc(KidHands, 0.6f);
                break;
        }

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
