using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class vc_SkillZone : MonoBehaviour
{
    private static int _activeZoneCount = 0;
    private bool _playerInside = false;

    private void OnDisable()
    {
        if (_playerInside)
            HandleExit();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || _playerInside) return;

        _playerInside = true;
        _activeZoneCount++;

        if (_activeZoneCount == 1)
            vc_SkillManager.Instance?.SetSkillsInteractable(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || !_playerInside) return;
        HandleExit();
    }

    private void HandleExit()
    {
        _playerInside = false;
        _activeZoneCount = Mathf.Max(0, _activeZoneCount - 1);

        if (_activeZoneCount == 0)
            vc_SkillManager.Instance?.SetSkillsInteractable(false);
    }

    public static void ResetCounter()
    {
        _activeZoneCount = 0;
    }
}
