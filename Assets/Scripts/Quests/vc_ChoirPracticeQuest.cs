using System.Collections;
using UnityEngine;

/// <summary>
/// A quest (A1). Lead the school choir for Mr. Santos. Any APPROPRIATE skill works - how well
/// it fits decides how fast it goes (and so the stars); skills that make no sense do nothing.
///
///   craft    A  best fit     arranges the song and conducts; rows sing in harmony          ~2s
///   barrier  A  workaround   shapes one section's voice per press until they blend          x3
///   paint    A  workaround   draws a colour-coded score, then they sing                     5s
///   command  E  off-letter   firm cues; the choir follows, a bit stiff                      8s
///   teach    S  off-letter   teaches each section its part, then they sing                  8s
///   anything else: a hint, no effect
///
/// Singers are laid out row by row (3 rows of 3). Each row has its own note colour.
/// While a slow fix is running the best fit (craft) takes over at once.
/// </summary>
[DisallowMultipleComponent]
public class vc_ChoirPracticeQuest : vc_FitQuestBase
{
    [Header("Choir")]
    [Tooltip("Singers, row 0 first (front), 3 per row.")]
    [SerializeField] private SpriteRenderer[] singers;
    [SerializeField] private Sprite[] mouthClosed;
    [SerializeField] private Sprite[] mouthOpen;
    [SerializeField] private int perRow = 3;
    [Tooltip("Note sprite per row.")]
    [SerializeField] private Sprite[] rowNotes;

    [Header("Props")]
    [SerializeField] private SpriteRenderer baton;
    [SerializeField] private Sprite[] batonFrames;
    [SerializeField] private SpriteRenderer score;
    [SerializeField] private Sprite scorePlain;
    [SerializeField] private Sprite scoreColor;
    [SerializeField] private SpriteRenderer piano;
    [SerializeField] private Sprite[] pianoFrames;

    [Header("Effects")]
    [SerializeField] private Sprite dotsBubble;
    [SerializeField] private Sprite[] splatFrames;
    [SerializeField] private Sprite[] sparkFrames;

    [Header("Timing (seconds)")]
    [SerializeField] private float craftTime = 2f;
    [SerializeField] private int shapePresses = 3;
    [SerializeField] private float paintTime = 5f;
    [SerializeField] private float commandTime = 8f;
    [SerializeField] private float teachTime = 8f;

    private Vector3[] _rest;
    private int _shaped;
    private float _idleClock;

    private int Rows => singers == null ? 0 : Mathf.CeilToInt(singers.Length / (float)Mathf.Max(1, perRow));

    protected override Vector3 BarWorldPosition =>
        (baton != null ? baton.transform.position : transform.position) + new Vector3(-0.5f, 1.4f, 0f);

    private void Awake()
    {
        int n = singers != null ? singers.Length : 0;
        _rest = new Vector3[n];
        for (int i = 0; i < n; i++) _rest[i] = singers[i].transform.position;
    }

    protected override void OnBegin()
    {
        _shaped = 0;
        _idleClock = 1f;
        for (int i = 0; i < singers.Length; i++) { singers[i].transform.position = _rest[i]; Mouth(i, false); }
        if (score != null) { score.sprite = scorePlain; score.enabled = false; }
        Baton(1);
        Piano(0);

        vc_QuestHUD.Instance?.ShowQuestInfo(
            "Quest",
            "Choir Practice",
            "The school choir needs someone to lead them today.",
            new[] { "Lead the choir" });
    }

    protected override void OnWorkCancelled()
    {
        for (int i = 0; i < singers.Length; i++) { singers[i].transform.position = _rest[i]; Mouth(i, false); }
        Baton(1);
    }

    // ── Idle life: the choir chatters while waiting ──────────────────────

    private void Update()
    {
        if (!Started || Done || Working || singers == null || singers.Length == 0) return;
        _idleClock -= Time.deltaTime;
        if (_idleClock > 0f) return;
        _idleClock = Random.Range(1.0f, 2.0f);
        int i = Random.Range(0, singers.Length);
        StartCoroutine(Chatter(i));
    }

    private IEnumerator Chatter(int i)
    {
        Mouth(i, true);
        if (dotsBubble != null && Random.value < 0.5f) Fx(new[] { dotsBubble }, Top(i), 1.5f, 0.1f, 24);
        yield return new WaitForSeconds(0.25f);
        if (!Working) Mouth(i, false);
    }

