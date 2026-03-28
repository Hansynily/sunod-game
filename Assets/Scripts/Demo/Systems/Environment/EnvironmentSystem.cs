using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void BuildEnvironment()
        {
            Vector3 start = _player.position;
            _riverCenter = new Vector3(RiverWorldX, start.y, 0f);

            float worldMinX = start.x - 6f;
            float worldMaxX = Mathf.Max(start.x + 8f, CatSpawnWorldX + 4f);
            _worldBounds = Rect.MinMaxRect(worldMinX, start.y - 4.5f, worldMaxX, start.y + 4.5f);

            GameObject envRoot = new("DemoEnvironment");
            if (GetContentParent() != null)
                envRoot.transform.SetParent(GetContentParent(), false);

            GameObject riverVisual = CreateWorldRect("RiverVisual", envRoot.transform, _riverCenter, new Vector2(RiverWidth, 8f), new Color(0.2f, 0.45f, 0.9f, 0.35f));
            riverVisual.GetComponent<SpriteRenderer>().sortingOrder = 5;

            GameObject blocker = new("RiverBlocker", typeof(BoxCollider2D));
            blocker.transform.SetParent(envRoot.transform, false);
            blocker.transform.position = _riverCenter;
            _riverBlocker = blocker.GetComponent<BoxCollider2D>();
            _riverBlocker.size = new Vector2(RiverWidth, 8f);
            _riverBlocker.isTrigger = false;

            float buildZoneX = _riverCenter.x - ((RiverWidth * 0.5f) + 0.8f);
            GameObject buildZone = CreateWorldRect("BuildZone", envRoot.transform, new Vector3(buildZoneX, _riverCenter.y, 0f), new Vector2(BuildZoneWidth, 2.2f), new Color(0.95f, 0.8f, 0.2f, 0.15f));
            buildZone.GetComponent<SpriteRenderer>().sortingOrder = 6;
            _buildZone = buildZone.AddComponent<BoxCollider2D>();
            _buildZone.size = new Vector2(BuildZoneWidth, 2.2f);
            _buildZone.isTrigger = true;

            CreateBoundaryColliders(envRoot.transform, _worldBounds);
        }

        private static GameObject CreateWorldRect(string name, Transform parent, Vector3 position, Vector2 size, Color color)
        {
            GameObject go = new(name, typeof(SpriteRenderer));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite;
            renderer.color = color;
            return go;
        }

        private static void CreateBoundaryColliders(Transform parent, Rect bounds)
        {
            float thickness = 0.4f;
            CreateBoundary(parent, "BoundaryTop", new Vector2(bounds.center.x, bounds.yMax + thickness * 0.5f), new Vector2(bounds.width + thickness * 2f, thickness));
            CreateBoundary(parent, "BoundaryBottom", new Vector2(bounds.center.x, bounds.yMin - thickness * 0.5f), new Vector2(bounds.width + thickness * 2f, thickness));
            CreateBoundary(parent, "BoundaryLeft", new Vector2(bounds.xMin - thickness * 0.5f, bounds.center.y), new Vector2(thickness, bounds.height + thickness * 2f));
            CreateBoundary(parent, "BoundaryRight", new Vector2(bounds.xMax + thickness * 0.5f, bounds.center.y), new Vector2(thickness, bounds.height + thickness * 2f));
        }

        private static void CreateBoundary(Transform parent, string name, Vector2 center, Vector2 size)
        {
            GameObject wall = new(name, typeof(BoxCollider2D));
            wall.transform.SetParent(parent, false);
            wall.transform.position = center;
            var collider = wall.GetComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = false;
        }

    }
}
