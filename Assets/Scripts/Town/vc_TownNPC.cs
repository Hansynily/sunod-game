using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// A townsperson who gives one quest (one RIASEC item). Shows "!" while the quest is
/// open, a check once it was completed, a soft "x" if it was failed, and "..." if their
/// quest isn't built yet. Walking up to them asks the player for help; vc_TownStage runs
/// the quest itself.
///
/// Each day vc_TownStage picks a few open NPCs to make a request: they show "!" where they
/// live (scattered around town, so the player explores to find them). The rest of the open
/// pool stays hidden that day. Requests not answered by sleep time leave the town for good
/// (Gone). A turned-down NPC walks off and fades out (<see cref="WalkAway"/>).
/// </summary>
[DisallowMultipleComponent]
public class vc_TownNPC : MonoBehaviour
{
    [Header("Identity")]
    public string npcId;
    public string npcName;
    [Tooltip("RIASEC item this NPC's quest measures (e.g. R6). Label only - the quest prefab's own questionCode is what gets scored.")]
    public string questCode;

    [Header("Quest")]
    [Tooltip("Room prefab with a vc_QuestRoom. Leave empty if the quest isn't built yet.")]
    public GameObject questPrefab;
    [TextArea(2, 4)] public string askLine;
    [TextArea(2, 4)] public string thanksLine;

    [Header("Visuals")]
    public Sprite[] idleFrames;
    public float frameTime = 0.15f;
    public SpriteRenderer body;
    public SpriteRenderer marker;
    public Sprite markerQuest;
    public Sprite markerDone;
    public Sprite markerSoon;
    [Tooltip("Quest attempted but not completed (failed on the timer). Loaded from Resources/TownLife/emote_missed when empty.")]
    public Sprite markerMissed;

    public bool HasQuest => questPrefab != null;

    /// <summary>The quest's id (vc_QuestRoom.QuestId) - what the save and progress track.</summary>
    public string QuestId
    {
        get
        {
            if (_questId == null)
            {
                vc_QuestRoom room = questPrefab != null ? questPrefab.GetComponentInChildren<vc_QuestRoom>(true) : null;
                _questId = room != null && !string.IsNullOrEmpty(room.QuestId) ? room.QuestId : npcId;
            }
            return _questId;
        }
    }
    private string _questId;
    public bool Attempted { get; private set; }
    /// <summary>The attempt finished the quest (stars earned). False for a fail or a refusal.</summary>
    public bool Passed { get; private set; }
    /// <summary>The player said "Not for me" (or cancelled the quest). Counts as attempted.</summary>
    public bool Refused { get; private set; }
    /// <summary>Their request expired unanswered at sleep time. They've left the town.</summary>
    public bool Gone { get; private set; }
    /// <summary>Built, not attempted, not gone - still in the pool of possible requests.</summary>
    public bool IsOpen => HasQuest && !Attempted && !Gone;
    /// <summary>Playing the walk-off animation (no talking to them any more).</summary>
    public bool Leaving { get; private set; }

    /// <summary>Palette name for the baked walk sheets ("adam_1"), read from the idle frames.</summary>
    public string Look => _look ?? (_look = vc_NpcSprites.LookFromSprite(idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : body != null ? body.sprite : null));
    private string _look;

    private Vector3 _homePosition;
    private bool _homeCaptured;

    private float _frameTimer;
    private int _frame;
    private float _phase;
    private Vector3 _markerBase;
    private Sprite[] _overrideFrames;   // walking frames while leaving

    private void Awake()
    {
        CaptureHome();
        _phase = UnityEngine.Random.value * 3f;
        _frame = UnityEngine.Random.Range(0, Mathf.Max(1, idleFrames != null ? idleFrames.Length : 1));
        if (marker != null) _markerBase = marker.transform.localPosition;
        if (markerMissed == null) markerMissed = Resources.Load<Sprite>("TownLife/emote_missed");
        RefreshMarker();
    }

    private void Update()
    {
        Sprite[] frames = _overrideFrames ?? idleFrames;
        if (body != null && frames != null && frames.Length > 0)
        {
            _frameTimer += Time.deltaTime;
            if (_frameTimer >= frameTime)
            {
                _frameTimer = 0f;
                _frame = (_frame + 1) % frames.Length;
                body.sprite = frames[_frame];
            }
        }

        if (marker != null)
        {
            float bob = Mathf.Round(Mathf.Sin((Time.time + _phase) * 3f) * 1.5f) / 16f;   // whole-pixel bob
            marker.transform.localPosition = _markerBase + new Vector3(0f, bob, 0f);
        }
    }

