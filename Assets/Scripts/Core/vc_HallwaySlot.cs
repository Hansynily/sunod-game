using UnityEngine;

namespace SunodGame.Core
{
    /// <summary>
    /// Marks a position in the scene where a hallway prefab should be instantiated
    /// on floor load. Place this component on the hallway slot GameObjects.
    /// vc_FloorInitializer finds all of these and loads the configured hallwayPrefab into each.
    /// </summary>
    [DisallowMultipleComponent]
    public class vc_HallwaySlot : MonoBehaviour
    {
        public bool IsLoaded { get; private set; }

        public void MarkLoaded() => IsLoaded = true;
    }
}