    // ── Skill handling ───────────────────────────────────────────────────

    protected override void OnSkill(vc_SkillData d)
    {
        if (d.HasTag("craft")) { StartWork(CraftRoutine()); return; }
        if (BlockedByWork()) return;
        if (d.HasTag("barrier")) { ShapePress(); return; }
        if (d.HasTag("paint")) { StartWork(PaintRoutine()); return; }
        if (d.HasTag("command")) { StartWork(CommandRoutine()); return; }
        if (d.HasTag("teach")) { StartWork(TeachRoutine()); return; }
        Toast("The choir needs an artistic lead.", 2.2f);
    }

    // ── Craft: arrange and conduct, all in harmony ───────────────────────

    private IEnumerator CraftRoutine()
    {
        Toast("You arrange the song and conduct...", craftTime);
        ShowBar(true);
        yield return SingAll(craftTime, 0f, craftTime, hops: true);
        Finish("Perfect harmony! The choir sounds amazing.");
    }

    // ── Barrier: shape one section per press ─────────────────────────────

    private void ShapePress()
    {
        int row = _shaped % Mathf.Max(1, Rows);
        _shaped++;
        StartCoroutine(SingRow(row, 1.0f, true));
        Fx(sparkFrames, RowCenter(row) + new Vector3(0f, 0.9f, 0f), 12f, 0.15f);
        if (_shaped < shapePresses)
        {
            Toast($"You shape this section's voice ({_shaped}/{shapePresses})", 2f);
            return;
        }
        StartCoroutine(SingAllLoose(1.8f));
        Finish("Every section shaped. Now they blend!");
    }

    // ── Paint: colour-coded score, then sing ─────────────────────────────