    public void SetAttempted(bool attempted)
    {
        Attempted = attempted;
        if (!attempted) { Refused = false; Passed = false; }
        RefreshMarker();
    }

    /// <summary>Whether the attempt completed the quest (check) or not (soft "x").</summary>
    public void SetPassed(bool passed)
    {
        Passed = passed;
        RefreshMarker();
    }

    public void SetRefused(bool refused)
    {
        Refused = refused;
        if (refused) { Attempted = true; Passed = false; }
        RefreshMarker();
    }

    public void SetGone(bool gone)
    {
        Gone = gone;
        if (gone) gameObject.SetActive(false);
    }

    /// <summary>Shows or hides the NPC (hidden = not requesting anything today).</summary>
    public void SetVisible(bool visible)
    {
        if (Leaving && visible) return;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    /// <summary>Stands the NPC somewhere else for the day.</summary>
    public void PlaceAt(Vector3 position)
    {
        CaptureHome();
        position.z = transform.position.z;
        transform.position = position;
    }

    /// <summary>Back to where the town builder put them.</summary>
    public void ReturnHome()
    {
        CaptureHome();
        transform.position = _homePosition;
    }

    /// <summary>
    /// Turned down / gave up: the NPC turns away, walks off a few tiles and fades out, then
    /// hides. Purely visual - the caller has already saved the outcome.
    /// </summary>
    public void WalkAway(Vector3 awayFrom, Action onGone = null)
    {
        if (!gameObject.activeInHierarchy) { onGone?.Invoke(); return; }
        StopAllCoroutines();
        StartCoroutine(WalkAwayRoutine(awayFrom, onGone));
    }

    private IEnumerator WalkAwayRoutine(Vector3 awayFrom, Action onGone)
    {
        Leaving = true;
        if (marker != null) marker.enabled = false;
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>()) c.enabled = false;

        Vector2 dir = (Vector2)(transform.position - awayFrom);
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
        // walk mostly along one axis, like the player does
        dir = Mathf.Abs(dir.x) >= Mathf.Abs(dir.y) ? new Vector2(Mathf.Sign(dir.x), 0f) : new Vector2(0f, Mathf.Sign(dir.y));

        Sprite[] walk = vc_NpcSprites.Walk(Look, vc_NpcSprites.DirFor(dir));
        if (walk != null) { _overrideFrames = walk; _frame = 0; frameTime = 0.1f; }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        const float walkTime = 1.6f, fadeTime = 0.7f, speed = 2.2f;
        for (float t = 0f; t < walkTime; t += Time.deltaTime)
        {
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            float a = t < walkTime - fadeTime ? 1f : Mathf.Clamp01((walkTime - t) / fadeTime);
            a = Mathf.Round(a * 4f) / 4f;   // stepped fade, pixel-art style
            foreach (SpriteRenderer sr in renderers)
                if (sr != null) { Color c = sr.color; c.a = a; sr.color = c; }
            yield return null;
        }

        gameObject.SetActive(false);
        // reset for a Continue / debug reset that brings them back
        foreach (SpriteRenderer sr in renderers)
            if (sr != null) { Color c = sr.color; c.a = 1f; sr.color = c; }
        foreach (Collider2D c in GetComponentsInChildren<Collider2D>(true)) c.enabled = true;
        if (marker != null) marker.enabled = true;
        _overrideFrames = null;
        frameTime = 0.15f;
        ReturnHome();
        Leaving = false;
        onGone?.Invoke();
    }

    private void CaptureHome()
    {
        if (_homeCaptured) return;
        _homePosition = transform.position;
        _homeCaptured = true;
    }

    private void RefreshMarker()
    {
        if (marker == null) return;
        if (!HasQuest) marker.sprite = markerSoon;
        else if (!Attempted) marker.sprite = markerQuest;
        else marker.sprite = Passed || markerMissed == null ? markerDone : markerMissed;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Leaving && other != null && other.CompareTag("Player"))
            vc_TownStage.Current?.OnNpcApproached(this);
    }
}
