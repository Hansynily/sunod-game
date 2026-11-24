#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using D = vc_TrailerDirector;

/// <summary>TRAILER ONLY. Quest shots: approach, accept, play the room with the best-fit skill.</summary>
public static class vc_TrailerQuests
{
    public static vc_TownNPC Npc(string questId) => vc_TownStage.Current.QuestGivers.FirstOrDefault(n => n.QuestId == questId);

    private static object Get(object o, string f) => o.GetType().GetField(f, D.Any)?.GetValue(o) ?? FindField(o.GetType(), f)?.GetValue(o);
    private static System.Reflection.FieldInfo FindField(System.Type t, string f)
    {
        for (; t != null; t = t.BaseType) { var fi = t.GetField(f, D.Any | System.Reflection.BindingFlags.DeclaredOnly); if (fi != null) return fi; }
        return null;
    }

    /// <summary>Walk up to the giver, Talk, hold on the Help/Later card, press Help.</summary>
    public static IEnumerator ApproachAndAccept(vc_TownNPC npc, Vector2 from, float dialogHold = 1.4f)
    {
        vc_TownStage st = vc_TownStage.Current;
        D.Teleport(from);
        D.HoldCamera(false);
        yield return D.Wait(0.4f);
        yield return D.WalkTo((Vector2)npc.transform.position + new Vector2(0f, -1.1f), 7f);
        FindField(typeof(vc_TownStage), "_promptCooldownUntil")?.SetValue(st, 0f);
        yield return D.Wait(0.6f);   // the glowing Talk! button
        st.OnNpcApproached(npc);
        yield return D.Wait(dialogHold);
        typeof(vc_DialogPanel).GetMethod("OnConfirmClicked", D.Any).Invoke(vc_DialogPanel.Instance, null);
    }

    public static vc_FitQuestBase Fit()
    {
        GameObject room = Room();
        return room != null ? room.GetComponentInChildren<vc_FitQuestBase>(true) : null;
    }

    public static GameObject Room()
    {
        if (vc_TownStage.Current == null) return null;
        return FindField(typeof(vc_TownStage), "_activeRoom")?.GetValue(vc_TownStage.Current) as GameObject;
    }

    public static IEnumerator WaitForQuestStart(float max = 12f)
    {
        float t = 0;
        while (t < max && (vc_QuestTimer.Instance == null || !vc_QuestTimer.Instance.IsRunning() || Fit() == null)) { t += Time.deltaTime; yield return null; }
    }

    /// <summary>In the room: walk into the glowing zone, press the best-fit skill until the quest is done.</summary>
    public static IEnumerator PlayRoom(int stars = 5, float firstPressDelay = 0.3f, string forceSkill = null)
    {
        yield return WaitForQuestStart();
        vc_FitQuestBase fit = Fit();
        if (fit == null) { D.Note = "no fit quest"; yield break; }
        vc_QuestTimer.Instance.SetStarOverride(stars);
        var all = vc_QuestAvailabilityFilter.Instance.GameSettings.skills;
        string tag = fit.QuestProfile != null ? fit.QuestProfile.bestFitTag : null;
        vc_SkillData skill = forceSkill != null ? all.FirstOrDefault(s => s != null && s.skillName == forceSkill)
                           : all.FirstOrDefault(s => s != null && !string.IsNullOrEmpty(tag) && s.HasTag(tag));
        if (skill == null) { D.Note = "no skill for tag " + tag; yield break; }
        var sm = vc_SkillManager.Instance;
        if (sm.GetSkillInCategory(skill.riaSecLetter) != skill) { vc_PlayerInventory.Instance.AddSkill(skill); sm.EquipToCategorySlot(skill); }

        // walk into the zone
        PlayerController p = D.Player;
        vc_SkillZone zone = Room().GetComponentInChildren<vc_SkillZone>(true);
        if (zone != null)
        {
            var col = zone.GetComponent<Collider2D>();
            Vector2 c = col != null ? (Vector2)col.bounds.center : (Vector2)zone.transform.position;
            float t = 0;
            while (t < 5f && Vector2.Distance(p.transform.position, c) > 0.25f)
            {
                p.ScriptedMove = (c - (Vector2)p.transform.position).normalized;
                t += Time.deltaTime; yield return null;
            }
            p.ScriptedMove = Vector2.zero; yield return null; p.ScriptedMove = null;
        }
        yield return D.Wait(firstPressDelay);

        int slot = vc_SkillManager.SlotIndexForLetter(skill.riaSecLetter);
        float tt = 0, next = 0;
        while (tt < 25f && vc_QuestTimer.Instance.FinalResult == null)
        {
            bool done = (bool)FindField(typeof(vc_FitQuestBase), "Done").GetValue(fit);
            bool working = (bool)FindField(typeof(vc_FitQuestBase), "Working").GetValue(fit);
            if (!done && !working && tt >= next) { sm.GetSkillSlot(slot)?.Activate(); next = tt + 0.7f; }
            tt += Time.deltaTime; yield return null;
        }
    }