    private IEnumerator PaintRoutine()
    {
        Toast("You draw a colour-coded score...", paintTime);
        ShowBar(true);
        if (score != null) { score.enabled = true; score.sprite = scorePlain; }
        float paint = paintTime - 2f, t = 0f;
        int strokes = 5;
        float per = paint / strokes;
        for (int s = 0; s < strokes; s++)
        {
            if (score != null) Fx(splatFrames, score.bounds.center + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.15f, 0.2f), 0f), 10f, 0f, 22);
            SunodGame.Core.vc_Sfx.Play("brush", 0.45f, 0.1f);
            if (s == strokes - 1 && score != null) { score.sprite = scoreColor; Fx(sparkFrames, score.bounds.center, 12f, 0.15f); }
            yield return BarStep(per, t / paintTime, (t + per) / paintTime);
            t += per;
        }
        yield return SingAll(paintTime - t, t, paintTime, hops: true);
        Finish("Each section knows its colour. Lovely!");
    }

    // ── Command: firm cues, a bit stiff ──────────────────────────────────

    private IEnumerator CommandRoutine()
    {
        Toast("You lead with firm cues!", commandTime);
        ShowBar(true);
        float t = 0f;
        int beat = 0;
        float cue = 0.8f;
        while (t < commandTime - 0.01f)
        {
            Baton(beat % 2 == 0 ? 0 : 2);
            SunodGame.Core.vc_Sfx.Play("clack", 0.4f);
            int row = (beat / 3) % Mathf.Max(1, Rows);
            // stiff: one row at a time, mouths snap open and shut, no bouncing
            for (int i = 0; i < singers.Length; i++) Mouth(i, i / perRow == row && beat % 2 == 0);
            if (beat % 2 == 0) Note(row);
            float step = Mathf.Min(cue, commandTime - t);
            yield return BarStep(step, t / commandTime, (t + step) / commandTime);
            t += step;
            beat++;
        }
        for (int i = 0; i < singers.Length; i++) Mouth(i, false);
        Finish("A bit stiff, but the choir followed every cue.");
    }

    // ── Teach: each section its part ─────────────────────────────────────

    private IEnumerator TeachRoutine()
    {
        Toast("You teach each section its part...", teachTime);
        ShowBar(true);
        float allSing = 1.6f, t = 0f;
        int rows = Rows;
        float per = (teachTime - allSing) / Mathf.Max(1, rows);
        for (int r = 0; r < rows; r++)
        {
            Piano(1); SunodGame.Core.vc_Sfx.Play("piano_note", 0.45f, 0.3f);
            yield return BarStep(per * 0.4f, t / teachTime, (t + per * 0.4f) / teachTime);
            Piano(0);
            StartCoroutine(SingRow(r, per * 0.6f, false));
            yield return BarStep(per * 0.6f, (t + per * 0.4f) / teachTime, (t + per) / teachTime);
            t += per;
        }
        yield return SingAll(teachTime - t, t, teachTime, hops: true);
        Finish("Every section learned its part!");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private IEnumerator SingAll(float seconds, float t0, float total, bool hops)
    {
        float t = 0f;
        int beat = 0;
        while (t < seconds)
        {
            Baton(beat % 3);
            for (int i = 0; i < singers.Length; i++) Mouth(i, (beat + i / perRow) % 2 == 0);
            if (beat % 2 == 0)
            {
                for (int r = 0; r < Rows; r++) Note(r);
                Piano(1 + beat / 2 % 2);
                SunodGame.Core.vc_Sfx.Play("piano_note", 0.4f, 0.4f);
                if (hops) for (int i = 0; i < singers.Length; i++) StartCoroutine(Hop(i));
            }
            else Piano(0);
            float step = Mathf.Min(0.25f, seconds - t);
            yield return BarStep(step, (t0 + t) / total, (t0 + t + step) / total);
            t += step;
            beat++;
        }
        for (int i = 0; i < singers.Length; i++) Mouth(i, false);
        Baton(1); Piano(0);
    }

    private IEnumerator SingAllLoose(float seconds)
    {
        float t = 0f;
        int beat = 0;
        while (t < seconds)
        {
            for (int i = 0; i < singers.Length; i++) Mouth(i, (beat + i / perRow) % 2 == 0);
            if (beat % 2 == 0) { for (int r = 0; r < Rows; r++) Note(r); SunodGame.Core.vc_Sfx.Play("piano_note", 0.4f, 0.4f); }
            yield return new WaitForSeconds(0.25f);
            t += 0.25f; beat++;
        }
        for (int i = 0; i < singers.Length; i++) Mouth(i, false);
    }

    private IEnumerator SingRow(int row, float seconds, bool hop)
    {
        float t = 0f;
        bool open = true;
        Note(row);
        SunodGame.Core.vc_Sfx.Play("piano_note", 0.4f, 0.3f);
        while (t < seconds)
        {
            for (int i = row * perRow; i < Mathf.Min(singers.Length, (row + 1) * perRow); i++)
            {
                Mouth(i, open);
                if (hop && open) StartCoroutine(Hop(i));
            }
            open = !open;
            yield return new WaitForSeconds(0.22f);
            t += 0.22f;
        }
        for (int i = row * perRow; i < Mathf.Min(singers.Length, (row + 1) * perRow); i++) Mouth(i, false);
    }

    private void Note(int row)
    {
        if (rowNotes == null || rowNotes.Length == 0) return;
        Fx(new[] { rowNotes[row % rowNotes.Length] }, RowCenter(row) + new Vector3(Random.Range(-0.6f, 0.6f), 1.1f, 0f), 1.6f, 0.6f, 24);
    }

    private Vector3 RowCenter(int row)
    {
        Vector3 sum = Vector3.zero; int n = 0;
        for (int i = row * perRow; i < Mathf.Min(singers.Length, (row + 1) * perRow); i++) { sum += _rest[i]; n++; }
        return n > 0 ? sum / n : transform.position;
    }

    private void Mouth(int i, bool open)
    {
        if (i < 0 || i >= singers.Length || singers[i] == null) return;
        Sprite[] set = open ? mouthOpen : mouthClosed;
        if (set != null && i < set.Length && set[i] != null) singers[i].sprite = set[i];
    }

    private void Baton(int f)
    {
        if (baton != null && batonFrames != null && f < batonFrames.Length && batonFrames[f] != null) baton.sprite = batonFrames[f];
    }

    private void Piano(int f)
    {
        if (piano != null && pianoFrames != null && f < pianoFrames.Length && pianoFrames[f] != null) piano.sprite = pianoFrames[f];
    }

    private Vector3 Top(int i) => _rest[i] + new Vector3(0f, 1.4f, 0f);

    private IEnumerator Hop(int i)
    {
        if (singers[i] == null) yield break;
        singers[i].transform.position = _rest[i] + new Vector3(0f, 1f / 22f, 0f);
        yield return new WaitForSeconds(0.1f);
        if (singers[i] != null) singers[i].transform.position = _rest[i];
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
