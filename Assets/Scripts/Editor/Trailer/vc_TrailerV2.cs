#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using D = vc_TrailerDirector;
using Q = vc_TrailerQuests;

/// <summary>TRAILER ONLY. Second-pass shots: a core loop with the giver on screen, clean stage changes.</summary>
public static class vc_TrailerV2
{
    /// <summary>Every frame for 'seconds': no dialogs, no HUD (new UI that loads with a town is hidden too).</summary>
    public static IEnumerator KeepClean(float seconds, bool hud)
    {
        float t = 0;
        while (t < seconds)
        {
            D.CloseDialogs();
            if (hud) D.HideUi(true);
            D.HideControls();
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    /// <summary>Core loop with a giver who really has a request today (visible, with the "!").</summary>
    public static IEnumerator CoreVisible(string name, string[] prefer, Vector2 fromOffset)
    {
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        var today = vc_TownStage.Current.TodayRequests;
        vc_TownNPC npc = today.FirstOrDefault(n => prefer.Contains(n.QuestId)) ?? today.FirstOrDefault();
        if (npc == null) { D.Note = "no request today"; yield break; }
        D.Note = "core npc " + npc.QuestId + " " + npc.npcName;
        D.Teleport((Vector2)npc.transform.position + fromOffset);
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Wait(0.3f);
        yield return D.Shot(name, 24f, CoreAction(npc, fromOffset));
    }

    private static IEnumerator CoreAction(vc_TownNPC npc, Vector2 fromOffset)
    {
        yield return D.Wait(0.6f);
        yield return Q.ApproachAndAccept(npc, (Vector2)npc.transform.position + fromOffset, 1.6f);
        yield return Q.PlayRoom(5, 0.5f);
        yield return Q.BackToTown(3.2f);
    }

    /// <summary>The cube wall between two towns, camera on each plaza, no HUD and no panels.</summary>
    public static IEnumerator StageChange(string name, string from, string to, Vector2 camFrom, Vector2 camTo)
    {
        yield return D.LoadTown(from);
        D.SetRatio(3);
        D.HideUi(true);
        D.HoldCamera(true);
        D.PlaceCam(camFrom);
        D.Teleport(camFrom + new Vector2(0f, -2f));
        yield return D.Wait(0.6f);
        vc_OfflineSession.Run(KeepClean(7f, true));
        yield return D.Shot(name, 5f, ChangeAction(to, camTo));
        yield return D.Wait(0.5f);
        D.HideUi(false);
        D.HoldCamera(false);
    }

    private static IEnumerator ChangeAction(string to, Vector2 camTo)
    {
        yield return D.Wait(1.0f);
        yield return vc_PixelTransition.Cover(0.8f);
        vc_TownFlow.Instance.LoadTown(to);
        float t = 0;
        while ((vc_TownStage.Current == null || vc_TownStage.Current.gameObject.scene.name != to) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
        D.HoldCamera(true);
        D.PlaceCam(camTo);
        D.Teleport(camTo + new Vector2(0f, -2f));
        for (int i = 0; i < 30; i++) { D.HoldCamera(true); D.PlaceCam(camTo); yield return null; }
    }

    /// <summary>The plaza with today's "!" requests, HUD hidden, slow drift.</summary>
    public static IEnumerator FlyRequests(string name)
    {
        yield return D.LoadTown("Town_Elementary");
        D.HideUi(true);
        D.SetRatio(3);
        D.HoldCamera(true);
        var pts = new[] { new Vector2(24f, 24f), new Vector2(33f, 25f) };
        D.PlaceCam(pts[0]);
        yield return D.Wait(0.5f);
        yield return D.Shot(name, 6f, D.Fly(pts, 6f));
        D.HideUi(false); D.HoldCamera(false);
    }

    public static IEnumerator ArtisticAlt()
    {
        yield return D.LoadTown("Town_Elementary"); D.SetRatio(4);
        yield return Q.RoomOnly("q_song_A", "e_a4_song", 9f);
        yield return Q.RoomOnly("q_poster_A", "a3_poster", 9f);
    }

    public static IEnumerator Remember() { yield return D.LoadTown("Town_HighSchool"); yield return vc_TrailerLife.Remember("remember"); }

    public static IEnumerator Wake() { yield return D.LoadTown("Town_HighSchool"); yield return vc_TrailerLife.WakeUp("wake_loop2"); }

    // ── core take with the skill wheel opened in the room ─────────────────

    private static System.Reflection.FieldInfo Fld(System.Type t, string f)
    {
        for (; t != null; t = t.BaseType) { var fi = t.GetField(f, D.Any | System.Reflection.BindingFlags.DeclaredOnly); if (fi != null) return fi; }
        return null;
    }

    public static IEnumerator CoreSkills(string name)
    {
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        var today = vc_TownStage.Current.TodayRequests;
        vc_TownNPC npc = vc_TrailerQuests.Npc("e_a6_drum");
        if (!today.Contains(npc))
        {   // make Paolo one of today's requests (visible, with his "!")
            var list = Fld(typeof(vc_TownStage), "_today").GetValue(vc_TownStage.Current) as System.Collections.Generic.List<vc_TownNPC>;
            list.Add(npc);
            npc.SetVisible(true);
            npc.PlaceAt(npc.HomePosition);
            typeof(vc_TownStage).GetMethod("RefreshHomes", D.Any)?.Invoke(vc_TownStage.Current, null);
        }
        // one skill of every colour, so the wheel is full (the room's best fit goes in its slot later)
        var all = vc_QuestAvailabilityFilter.Instance.GameSettings.skills;
        foreach (string l in new[] { "R", "I", "A", "S", "E", "C" })
        {
            var s = all.FirstOrDefault(k => k != null && k.riaSecLetter == l && !vc_SkillLevels.IsRare(k));
            if (s != null) { vc_PlayerInventory.Instance.AddSkill(s); vc_SkillManager.Instance.EquipToCategorySlot(s); }
        }
        Vector2 from = (Vector2)npc.transform.position + new Vector2(-4.5f, -1.4f);
        D.Teleport(from);
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Wait(0.3f);
        D.Note = "core npc " + npc.QuestId;
        yield return D.Shot(name, 24f, CoreSkillsAction(npc, from));
    }

    private static IEnumerator CoreSkillsAction(vc_TownNPC npc, Vector2 from)
    {
        yield return D.Wait(0.6f);
        yield return Q.ApproachAndAccept(npc, from, 1.6f);
        yield return Q.WaitForQuestStart();
        vc_FitQuestBase fit = Q.Fit();
        vc_QuestTimer.Instance.SetStarOverride(5);
        var all = vc_QuestAvailabilityFilter.Instance.GameSettings.skills;
        vc_SkillData best = all.FirstOrDefault(s => s != null && s.HasTag(fit.QuestProfile.bestFitTag));
        var sm = vc_SkillManager.Instance;
        if (sm.GetSkillInCategory(best.riaSecLetter) != best) { vc_PlayerInventory.Instance.AddSkill(best); sm.EquipToCategorySlot(best); }
        // into the glowing zone
        PlayerController p = D.Player;
        vc_SkillZone zone = Q.Room().GetComponentInChildren<vc_SkillZone>(true);
        var col = zone.GetComponent<Collider2D>();
        Vector2 c = col != null ? (Vector2)col.bounds.center : (Vector2)zone.transform.position;
        float t = 0;
        while (t < 5f && Vector2.Distance(p.transform.position, c) > 0.25f) { p.ScriptedMove = (c - (Vector2)p.transform.position).normalized; t += Time.deltaTime; yield return null; }
        p.ScriptedMove = Vector2.zero; yield return null; p.ScriptedMove = null;
        yield return D.Wait(0.4f);
        // open the wheel, look at the choices, pick the best fit
        var hud = Object.FindFirstObjectByType<SunodGame.UI.GameHUDUIToolkit>();
        var radial = Fld(typeof(SunodGame.UI.GameHUDUIToolkit), "_radial").GetValue(hud);
        radial.GetType().GetMethod("Open").Invoke(radial, null);
        yield return D.Wait(2.2f);
        radial.GetType().GetMethod("Close").Invoke(radial, null);
        int slot = vc_SkillManager.SlotIndexForLetter(best.riaSecLetter);
        float tt = 0, next = 0;
        while (tt < 20f && vc_QuestTimer.Instance.FinalResult == null)
        {
            bool done = (bool)Fld(typeof(vc_FitQuestBase), "Done").GetValue(fit);
            bool working = (bool)Fld(typeof(vc_FitQuestBase), "Working").GetValue(fit);
            if (!done && !working && tt >= next) { sm.GetSkillSlot(slot)?.Activate(); next = tt + 0.7f; }
            tt += Time.deltaTime; yield return null;
        }
        yield return Q.BackToTown(3.2f);
    }

    public static IEnumerator All()
    {
        yield return CoreVisible("core_b", new[] { "e_i6_fish", "r2_stones", "e_a6_drum", "e_e2_snacks" }, new Vector2(-4.5f, -1.4f));
        string n1 = D.Note;
        yield return CoreVisible("core_c", new[] { "e_e2_snacks", "e_a6_drum", "r_pump" }, new Vector2(4.5f, -1.4f));
        string n2 = D.Note;
        yield return StageChange("stage_elem_hs", "Town_Elementary", "Town_HighSchool", new Vector2(28f, 22.5f), new Vector2(30f, 21.5f));
        yield return StageChange("stage_hs_college", "Town_HighSchool", "Town_College", new Vector2(30f, 21.5f), new Vector2(31f, 22.5f));
        yield return FlyRequests("fly_requests");
        D.Note = n1 + " | " + n2;
    }
}
#endif
