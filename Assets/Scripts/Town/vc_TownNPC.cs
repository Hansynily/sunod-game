using UnityEngine;

/// <summary>
/// A townsperson who gives one quest (one RIASEC item). Shows "!" while the quest is
/// open, a check once it's been attempted, and "..." if their quest isn't built yet.
/// Walking up to them asks the player for help; vc_TownStage runs the quest itself.
///
/// Each day vc_TownStage picks a few open NPCs to make a request: they walk to the
/// request spot near the plaza and show "!". The rest of the open pool stays hidden
/// that day. Requests not answered by sleep time leave the town for good (Gone).
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
    /// <summary>The player said "Not for me" (or cancelled the quest). Counts as attempted.</summary>
    public bool Refused { get; private set; }
    /// <summary>Their request expired unanswered at sleep time. They've left the town.</summary>
    public bool Gone { get; private set; }
    /// <summary>Built, not attempted, not gone - still in the pool of possible requests.</summary>
    public bool IsOpen => HasQuest && !Attempted && !Gone;

    private Vector3 _homePosition;
    private bool _homeCaptured;

    private float _frameTimer;
    private int _frame;
    private float _phase;
    private Vector3 _markerBase;

    private void Awake()
    {
        CaptureHome();
        _phase = Random.value * 3f;
        _frame = Random.Range(0, Mathf.Max(1, idleFrames != null ? idleFrames.Length : 1));
        if (marker != null) _markerBase = marker.transform.localPosition;
        RefreshMarker();
    }

    private void Update()
    {
        if (body != null && idleFrames != null && idleFrames.Length > 0)
        {
            _frameTimer += Time.deltaTime;
            if (_frameTimer >= frameTime)
            {
                _frameTimer = 0f;
                _frame = (_frame + 1) % idleFrames.Length;
                body.sprite = idleFrames[_frame];
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
        if (!attempted) Refused = false;
        RefreshMarker();
    }

    public void SetRefused(bool refused)
    {
        Refused = refused;
        if (refused) Attempted = true;
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
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    /// <summary>Stands the NPC somewhere else for the day (the request spot).</summary>
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

    private void CaptureHome()
    {
        if (_homeCaptured) return;
        _homePosition = transform.position;
        _homeCaptured = true;
    }

    private void RefreshMarker()
    {
        if (marker == null) return;
        marker.sprite = !HasQuest ? markerSoon : (Attempted ? markerDone : markerQuest);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.CompareTag("Player"))
            vc_TownStage.Current?.OnNpcApproached(this);
    }
}
