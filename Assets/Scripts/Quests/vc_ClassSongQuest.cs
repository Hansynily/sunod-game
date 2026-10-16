using System.Collections;
using UnityEngine;

/// <summary>
/// A quest (A4). Help Bea write the class song. Any APPROPRIATE skill works - how well it fits
/// decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   craft    A  best fit     pieces melody and words together; board fills, kids sing    ~2s
///   paint    A  workaround   writes the lyrics big and bright, then the kids sing along   4s
///   barrier  A  workaround   stacks note blocks into a tune, one per press                x3
///   teach    S  off-letter   teaches the kids the tune line by line                       8s
///   anything else: a hint, no effect
///
/// While a slow fix is running the best fit (craft) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_ClassSongQuest : vc_FitQuestBase
{
    [Header("Scene")]
    [SerializeField] private SpriteRenderer piano;
    [Tooltip("0 = idle, 1/2 = keys lit (alternated while playing).")]
    [SerializeField] private Sprite[] pianoFrames;
    [SerializeField] private SpriteRenderer lyricsBoard;
    [Tooltip("0 = blank, 1 = half, 2 = full, 3 = bright painted lyrics.")]
    [SerializeField] private Sprite[] boardFrames;
    [SerializeField] private SpriteRenderer[] kids;
    [SerializeField] private Sprite[] kidMouthClosed;
    [SerializeField] private Sprite[] kidMouthOpen;

    [Header("Effects")]
    [SerializeField] private Sprite[] noteSprites;
    [SerializeField] private Sprite[] noteBlocks;
    [SerializeField] private Sprite[] splatFrames;
    [SerializeField] private Sprite[] sparkFrames;
    [SerializeField] private Sprite dotsBubble;

    [Header("Timing (seconds)")]
    [SerializeField] private float craftTime = 2f;
    [SerializeField] private float paintTime = 4f;
    [SerializeField] private int blockPresses = 3;
    [SerializeField] private float teachTime = 8f;

    private Vector3[] _kidRest;
    private int _blocks;
    private float _idleClock, _dotsClock;
    private readonly System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();

    protected override Vector3 BarWorldPosition =>
        (lyricsBoard != null ? lyricsBoard.transform.position : transform.position) + new Vector3(0.3f, 2.3f, 0f);

    private void Awake()
    {
        int n = kids != null ? kids.Length : 0;
        _kidRest = new Vector3[n];
        for (int i = 0; i < n; i++) _kidRest[i] = kids[i].transform.position;
    }

    protected override void OnBegin()
    {
        foreach (GameObject g in _spawned) if (g != null) Destroy(g);
        _spawned.Clear();
        _blocks = 0;
        _idleClock = 1f; _dotsClock = 1.5f;
        SetPiano(0);
        SetBoard(0);
        for (int i = 0; i < kids.Length; i++) { kids[i].transform.position = _kidRest[i]; Mouth(i, false); }

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Class Song",
            "The class program needs a song. Help Bea write one.",
            new[] { "Write the class song" });
    }

    // ── Idle life: the kids fidget and wait ──────────────────────────────

    private void Update()
    {
        if (!Started || Done || Working || kids == null || kids.Length == 0) return;

        _idleClock -= Time.deltaTime;
        if (_idleClock <= 0f)
        {
            _idleClock = Random.Range(1.2f, 2.4f);
            StartCoroutine(Hop(Random.Range(0, kids.Length), 2));
        }
        _dotsClock -= Time.deltaTime;
        if (_dotsClock <= 0f && dotsBubble != null)
        {
            _dotsClock = Random.Range(2.4f, 3.4f);
            Fx(new[] { dotsBubble }, Top(Random.Range(0, kids.Length)), 1.4f, 0.15f, 24);
        }
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        if (d.HasTag("barrier")) { BlockPress(); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        Toast("A song needs a creative spark.", 2.2f);
    }

    // ── Craft: melody and words pieced together ──────────────────────────

    private IEnumerator CraftRoutine()
    {
        Toast("You piece the melody and words together...", craftTime);
        ShowBar(true);
        float t = 0f, step = 0.25f;
        int i = 0;
        while (t < craftTime - 0.5f)
        {
            PlayNote(i++);
            if (t > craftTime * 0.25f && t < craftTime * 0.25f + step) { SetBoard(1); Fx(sparkFrames, BoardCenter(), 12f, 0.1f); }
            if (t > craftTime * 0.55f && t < craftTime * 0.55f + step) { SetBoard(2); Fx(sparkFrames, BoardCenter(), 12f, 0.1f); }
            yield return BarStep(step, t / craftTime, (t + step) / craftTime);
            t += step;
        }
        SetBoard(2);
        StartCoroutine(Sing(2.4f));
        yield return BarStep(craftTime - t, t / craftTime, 1f);
        Finish("The class song is done. Everyone sings!");
    }

    // ── Paint: lyrics big and bright, then everyone sings ────────────────

    private IEnumerator PaintRoutine()
    {
        Toast("You paint the lyrics big and bright...", paintTime);
        ShowBar(true);
        float paintPart = paintTime - 1.4f, t = 0f;
        int strokes = 6;
        float per = paintPart / strokes;
        Vector3 c = BoardCenter();
        for (int s = 0; s < strokes; s++)
        {
            Vector3 at = c + new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(-0.4f, 0.4f), 0f);
            Fx(splatFrames, at, 10f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("brush", 0.5f, 0.1f);
            if (s == 2) SetBoard(1);
            if (s == strokes - 1) { SetBoard(3); Fx(sparkFrames, c, 12f, 0.15f); }
            yield return BarStep(per, t / paintTime, (t + per) / paintTime);
            t += per;
        }
        StartCoroutine(Sing(2f));
        yield return BarStep(paintTime - t, t / paintTime, 1f);
        Finish("Big, bright lyrics. The kids sing along!");
    }

    // ── Barrier: build the tune from stacked note blocks ─────────────────

    private void BlockPress()
    {
        _blocks++;
        if (noteBlocks != null && noteBlocks.Length > 0 && piano != null)
        {
            var go = new GameObject("NoteBlock");
            go.transform.SetParent(transform, true);
            Bounds pb = piano.bounds;
            go.transform.position = Snap(new Vector3(pb.min.x + 0.45f + (_blocks - 1) * 0.6f, pb.max.y + 0.3f, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = noteBlocks[(_blocks - 1) % noteBlocks.Length];
            sr.sortingLayerName = sortingLayer; sr.sortingOrder = 14;
            _spawned.Add(go);
            StartCoroutine(Drop(go.transform));
        }
        PlayNote(_blocks * 2);
        SetBoard(_blocks >= 2 ? 1 : 0);

        if (_blocks < blockPresses)
        {
            Toast($"You stack a note block ({_blocks}/{blockPresses})", 2f);
            return;
        }
        SetBoard(2);
        Fx(sparkFrames, BoardCenter(), 12f, 0.15f);
        StartCoroutine(Sing(2f));
        Finish("Block by block, the tune is built. Sing!");
    }

    // ── Teach: the tune line by line ─────────────────────────────────────

    private IEnumerator TeachRoutine()
    {
        Toast("You teach the kids the tune, line by line...", teachTime);
        ShowBar(true);
        int lines = 4;
        float per = teachTime / lines, t = 0f;
        for (int l = 0; l < lines; l++)
        {
            // you play the line, then more kids join in each time
            for (int k = 0; k < 3; k++) PlayNote(l * 3 + k, k * 0.22f);
            yield return BarStep(per * 0.45f, t / teachTime, (t + per * 0.45f) / teachTime);
            int singers = Mathf.Min(kids.Length, l + 1);
            for (int i = 0; i < singers; i++) StartCoroutine(SingOne(i, per * 0.45f));
            if (l == 1) SetBoard(1);
            if (l == 3) SetBoard(2);
            yield return BarStep(per * 0.55f, (t + per * 0.45f) / teachTime, (t + per) / teachTime);
            t += per;
        }
        StartCoroutine(Sing(1.6f));
        Finish("They know the tune by heart now!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void PlayNote(int i, float delay = 0f) => StartCoroutine(NoteRoutine(i, delay));

    private IEnumerator NoteRoutine(int i, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        SetPiano(1 + (i % 2));
        SunodGame.Core.vc_Sfx.Play("piano_note", 0.45f, 0.35f);
        if (piano != null && noteSprites != null && noteSprites.Length > 0)
        {
            Bounds b = piano.bounds;
            Vector3 at = new Vector3(Random.Range(b.min.x + 0.3f, b.max.x - 0.3f), b.max.y + 0.1f, 0f);
            Fx(new[] { noteSprites[i % noteSprites.Length] }, at, 1.6f, 0.7f, 22);
        }
        yield return new WaitForSeconds(0.18f);
        SetPiano(0);
    }

    private IEnumerator Sing(float seconds)
    {
        float t = 0f;
        int beat = 0;
        while (t < seconds)
        {
            for (int i = 0; i < kids.Length; i++) Mouth(i, (beat + i) % 2 == 0);
            if (beat % 2 == 0)
            {
                PlayNote(beat);
                for (int i = 0; i < kids.Length; i++) StartCoroutine(Hop(i, 1));
                if (noteSprites != null && noteSprites.Length > 0 && kids.Length > 0)
                    Fx(new[] { noteSprites[beat % noteSprites.Length] }, Top(beat % kids.Length), 1.6f, 0.5f, 24);
            }
            beat++;
            yield return new WaitForSeconds(0.3f);
            t += 0.3f;
        }
        for (int i = 0; i < kids.Length; i++) Mouth(i, false);
    }

    private IEnumerator SingOne(int i, float seconds)
    {
        float t = 0f;
        bool open = true;
        if (noteSprites != null && noteSprites.Length > 0) Fx(new[] { noteSprites[i % noteSprites.Length] }, Top(i), 1.6f, 0.5f, 24);
        while (t < seconds)
        {
            Mouth(i, open);
            open = !open;
            yield return new WaitForSeconds(0.2f);
            t += 0.2f;
        }
        Mouth(i, false);
    }

    private void Mouth(int i, bool open)
    {
        if (i < 0 || i >= kids.Length || kids[i] == null) return;
        Sprite[] set = open ? kidMouthOpen : kidMouthClosed;
        if (set != null && i < set.Length && set[i] != null) kids[i].sprite = set[i];
    }

    private void SetPiano(int f)
    {
        if (piano != null && pianoFrames != null && f < pianoFrames.Length && pianoFrames[f] != null) piano.sprite = pianoFrames[f];
    }

    private void SetBoard(int f)
    {
        if (lyricsBoard != null && boardFrames != null && f < boardFrames.Length && boardFrames[f] != null) lyricsBoard.sprite = boardFrames[f];
    }

    private Vector3 BoardCenter()
    {
        if (lyricsBoard == null) return transform.position;
        Bounds b = lyricsBoard.bounds;
        return new Vector3(b.center.x, b.center.y + b.extents.y * 0.25f, 0f);
    }

    private Vector3 Top(int i)
    {
        Bounds b = kids[i].bounds;
        return new Vector3(b.center.x, b.max.y + 0.3f, 0f);
    }

    private IEnumerator Hop(int i, int px)
    {
        if (i < 0 || i >= kids.Length || kids[i] == null) yield break;
        Transform tr = kids[i].transform;
        tr.position = _kidRest[i] + new Vector3(0f, px / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (tr != null) tr.position = _kidRest[i];
    }

    private IEnumerator Drop(Transform tr)
    {
        Vector3 rest = tr.position;
        float[] off = { 0.6f, 0.3f, 0.1f, -0.05f, 0f };
        foreach (float o in off)
        {
            if (tr == null) yield break;
            tr.position = Snap(rest + new Vector3(0f, o, 0f));
            yield return new WaitForSeconds(0.05f);
        }
        SunodGame.Core.vc_Sfx.Play("plop", 0.5f, 0.1f);
    }

    private IEnumerator BarStep(float seconds, float k0, float k1)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetBar(Mathf.Lerp(k0, k1, t / seconds));
            yield return null;
        }
    }
}
