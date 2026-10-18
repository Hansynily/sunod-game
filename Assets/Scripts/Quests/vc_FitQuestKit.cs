using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small helpers on top of vc_FitQuestBase that most hand-built quests repeat: timed bar steps,
/// hops, walking a sprite with frames, spawned props that get cleaned up on restart, delayed
/// calls and a "point above this sprite" helper.
/// </summary>
public abstract class vc_FitQuestKit : vc_FitQuestBase
{
    private readonly List<GameObject> _spawned = new List<GameObject>();

    /// <summary>Waits 'seconds' while the progress bar eases from k0 to k1.</summary>
    protected IEnumerator Step(float seconds, float k0, float k1)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetBar(Mathf.Lerp(k0, k1, t / seconds));
            yield return null;
        }
    }

    /// <summary>Waits 'seconds' and advances the running clock / bar (clock is t, total is the routine length).</summary>
    protected IEnumerator Tick(float seconds, float[] clock, float total)
    {
        float k0 = clock[0] / total;
        clock[0] += seconds;
        yield return Step(seconds, k0, clock[0] / total);
    }

    protected IEnumerator Hop(Transform tr, int px = 2)
    {
        if (tr == null) yield break;
        Vector3 p = tr.position;
        tr.position = p + new Vector3(0f, px / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (tr != null) tr.position = p;
    }

    /// <summary>Moves a sprite from-to, cycling walk frames (frame 0 when it stops).</summary>
    protected IEnumerator WalkSprite(SpriteRenderer sr, Sprite[] frames, Vector3 from, Vector3 to, float seconds)
    {
        if (sr == null) yield break;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            sr.transform.position = Snap(Vector3.Lerp(from, to, t / seconds));
            if (frames != null && frames.Length > 0) sr.sprite = frames[(int)(t * 8f) % frames.Length];
            yield return null;
        }
        sr.transform.position = to;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    /// <summary>A prop that lives until the quest restarts.</summary>
    protected SpriteRenderer Spawn(Sprite s, Vector3 at, int order, string name = "Spawned")
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, true);
        go.transform.position = Snap(at);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = order;
        _spawned.Add(go);
        return sr;
    }

    protected void ClearSpawned()
    {
        foreach (var g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
    }

    protected IEnumerator PopIn(Transform tr)
    {
        float[] s = { 0.4f, 0.8f, 1.15f, 1f };
        Vector3 baseScale = tr != null ? tr.localScale : Vector3.one;
        foreach (float k in s)
        {
            if (tr == null) yield break;
            tr.localScale = baseScale * k;
            yield return new WaitForSeconds(0.05f);
        }
    }

    protected void Later(float seconds, System.Action a) => StartCoroutine(LaterRoutine(seconds, a));

    private IEnumerator LaterRoutine(float s, System.Action a)
    {
        yield return new WaitForSeconds(s);
        a();
    }

    protected static Vector3 Above(SpriteRenderer sr, float extra = 0.3f)
    {
        if (sr == null) return Vector3.zero;
        Bounds b = sr.bounds;
        return new Vector3(b.center.x, b.max.y + extra, 0f);
    }

    protected void Bubble(Sprite s, SpriteRenderer over, float seconds = 1.4f, int order = 24)
    {
        if (s != null && over != null) Fx(new[] { s }, Above(over), 1f / seconds, 0.2f, order);
    }

    protected static void Sfx(string clip, float vol = 0.6f, float jitter = 0.1f) => SunodGame.Core.vc_Sfx.Play(clip, vol, jitter);
}
