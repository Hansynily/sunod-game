using UnityEngine;

/// <summary>
/// A townsperson who gives one quest (one RIASEC item). Shows "!" while the quest is
/// open, a check once it's been attempted, and "..." if their quest isn't built yet.
/// Walking up to them asks the player for help; vc_TownStage runs the quest itself.
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
    public bool Attempted { get; private set; }

    private float _frameTimer;
    private int _frame;
    private float _phase;
    private Vector3 _markerBase;

    private void Awake()
    {
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
        RefreshMarker();
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
