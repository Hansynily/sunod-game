using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private void UseTrack()
        {
            _trackActive = !_trackActive;
            if (_xrayOverlay != null) _xrayOverlay.gameObject.SetActive(_trackActive);
            ShowToast(_trackActive ? "Track enabled." : "Track disabled.");
        }

        private void SpawnPawPrint()
        {
            if (_cat == null) return;

            GameObject paw = new("PawPrint", typeof(SpriteRenderer));
            paw.transform.position = _cat.transform.position + new Vector3(0f, -0.2f, 0f);
            paw.transform.localScale = new Vector3(0.16f, 0.16f, 1f);

            SpriteRenderer renderer = paw.GetComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite;
            renderer.color = new Color(0.9f, 0.9f, 0.2f, _trackActive ? 0.9f : 0f);
            renderer.sortingOrder = 35;
            renderer.enabled = _trackActive;

            _pawPrints.Add(new PawPrintData
            {
                go = paw,
                renderer = renderer,
                expiresAt = Time.time + PawPrintLifetime
            });
        }

        private void UpdatePawPrints()
        {
            for (int i = _pawPrints.Count - 1; i >= 0; i--)
            {
                PawPrintData data = _pawPrints[i];
                if (data == null || data.go == null)
                {
                    _pawPrints.RemoveAt(i);
                    continue;
                }

                float remaining = data.expiresAt - Time.time;
                if (remaining <= 0f)
                {
                    Destroy(data.go);
                    _pawPrints.RemoveAt(i);
                    continue;
                }

                float alpha = Mathf.Clamp01(remaining / PawPrintLifetime);
                if (data.renderer != null)
                {
                    data.renderer.enabled = _trackActive;
                    Color c = data.renderer.color;
                    c.a = _trackActive ? alpha : 0f;
                    data.renderer.color = c;
                }
            }
        }
    }
}
