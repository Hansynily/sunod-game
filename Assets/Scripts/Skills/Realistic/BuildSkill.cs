using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void UseBuild()
        {
            if (_buildUsed)
            {
                ShowToast("Bridge already built.");
                return;
            }

            if (_buildZone == null || !_buildZone.bounds.Contains(_player.position))
            {
                ShowToast("Nothing to build here.");
                return;
            }

            _buildUsed = true;
            if (_riverBlocker != null) _riverBlocker.enabled = false;

            float bridgeY = _buildZone != null ? _buildZone.bounds.center.y : _riverCenter.y;
            Vector3 bridgePos = new Vector3(_riverCenter.x, bridgeY, 0f);
            float bridgeWidth = RiverWidth + 0.8f;
            _bridge = CreateWorldRect("Bridge", null, bridgePos, new Vector2(bridgeWidth, BridgeHeight), new Color(0.45f, 0.25f, 0.12f, 1f));
            _bridge.GetComponent<SpriteRenderer>().sortingOrder = 1;

            ShowToast("Build used: bridge created.");
        }
    }
}
