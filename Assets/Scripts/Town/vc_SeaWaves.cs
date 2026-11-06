using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moving sea for the coastal towns (2026-11-07). Added by vc_TownStage when the town has a
/// "ShoreFoam" line (built by vc_TownBuilder):
///  - the foam along the shore washes in and out (flips between its two frames and slides a
///    pixel up and back, a little out of step along the beach),
///  - wave crests roll in from the open sea toward the beach and fade as they reach it, only
///    around the camera so it stays cheap.
/// Purely visual: nothing here collides or is saved.
/// </summary>
[DisallowMultipleComponent]
public class vc_SeaWaves : MonoBehaviour
{
    private const float Px = 1f / 16f;

    private readonly List<(Transform t, SpriteRenderer sr, Vector3 rest, int phase)> _foam = new List<(Transform, SpriteRenderer, Vector3, int)>();
    private readonly Dictionary<int, float> _shoreY = new Dictionary<int, float>();
    private Sprite[] _frames;
    private float _tick;
    private int _step;

    private class Crest { public SpriteRenderer sr; public float x, y, stopY, t, life; }
    private readonly List<Crest> _crests = new List<Crest>();
    private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
    private float _nextCrest;
    private Camera _cam;

    private void Start()
    {
        Transform foam = transform.Find("ShoreFoam");
        if (foam == null) { enabled = false; return; }
        Sprite f0 = null, f1 = null;
        foreach (Transform c in foam)
        {
            SpriteRenderer sr = c.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) continue;
            if (c.name == "foam0" && f0 == null) f0 = sr.sprite;
            if (c.name == "foam1" && f1 == null) f1 = sr.sprite;
            int x = Mathf.FloorToInt(c.position.x);
            _foam.Add((sr.transform, sr, sr.transform.localPosition, (x / 3) & 3));
            _shoreY[x] = c.position.y;
        }
        _frames = new[] { f0 ?? f1, f1 ?? f0 };
        _cam = Camera.main;
    }

    private void Update()
    {
        // foam: wash in and out in small, pixel-sized steps
        _tick += Time.deltaTime;
        if (_tick >= 0.32f)
        {
            _tick = 0f;
            _step++;
            foreach (var (t, sr, rest, phase) in _foam)
            {
                if (t == null) continue;
                int s = (_step + phase) % 8;
                sr.sprite = _frames[(s / 2) & 1];
                float dy = s < 3 ? s * Px : s < 6 ? (5 - s) * Px : -Px;   // in 0,1,2 · back 2,1,0 · a pull out
                t.localPosition = rest + new Vector3(0f, dy, 0f);
            }
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _frames[0] == null) return;

        // crests rolling in, only where the camera looks
        if (Time.time > _nextCrest && _crests.Count < 14)
        {
            _nextCrest = Time.time + Random.Range(0.25f, 0.6f);
            float h = _cam.orthographicSize, w = h * _cam.aspect;
            float x = _cam.transform.position.x + Random.Range(-w, w);
            if (_shoreY.TryGetValue(Mathf.FloorToInt(x), out float shore))
            {
                float startY = shore - Random.Range(1.5f, 6.5f);
                if (startY > _cam.transform.position.y - h - 1f) Spawn(x, startY, shore - 0.4f);
            }
        }

        for (int i = _crests.Count - 1; i >= 0; i--)
        {
            Crest c = _crests[i];
            c.t += Time.deltaTime;
            c.y = Mathf.Min(c.stopY, c.y + 0.45f * Time.deltaTime);
            float k = c.t / c.life;
            // swell in, hold, fade as it nears the beach (and never past the foam)
            float a = Mathf.Clamp01(k * 3f) * Mathf.Clamp01((c.stopY - c.y) / 1.2f + (1f - k) * 0.2f);
            c.sr.color = new Color(1f, 1f, 1f, Mathf.Round(a * 4f) / 4f * 0.75f);
            c.sr.transform.position = new Vector3(Mathf.Round(c.x * 16f) / 16f, Mathf.Round(c.y * 16f) / 16f, 0f);
            if (k >= 1f || a <= 0.01f && k > 0.4f)
            {
                c.sr.gameObject.SetActive(false);
                _pool.Push(c.sr);
                _crests.RemoveAt(i);
            }
        }
    }

    private void Spawn(float x, float y, float stopY)
    {
        SpriteRenderer sr = _pool.Count > 0 ? _pool.Pop() : null;
        if (sr == null)
        {
            var go = new GameObject("WaveCrest");
            go.transform.SetParent(transform, false);
            sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = -13;   // over the water and the deep-sea shade, under everything on land
        }
        sr.gameObject.SetActive(true);
        sr.sprite = _frames[Random.Range(0, 2)];
        sr.transform.localScale = new Vector3(Random.Range(0, 2) == 0 ? 2f : 3f, 1f, 1f);   // a longer line than the shore foam
        _crests.Add(new Crest { sr = sr, x = x, y = y, stopY = stopY, life = Random.Range(3.5f, 6f) });
    }
}
