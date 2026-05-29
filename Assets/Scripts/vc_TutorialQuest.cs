using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Tutorial quest: one obstacle, solvable by ANY of the six RIASEC skills, but
/// each skill resolves it in its own flavor (mirrors vc_SlipperyWayQuest /
/// vc_CatQuest, keyed off capabilityTags).
///
///   bridge/build (R) → obstacle sinks into the ground
///   scan        (I) → obstacle fades away (saw the hidden gap)
///   craft       (A) → obstacle squashes flat (reshaped)
///   navigate    (C) → obstacle slides aside (found the trail)
///   help        (S) → helper NPC walks in and clears it
///   persuade    (E) → helper NPC walks in and moves it aside
///
/// Tutorial-only — standalone, does NOT use vc_QuestRoom / vc_IQuestLogic.
/// </summary>
[DisallowMultipleComponent]
public class vc_TutorialQuest : MonoBehaviour
{
    [Header("Obstacle (acted on by build / scan / craft / navigate)")]
    [SerializeField] private GameObject obstacleBlocker;

    [Header("Helper NPC (used by help / persuade) — leave disabled in scene")]
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

    public void BeginTutorialQuest()
    {
        if (_active || _solved) return;
        _active = true;
        SubscribeToSkillManager();
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

        vc_SkillData data = usedSkill.SkillData;

        // Lock in immediately so a second press during the animation can't retrigger.
        _solved = true;
        _active = false;
        UnsubscribeFromSkillManager();

        if (data.HasTag("bridge") || data.HasTag("build"))
        {
            vc_FloatingMessage.Instance?.Show("You build a way straight through.");
            StartCoroutine(ResolveObstacleMotion(Vector3.down * 1.25f, fadeOut: true));
        }
        else if (data.HasTag("scan"))
        {
            vc_FloatingMessage.Instance?.Show("You see the hidden gap and slip through.");
            StartCoroutine(ResolveFadeThrough());
        }
        else if (data.HasTag("craft"))
        {
            vc_FloatingMessage.Instance?.Show("You mold it down out of the way.");
            StartCoroutine(ResolveSquash());
        }
        else if (data.HasTag("navigate"))
        {
            vc_FloatingMessage.Instance?.Show("You find the trail around it.");
            StartCoroutine(ResolveObstacleMotion(Vector3.right * 2f, fadeOut: false));
        }
        else if (data.HasTag("help"))
        {
            vc_FloatingMessage.Instance?.Show("Together, you clear the way.");
            StartCoroutine(ResolveWithNpc());
        }
        else if (data.HasTag("persuade"))
        {
            vc_FloatingMessage.Instance?.Show("You convince them to move it aside.");
            StartCoroutine(ResolveWithNpc());
        }
        else
        {
            // Any other skill still solves it (tutorial single-skill rule).
            vc_FloatingMessage.Instance?.Show("The way is clear.");
            StartCoroutine(ResolveFadeThrough());
        }
    }

    // ── Obstacle resolutions ──────────────────────────────────────────────

    private IEnumerator ResolveObstacleMotion(Vector3 offset, bool fadeOut)
    {
        if (obstacleBlocker != null)
        {
            Transform t = obstacleBlocker.transform;
            SpriteRenderer sr = obstacleBlocker.GetComponent<SpriteRenderer>();
            Vector3 from = t.position;
            Vector3 to = from + offset;
            Color baseColor = sr != null ? sr.color : Color.white;

            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / animDuration);
                t.position = Vector3.Lerp(from, to, k);
                if (sr != null && fadeOut)
                {
                    Color c = baseColor; c.a = 1f - k; sr.color = c;
                }
                yield return null;
            }

            if (fadeOut) obstacleBlocker.SetActive(false);
        }

        CompleteQuest();
    }

    private IEnumerator ResolveFadeThrough()
    {
        if (obstacleBlocker != null)
        {
            SpriteRenderer sr = obstacleBlocker.GetComponent<SpriteRenderer>();
            Color baseColor = sr != null ? sr.color : Color.white;

            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / animDuration);
                if (sr != null) { Color c = baseColor; c.a = 1f - k; sr.color = c; }
                yield return null;
            }

            obstacleBlocker.SetActive(false);
        }

        CompleteQuest();
    }

    private IEnumerator ResolveSquash()
    {
        if (obstacleBlocker != null)
        {
            Transform t = obstacleBlocker.transform;
            Vector3 from = t.localScale;
            Vector3 to = new Vector3(from.x * 1.3f, 0.02f, from.z);

            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / animDuration);
                t.localScale = Vector3.Lerp(from, to, k);
                yield return null;
            }

            obstacleBlocker.SetActive(false);
        }

        CompleteQuest();
    }

    private IEnumerator ResolveWithNpc()
    {
        if (helperNpcObject != null) helperNpcObject.SetActive(true);

        Vector3 target = npcWalkTarget != null
            ? npcWalkTarget.position
            : (obstacleBlocker != null ? obstacleBlocker.transform.position : transform.position);

        if (helperNpc != null)
        {
            helperNpc.WalkToPoint(target);
            yield return new WaitUntil(helperNpc.HasReachedDestination);
        }

        yield return new WaitForSeconds(npcArrivePause);

        if (obstacleBlocker != null) obstacleBlocker.SetActive(false);

        CompleteQuest();
    }

    private void CompleteQuest()
    {
        // Objective check-off is owned by vc_TutorialFlowController.
        vc_FloatingMessage.Instance?.Show("Path cleared!");
        Completed?.Invoke();
    }
}
