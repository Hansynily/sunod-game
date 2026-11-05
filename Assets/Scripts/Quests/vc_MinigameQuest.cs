using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Shared shell for the "New gameplay" minigame quests (MINIGAME_DESIGN.md). A subclass only
/// builds its game inside the popup card (Play) and sets Score (0-100). This class pauses the
/// 30 s room timer, turns the score into stars (90/70/50/30 = 5/4/3/2, else 1), logs the helper
/// skills used, and finishes the quest.
/// </summary>
public abstract class vc_MinigameQuest : vc_FitQuestBase
{
    protected vc_MinigameOverlay UI;
    protected vc_SkillHelpers.Bar HelperBar;
    protected float Score;
    private readonly List<string> _helpersUsed = new List<string>();

    protected abstract string Code { get; }
    protected abstract string QuestName { get; }
    protected abstract string Intro { get; }
    protected abstract string CardTitle { get; }
    protected abstract IEnumerator Play();
    protected abstract string FinishLine(float score);
    protected virtual bool CanUseHelper(vc_SkillHelpers.Effect e) => true;
    protected abstract void UseHelper(vc_SkillHelpers.Effect e, vc_SkillData s);

    protected override Vector3 BarWorldPosition => transform.position + new Vector3(-0.5f, 1.4f, 0f);

    protected virtual void Awake()
    {
        UI = GetComponent<vc_MinigameOverlay>();
        if (UI == null) UI = gameObject.AddComponent<vc_MinigameOverlay>();
    }

    protected override void OnBegin()
    {
        _helpersUsed.Clear();
        Score = 0f;
        if (vc_QuestTimer.Instance != null)
        {
            vc_QuestTimer.Instance.ExternalHold = true;
            vc_QuestTimer.Instance.SetStarsVisible(false);
        }
        vc_QuestHUD.Instance?.ShowQuestInfo("Quest", QuestName, Intro, new[] { CardTitle });
        StartWork(Run());
    }

    protected override void OnSkill(vc_SkillData d) => Toast("Use your skills in the minigame panel.", 2f);

    protected override void OnWorkCancelled() => UI?.Close();

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(0.5f);
        UI.Open(CardTitle);
        yield return Play();

        Score = Mathf.Clamp(Score, 0f, 100f);
        int stars = Score >= 90f ? 5 : Score >= 70f ? 4 : Score >= 50f ? 3 : Score >= 30f ? 2 : 1;
        vc_QuestTimer.Instance?.SetStarOverride(stars);
        Debug.Log($"[Minigame] {Code}: {Mathf.RoundToInt(Score)}% -> {stars} stars; helpers used: " +
                  (_helpersUsed.Count > 0 ? string.Join(", ", _helpersUsed) : "none"));
        UI.Close();
        yield return new WaitForSeconds(0.4f);
        Finish(FinishLine(Score));
    }

    /// <summary>The player's skills as helper buttons.</summary>
    protected vc_SkillHelpers.Bar BuildHelpers(VisualElement parent)
    {
        HelperBar = vc_SkillHelpers.BuildBar(parent, CanUseHelper, (e, s) =>
        {
            _helpersUsed.Add($"{s.skillName} ({vc_SkillHelpers.EffectName(e)})");
            UseHelper(e, s);
        });
        return HelperBar;
    }

    // ── Small UI helpers ─────────────────────────────────────────────────

    protected static VisualElement Row(VisualElement parent, float marginTop = 6)
    {
        var r = new VisualElement();
        r.style.flexDirection = FlexDirection.Row;
        r.style.justifyContent = Justify.Center;
        r.style.alignItems = Align.Center;
        r.style.flexWrap = Wrap.Wrap;
        r.style.marginTop = marginTop;
        parent.Add(r);
        return r;
    }

    protected static VisualElement Box(VisualElement parent, float w, float h, Color c)
    {
        var b = new VisualElement();
        b.style.width = w; b.style.height = h;
        b.style.backgroundColor = c;
        b.style.flexShrink = 0;
        parent?.Add(b);
        return b;
    }

    protected static Button SmallButton(VisualElement parent, string text, System.Action onClick, float width = 0)
    {
        var b = new Button(onClick) { text = text };
        b.AddToClassList("sl-button");
        b.style.fontSize = 22;
        b.style.color = vc_MinigameOverlay.Ink;
        b.style.minHeight = 52;
        if (width > 0) b.style.width = width;
        b.style.marginLeft = 4; b.style.marginRight = 4; b.style.marginTop = 4;
        b.style.paddingBottom = 8;
        parent.Add(b);
        return b;
    }

    protected static void Border(VisualElement el, float w, Color c)
    {
        el.style.borderLeftWidth = w; el.style.borderRightWidth = w; el.style.borderTopWidth = w; el.style.borderBottomWidth = w;
        el.style.borderLeftColor = c; el.style.borderRightColor = c; el.style.borderTopColor = c; el.style.borderBottomColor = c;
    }

    protected static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }

    protected static readonly Color Gold = new Color(1f, 0.80f, 0.26f);
}