    /// <summary>On the quest-done sheet: wait, then "Back to town".</summary>
    public static IEnumerator BackToTown(float hold = 2.6f)
    {
        yield return D.Wait(hold);
        var ui = vc_RunProgressUI.Instance;
        var cont = FindField(typeof(vc_RunProgressUI), "_doneContinue")?.GetValue(ui) as System.Action;
        typeof(vc_RunProgressUI).GetMethod("HideQuestDone", D.Any)?.Invoke(ui, null);
        cont?.Invoke();
    }

    // ── the shots ────────────────────────────────────────────────────────

    /// <summary>The whole loop in one take: walk up, Talk, Help, into the room, the skill, stars, back.</summary>
    public static IEnumerator CoreLoop(string name, string town, string questId, Vector2 fromOffset, float seconds)
    {
        yield return D.LoadTown(town);
        D.SetRatio(4);
        vc_TownNPC npc = Npc(questId);
        if (npc == null) { D.Note = "no npc " + questId; yield break; }
        D.Teleport((Vector2)npc.transform.position + fromOffset);
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Shot(name, seconds, CoreLoopAction(npc, fromOffset));
    }

    private static IEnumerator CoreLoopAction(vc_TownNPC npc, Vector2 fromOffset)
    {
        yield return ApproachAndAccept(npc, (Vector2)npc.transform.position + fromOffset);
        yield return PlayRoom(5, 0.5f);
        yield return BackToTown(3.2f);
    }

    /// <summary>Montage: jump straight into a quest room (no dialog) and record the play.</summary>
    public static IEnumerator RoomOnly(string name, string questId, float seconds, float preroll = 0.2f)
    {
        vc_TownNPC npc = Npc(questId);
        if (npc == null) { D.Note = "no npc " + questId; yield break; }
        D.CloseDialogs();
        vc_TownStage.Current.DebugStartQuest(npc);
        yield return WaitForQuestStart();
        yield return D.Wait(preroll);
        yield return D.Shot(name, seconds, PlayRoom(5, 0.6f));
        // leave: finish the sheet so the next quest can start
        yield return D.Wait(0.5f);
        yield return BackToTown(0.1f);
        yield return D.Wait(2.5f);
    }

    public static IEnumerator Montage()
    {
        yield return D.LoadTown("Town_Elementary"); D.SetRatio(4);
        yield return RoomOnly("q_drum_A", "e_a6_drum", 9f);
        yield return RoomOnly("q_pump_R", "r_pump", 9f);
        yield return D.LoadTown("Town_HighSchool"); D.SetRatio(4);
        yield return RoomOnly("q_sparrow_S", "L3_FallenSparrow", 9f);
        yield return RoomOnly("q_ledger_C", "h_c8_ledger", 9f);
        yield return RoomOnly("q_pitch_E", "h_e1_pitch", 9f);
        yield return D.LoadTown("Town_College"); D.SetRatio(4);
        yield return RoomOnly("q_mushrooms_I", "i5_mushrooms", 9f);
    }

    public static IEnumerator CoreAndMontage()
    {
        yield return Core();
        yield return Montage();
    }

    public static IEnumerator Core()
    {
        // Nico at the beach: walk in from the west along the sand
        yield return CoreLoop("core_a", "Town_Elementary", "e_i6_fish", new Vector2(-5f, -1.5f), 24f);
    }
}
#endif
