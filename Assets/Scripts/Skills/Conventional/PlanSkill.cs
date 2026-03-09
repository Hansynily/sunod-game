using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void UpdatePlanIndicator()
        {
            if (!_planUnlocked || _player == null || _cat == null) return;
            if (Time.time < _nextPlanUpdateAt) return;

            _nextPlanUpdateAt = Time.time + 2f;
            ClearPlanDots();

            Vector3 from = _player.position;
            Vector3 to = _cat.transform.position;
            const int dotCount = 12;

            for (int i = 1; i <= dotCount; i++)
            {
                float t = i / (float)(dotCount + 1);
                Vector3 pos = Vector3.Lerp(from, to, t);
                GameObject dot = new($"PlanDot_{i}", typeof(SpriteRenderer));
                dot.transform.position = pos;
                dot.transform.localScale = new Vector3(0.08f, 0.08f, 1f);

                SpriteRenderer renderer = dot.GetComponent<SpriteRenderer>();
                renderer.sprite = WhiteSprite;
                renderer.color = new Color(0.95f, 0.95f, 0.95f, 0.95f);
                renderer.sortingOrder = 34;

                _planDots.Add(dot);
            }
        }

        private void ClearPlanDots()
        {
            for (int i = 0; i < _planDots.Count; i++)
            {
                if (_planDots[i] != null)
                    Destroy(_planDots[i]);
            }

            _planDots.Clear();
        }
    }
}
