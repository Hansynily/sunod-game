using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skill shrine in each town - the town version of the corridor's hallway markers.
/// Walk up to it to be offered 3 skills you don't have yet; the one you pick goes into
/// its kind's slot (with the usual Replace prompt). Usable once per day.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class vc_TownSkillShrine : MonoBehaviour
{
    public SpriteRenderer glow;
    private int _usedOnDay = -1;

    private void Update()
    {
        if (glow == null) return;
        bool ready = IsReady();
        Color c = glow.color;
        c.a = ready ? 0.55f + 0.35f * Mathf.Sin(Time.time * 3f) : 0.15f;
        glow.color = c;
    }

    private bool IsReady()
    {
        vc_TownStage stage = vc_TownStage.Current;
        return stage == null || stage.Day != _usedOnDay;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        vc_TownStage stage = vc_TownStage.Current;
        if (stage != null && (stage.InQuest || stage.Busy)) return;
        if (vc_DialogPanel.Instance != null && vc_DialogPanel.Instance.IsOpen) return;

        if (!IsReady())
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("The shrine is resting. Come back tomorrow.", 2f);
            return;
        }

        vc_SkillData[] picks = DrawPicks(3);
        if (picks.Length == 0 || vc_SkillAwardPopup.Instance == null)
        {
            vc_QuestHUD.Instance?.ShowFeedbackTimed("You already know every skill!", 2f);
            return;
        }

        vc_SkillAwardPopup.Instance.Show(picks, chosen =>
        {
            if (chosen == null) return;
            _usedOnDay = stage != null ? stage.Day : 0;
            vc_PlayerInventory.Instance?.AddSkill(chosen);
            vc_SkillManager.Instance?.OfferEquip(chosen);
        });
    }

    /// <summary>Random skills the player doesn't own, preferring kinds the town's open quests use.</summary>
    private static vc_SkillData[] DrawPicks(int count)
    {
        vc_QuestAvailabilityFilter filter = vc_QuestAvailabilityFilter.Instance
            ?? FindFirstObjectByType<vc_QuestAvailabilityFilter>(FindObjectsInactive.Include);
        vc_SkillData[] all = filter != null && filter.GameSettings != null ? filter.GameSettings.skills : null;
        if (all == null) return new vc_SkillData[0];

        var openLetters = new HashSet<string>();
        if (vc_TownStage.Current != null)
            foreach (vc_TownNPC npc in vc_TownStage.Current.GetComponentsInChildren<vc_TownNPC>())
                if (npc.HasQuest && !npc.Attempted && !string.IsNullOrEmpty(npc.questCode))
                    openLetters.Add(npc.questCode.Substring(0, 1).ToUpperInvariant());

        var pool = new List<(vc_SkillData s, int w)>();
        foreach (vc_SkillData s in all)
        {
            if (s == null) continue;
            if (vc_PlayerInventory.Instance != null && vc_PlayerInventory.Instance.HasSkill(s)) continue;
            bool wanted = !string.IsNullOrEmpty(s.riaSecLetter) && openLetters.Contains(s.riaSecLetter.ToUpperInvariant());
            pool.Add((s, wanted ? 3 : 1));
        }

        var picks = new List<vc_SkillData>();
        while (picks.Count < count && pool.Count > 0)
        {
            int total = 0;
            foreach (var p in pool) total += p.w;
            int roll = Random.Range(0, total), acc = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                acc += pool[i].w;
                if (roll < acc) { picks.Add(pool[i].s); pool.RemoveAt(i); break; }
            }
        }
        return picks.ToArray();
    }
}
