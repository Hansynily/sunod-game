using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class vc_DoorController : MonoBehaviour
{
    [SerializeField] private SpriteRenderer doorRenderer;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Sprite openSprite;
    [SerializeField] private BoxCollider2D doorCollider;
    [SerializeField] private Collider2D[] collidersToDisable;
    [SerializeField] private bool startUnlocked = false;

    private void Awake()
    {
        if (doorRenderer == null)
        {
            doorRenderer = GetComponent<SpriteRenderer>();
        }

        if (doorCollider == null)
        {
            doorCollider = GetComponent<BoxCollider2D>();
        }

        SetLockedState();

        if (doorRenderer != null)
        {
            doorRenderer.sprite = lockedSprite;
        }

        if (startUnlocked)
        {
            Unlock();
        }
    }

    private void OnEnable()
    {
        vc_QuestRoom.OnAnyQuestComplete += Unlock;
    }

    private void OnDisable()
    {
        vc_QuestRoom.OnAnyQuestComplete -= Unlock;
    }

    private void SetLockedState()
    {
        if (doorCollider != null)
        {
            doorCollider.enabled = true;
        }

        if (collidersToDisable == null)
        {
            return;
        }

        for (int i = 0; i < collidersToDisable.Length; i++)
        {
            Collider2D colliderToReset = collidersToDisable[i];
            if (colliderToReset == null)
            {
                continue;
            }

            colliderToReset.enabled = true;
        }
    }

    public void Unlock()
    {
        if (doorCollider != null)
        {
            doorCollider.enabled = false;
        }

        if (collidersToDisable != null)
        {
            for (int i = 0; i < collidersToDisable.Length; i++)
            {
                Collider2D colliderToDisable = collidersToDisable[i];
                if (colliderToDisable == null)
                {
                    continue;
                }

                colliderToDisable.enabled = false;
            }
        }

        if (doorRenderer != null)
        {
            doorRenderer.sprite = openSprite;
        }
    }
}
