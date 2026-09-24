using UnityEngine;

/// <summary>
/// Attach to a hallway's SealTrigger. Closes SealWall behind the player once they've
/// walked past it into the next room, but only if every quest in the room behind is
/// finished - a room with an active quest must stay walkable.
/// </summary>
[DisallowMultipleComponent]
public class vc_AreaSeal : MonoBehaviour
{
    [SerializeField] private GameObject sealWall;
    [SerializeField] private Transform roomBehind;

    private bool _sealed;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_sealed || other == null || !other.CompareTag("Player")) return;
        if (roomBehind == null || sealWall == null) return;

        vc_QuestRoom[] quests = roomBehind.GetComponentsInChildren<vc_QuestRoom>();
        foreach (vc_QuestRoom quest in quests)
        {
            if (!quest.IsFinished) return; // an unfinished quest keeps the room open
        }

        sealWall.SetActive(true);
        _sealed = true;
        vc_FloatingMessage.Instance?.Show("The path behind you is closed.");
    }
}
