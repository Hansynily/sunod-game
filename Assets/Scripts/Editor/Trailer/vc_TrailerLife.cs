#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using SunodGame.Models;
using D = vc_TrailerDirector;
using Q = vc_TrailerQuests;

/// <summary>TRAILER ONLY. Growing up, town life, the loop and the results, staged at runtime offline.</summary>
public static class vc_TrailerLife
{
    private static System.Reflection.FieldInfo F(System.Type t, string f)
    {
        for (; t != null; t = t.BaseType) { var fi = t.GetField(f, D.Any | System.Reflection.BindingFlags.DeclaredOnly); if (fi != null) return fi; }
        return null;
    }
    private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, D.Any).Invoke(o, a);
    private static vc_SkillData Skill(string name) => vc_QuestAvailabilityFilter.Instance.GameSettings.skills.FirstOrDefault(s => s != null && s.skillName == name);
    private static vc_SkillData SkillTag(string tag) => vc_QuestAvailabilityFilter.Instance.GameSettings.skills.FirstOrDefault(s => s != null && s.HasTag(tag));
    private static vc_SkillData[] Skills(string letter) => vc_QuestAvailabilityFilter.Instance.GameSettings.skills.Where(s => s != null && s.riaSecLetter == letter && !vc_SkillLevels.IsRare(s)).ToArray();

    // ── loop data for the loop-2 shots ────────────────────────────────────

    public static LoopMetaResponse Loop2Meta()
    {
        var m = vc_OfflineSession.FreshLoop();
        m.current_loop = 2; m.loops_completed = 1; m.threat = "typhoon"; m.current_finished = false;
        m.kept_skills = new List<string> { "BALAK", "SURI" };
        var survey = SkillTag("survey");
        if (survey != null && !m.kept_skills.Contains(survey.skillName)) m.kept_skills.Add(survey.skillName);
        m.owned_mementos = new List<string> { "compass", "lantern", "ribbon", "shell" };
        m.carried_mementos = new List<string> { "compass", "lantern", "ribbon" };
        m.pockets = 3; m.max_pockets = 4; m.board_unlocked = true;
        m.history = new List<LoopHistoryItem>
        {
            new LoopHistoryItem { loop = 1, threat = "typhoon", holland_code = "AS", career = "Arts & Design", passed = 11, failed = 2,
                kept_skills = new List<string> { "BALAK", "SURI" }, mementos = new List<string> { "ribbon" }, stars_earned = 41,
                change_line = "Your first childhood." },
            new LoopHistoryItem { loop = 2, threat = "flood", holland_code = "SIA", career = "Healthcare", passed = 13, failed = 1,
                kept_skills = new List<string> { "BALAK", "SURI" }, mementos = new List<string> { "compass", "lantern" }, stars_earned = 52,
                changed = true, change_line = "This time you helped more people up close." },
        };
        m.paths = new LoopPaths { differ = true, baseline = new LoopPath { loop = 1, holland_code = "AS", career = "Arts & Design" },
            latest = new LoopPath { loop = 2, holland_code = "SIA", career = "Healthcare" },
            change_line = "Your path moved from Arts & Design to Healthcare." };
        return m;
    }

    // ── growing up ────────────────────────────────────────────────────────

    /// <summary>Same walk in each town (the uniform changes with the school): for a match cut.</summary>
    public static IEnumerator WalkIn(string name, string town, Vector2 from, Vector2 to, float seconds)
    {
        yield return D.LoadTown(town);
        D.SetRatio(6);
        D.Teleport(from);
        D.HoldCamera(false);
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Shot(name, seconds, D.WalkTo(to, seconds + 1f, 0.1f));
    }

    /// <summary>Elementary -> High School (or onward) with the voxel cube wall closing and opening.</summary>
    public static IEnumerator TownChange(string name, string from, string to, Vector2 at)
    {
        yield return D.LoadTown(from);
        D.SetRatio(4);
        D.Teleport(at);
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Shot(name, 5.5f, TownChangeAction(to));
        yield return D.Wait(1.0f);
        D.CloseDialogs();
    }

    private static IEnumerator TownChangeAction(string to)
    {
        yield return D.Wait(0.8f);
        yield return vc_PixelTransition.Cover(0.8f);
        vc_TownFlow.Instance.LoadTown(to);
        float t = 0;
        while ((vc_TownStage.Current == null || vc_TownStage.Current.gameObject.scene.name != to) && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
        D.HideControls();
        yield return D.Wait(0.4f);
        D.CloseDialogs();
    }

    /// <summary>The night pick with a RARE card (gold frame), picked.</summary>
    public static IEnumerator NightRare(string name)
    {
        yield return D.LoadTown("Town_HighSchool");
        D.SetRatio(4);
        yield return D.Wait(0.5f);
        var rare = Skill("BALAK") ?? SkillTag("plan");
        var s = Skills("S"); var e = Skills("E");
        yield return D.Shot(name, 6.5f, NightRareAction(new[] { s[0], rare, e[0] }));
    }

    private static IEnumerator NightRareAction(vc_SkillData[] cards)
    {
        vc_RunProgressUI.Instance.ShowNight(1);
        yield return D.Wait(0.5f);
        vc_SkillAwardPopup.Instance.Show(cards, _ => { }, null);
        yield return D.Wait(2.6f);
        Call(vc_SkillAwardPopup.Instance, "OnCardSelected", 1);
        yield return D.Wait(1.1f);
        Call(vc_SkillAwardPopup.Instance, "OnConfirmClicked");
        yield return D.Wait(1.5f);
        vc_RunProgressUI.Instance.HideNight();
    }

    /// <summary>A quest finished with a skill one XP short of Lv2 ("Lv2!" stamp) or at Lv3 ("Mastered!").</summary>
    public static IEnumerator LevelQuest(string name, string town, string questId, bool mastered)
    {
        yield return D.LoadTown(town);
        D.SetRatio(4);
        vc_TownNPC npc = Q.Npc(questId);
        var all = vc_QuestAvailabilityFilter.Instance.GameSettings.skills;
        var prof = npc.questPrefab.GetComponentInChildren<vc_FitQuestBase>(true).QuestProfile;
        vc_SkillData best = all.FirstOrDefault(s => s != null && s.HasTag(prof.bestFitTag));
        // Lv2: the best fit, one XP short. Mastered: a same-colour skill (not the best fit) at Lv3.
        vc_SkillData sk = mastered ? all.FirstOrDefault(s => s != null && s.riaSecLetter == best.riaSecLetter && !s.HasTag(prof.bestFitTag) && !vc_SkillLevels.IsRare(s)) : best;
        vc_PlayerInventory.Instance.AddSkill(sk); vc_SkillManager.Instance.EquipToCategorySlot(sk);
        int want = mastered ? vc_SkillLevels.Lv3Xp : vc_SkillLevels.Lv2Xp - 1;
        int have = vc_SkillLevels.Xp(sk);
        if (want > have) vc_SkillLevels.AddXp(sk, want - have);
        yield return D.Wait(2.5f);
        vc_TownStage.Current.DebugStartQuest(npc);
        yield return Q.WaitForQuestStart();
        yield return D.Shot(name, 10f, Q.PlayRoom(5, 0.4f, sk.skillName));
        yield return Q.BackToTown(0.2f);
        yield return D.Wait(2f);
    }

    // ── town life ─────────────────────────────────────────────────────────

    public static IEnumerator Fiesta(string nameHud, string namePan)
    {
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        D.Teleport(new Vector2(28f, 20.5f));
        yield return D.Wait(0.8f);
        D.CloseDialogs();
        vc_TownEvents.Current.DebugForceToday("fiesta");
        yield return D.Shot(nameHud, 6f, CloseDialogAfter(3.8f));
        D.CloseDialogs();
        yield return D.Wait(1f);
        D.HideUi(true);
        D.SetRatio(4);
        yield return D.Shot(namePan, 8f, D.Fly(new[] { new Vector2(23.5f, 24f), new Vector2(32.5f, 23f) }, 8f));
        D.HideUi(false); D.HoldCamera(false);
    }

    private static IEnumerator CloseDialogAfter(float s) { yield return D.Wait(s); D.CloseDialogs(); }

    /// <summary>A town event scene (gathering crowd or a visit), lines advanced on a beat.</summary>
    public static IEnumerator EventScene(string name, string eventId, float seconds)
    {
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        vc_TownFlow.ForgetQuestIds(id => id.StartsWith("ev:"));
        var ev = vc_TownEvents.Current;
        ev.DebugForceToday(eventId);
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Shot(name, seconds, EventAction(ev));
        // finish the scene quietly
        for (int i = 0; i < 12 && vc_DialogPanel.Instance.IsOpen; i++) { Confirm(); yield return D.Wait(0.4f); }
        yield return D.Wait(1.5f);
    }

    private static void Confirm() { if (vc_DialogPanel.Instance.IsOpen) Call(vc_DialogPanel.Instance, "OnConfirmClicked"); }

    private static IEnumerator EventAction(vc_TownEvents ev)
    {
        ev.StartCoroutine((IEnumerator)Call(ev, "RunEvent"));
        yield return D.Wait(2.4f);
        for (int i = 0; i < 10; i++) { Confirm(); yield return D.Wait(1.9f); }
    }

    public static IEnumerator Letter(string name)
    {
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        var ev = vc_TownEvents.Current;
        ev.DebugForceToday("letter");
        var letter = F(typeof(vc_TownEvents), "_letterGo").GetValue(ev) as GameObject;
        if (letter == null) { D.Note = "no letter"; yield break; }
        Vector2 lp = letter.transform.position;
        D.Teleport(lp + new Vector2(-2.6f, -0.4f));
        yield return D.Wait(3.2f);   // let the "letter by your door" toast pass
        D.CloseDialogs();
        yield return D.Shot(name, 7f, LetterAction(lp));
        D.CloseDialogs();
    }

    private static IEnumerator LetterAction(Vector2 lp)
    {
        yield return D.Wait(0.5f);
        yield return D.WalkTo(lp, 4f, 0.2f);
        yield return D.Wait(0.4f);
        var ev = vc_TownEvents.Current;
        if (F(typeof(vc_TownEvents), "_letterGo").GetValue(ev) != null) Call(ev, "OpenLetter");
    }

    public static IEnumerator Cat(string name)
    {
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        GameObject cat = GameObject.Find("TownCat");
        if (cat == null) { D.Note = "no cat"; yield break; }
        Vector2 cp = cat.transform.position;
        D.Teleport(cp + new Vector2(-3f, -0.6f));
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Shot(name, 6f, CatAction(cp));
    }

    private static IEnumerator CatAction(Vector2 cp)
    {
        yield return D.WalkTo(cp + new Vector2(-0.9f, -0.2f), 4f, 0.15f);
        yield return D.Wait(0.4f);
        var critters = vc_TownStage.Current.GetComponent<vc_TownCritters>();
        critters.TryPet(cp + new Vector2(0f, 0.3f), cp);
    }

    public static IEnumerator Board(string name)
    {
        vc_LoopMeta.Set(Loop2Meta());
        F(typeof(vc_TownStage), "WakeUpShownForLoop").SetValue(null, 2);
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        Transform b = vc_TownStage.Current.transform.Find("Spots/board");
        Vector2 bp = b != null ? (Vector2)b.position : new Vector2(24.8f, 25.2f);
        D.Teleport(bp + new Vector2(2.2f, -1.6f));
        yield return D.Wait(1.0f);
        D.CloseDialogs();
        yield return D.Shot(name, 6f, BoardAction(bp));
        if (vc_QuestBoard.Current != null) vc_QuestBoard.Current.Close();
    }

    private static IEnumerator BoardAction(Vector2 bp)
    {
        yield return D.WalkTo(bp + new Vector2(0f, -1.0f), 3f, 0.15f);
        yield return D.Wait(0.3f);
        Call(vc_TownEvents.Current, "ReadBoard");
    }

    public static IEnumerator BeachHut(string name)
    {
        yield return D.LoadTown("Town_Elementary");
        D.HideUi(true);
        D.SetRatio(4);
        Transform spots = vc_TownStage.Current.transform.Find("Spots");
        Vector2 hut = new Vector2(36.4f, 7.6f);
        // send the roamers to the hut
        var life = vc_TownStage.Current.GetComponent<vc_TownLife>();
        var roamers = F(typeof(vc_TownLife), "_roamers")?.GetValue(life) as IList;
        if (roamers != null)
        {
            int k = 0;
            foreach (object r in roamers)
            {
                var go = F(r.GetType(), "go").GetValue(r) as GameObject;
                if (go == null || k > 4) continue;
                go.transform.position = hut + new Vector2(-2.5f + k * 1.3f, -1.2f + (k % 2) * 0.7f);
                k++;
            }
        }
        yield return D.Wait(0.6f);
        yield return D.Shot(name, 7f, D.Fly(new[] { hut + new Vector2(-2.2f, 0.6f), hut + new Vector2(1.8f, 0.2f) }, 7f));
        D.HideUi(false); D.HoldCamera(false);
    }

    // ── the loop ──────────────────────────────────────────────────────────

    public static IEnumerator Remember(string name)
    {
        vc_LoopMeta.Set(Loop2Meta());
        F(typeof(vc_TownStage), "WakeUpShownForLoop").SetValue(null, 2);
        yield return D.LoadTown("Town_Elementary");
        D.HideUi(true, true);
        D.CloseDialogs();
        var cands = new List<vc_SkillData>();
        foreach (string l in new[] { "R", "I", "A", "S", "E", "C" }) { var s = Skills(l); if (s.Length > 0) cands.Add(s[0]); }
        var rare = Skill("BALAK"); if (rare != null) cands.Insert(2, rare);
        yield return D.Shot(name, 8f, RememberAction(cands));
        D.HideUi(false);
    }

    private static IEnumerator RememberAction(List<vc_SkillData> cands)
    {
        var lm = vc_LoopMoments.Instance;
        var root = lm.GetComponent<UIDocument>().rootVisualElement;
        lm.ShowRemember(cands, 3, new List<string> { cands[2].skillName }, (held, reply) => reply(null), () => { });
        yield return D.Wait(1.2f);
        // tap two more cards (they glow violet)
        // the skill cards: clickable, 150 px wide, inside a non-clickable slot
        var cards = root.Query<VisualElement>().Where(v => v.pickingMode == PickingMode.Position && v.parent != null
            && v.parent.childCount == 1 && v.parent.pickingMode == PickingMode.Ignore
            && v.parent.parent != null && v.parent.parent.style.flexWrap == Wrap.Wrap).ToList();
        D.Note = "remember cards " + cards.Count;
        foreach (int idx in new[] { 0, 4 })
        {
            if (idx < cards.Count) { using (var e = ClickEvent.GetPooled()) { e.target = cards[idx]; cards[idx].SendEvent(e); } }
            yield return D.Wait(0.9f);
        }
        yield return D.Wait(1.4f);
        D.Press("Remember");
    }

    /// <summary>The real LOOP 2 wake-up in the Elementary town: cards, keepsakes, Begin, cards fly to the slots.</summary>
    public static IEnumerator WakeUp(string name)
    {
        vc_LoopMeta.Set(Loop2Meta());
        F(typeof(vc_TownStage), "WakeUpShownForLoop").SetValue(null, 0);
        D.SetRatio(4);
        if (!vc_OfflineSession.Active) vc_OfflineSession.Start(true);
        vc_LoopMeta.Set(Loop2Meta());
        vc_FairMode.Enabled = false;
        vc_TownFlow.PendingStartTown = "Town_Elementary";
        vc_TownFlow.PendingCompletedQuestIds = new List<string>();
        SceneManager.LoadScene("Game_Scene_Town");
        float t = 0;
        while ((vc_TownStage.Current == null || vc_TownStage.Current.gameObject.scene.name != "Town_Elementary") && t < 20f) { t += Time.unscaledDeltaTime; yield return null; }
        D.HideControls();
        yield return D.Shot(name, 11f, WakeAction());
    }

    private static IEnumerator WakeAction()
    {
        vc_OfflineSession.Run(vc_TrailerV2.KeepClean(11.5f, false));   // no joystick, no intro panel
        yield return D.Wait(5.2f);
        D.Press("Begin");
    }

    /// <summary>Loop 2: Nico notices the Old Compass you carry.</summary>
    public static IEnumerator Noticed(string name)
    {
        vc_LoopMeta.Set(Loop2Meta());
        F(typeof(vc_TownStage), "WakeUpShownForLoop").SetValue(null, 2);
        var shown = F(typeof(vc_TownStage), "MemoryOpenersShown").GetValue(null) as HashSet<string>;
        shown?.Clear();
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        vc_TownNPC npc = Q.Npc("e_i6_fish");
        D.Teleport((Vector2)npc.transform.position + new Vector2(-3.5f, -1.4f));
        yield return D.Wait(1f);
        D.CloseDialogs();
        yield return D.Shot(name, 7f, NoticeAction(npc));
        D.CloseDialogs();
    }

    private static IEnumerator NoticeAction(vc_TownNPC npc)
    {
        yield return D.WalkTo((Vector2)npc.transform.position + new Vector2(0f, -1.1f), 5f);
        F(typeof(vc_TownStage), "_promptCooldownUntil")?.SetValue(vc_TownStage.Current, 0f);
        yield return D.Wait(0.4f);
        vc_TownStage.Current.OnNpcApproached(npc);
    }

    // ── results ───────────────────────────────────────────────────────────

    public static IEnumerator Results(string name)
    {
        vc_LoopMeta.Set(Loop2Meta());
        vc_TownFlow.ResetRun();
        var g = typeof(SunodGame.Core.GameSessionData);
        void Set(string f, object v) => g.GetField(f, D.Any).SetValue(null, v);
        Set("hollandCode", "SIA"); Set("predictedClusterHollandCode", "SIA");
        Set("careerResult", "Healthcare"); Set("careerFamily", "Healthcare");
        Set("predictedCareerResult", "Healthcare");
        Set("riasecScores", JsonUtility.FromJson<RiasecScoresDto>("{\"r\":5,\"i\":11,\"a\":9,\"s\":14,\"e\":6,\"c\":4}"));
        Set("rounds_attempted", 14); Set("rounds_cleared", 13); Set("total_stars", 52); Set("partialRunNote", "");
        SceneManager.LoadScene("EndScene");
        yield return D.Wait(1.0f);
        D.HideControls();
        yield return D.Shot(name, 9f);
    }

    public static IEnumerator Progress(string name)
    {
        vc_LoopMeta.Set(Loop2Meta());
        SceneManager.LoadScene("MainMenu");
        yield return D.Wait(2.0f);
        vc_LoopMeta.Set(Loop2Meta());
        D.CloseDialogs();
        var menu = Object.FindFirstObjectByType<SunodGame.UI.MainMenuUIToolkit>();
        if (menu == null) { D.Note = "no menu"; yield break; }
        Call(menu, "OnStatsClicked", new object[] { null });
        yield return D.Wait(0.6f);
        var resp = new PlayerProgressResponse { has_data = true, quests_completed = 24, quests_attempted = 27, total_stars = 93,
            predicted_career_result = "Healthcare", predicted_career_family = "Healthcare", predicted_cluster_label = "Healthcare",
            predicted_holland_code = "SIA", riasec_scores = JsonUtility.FromJson<RiasecScoresDto>("{\"r\":5,\"i\":11,\"a\":9,\"s\":14,\"e\":6,\"c\":4}") };
        try { Call(menu, "SetStatsError", ""); } catch { }
        try { Call(menu, "ApplyProgressResponse", resp); } catch (System.Exception ex) { D.Note = "apply: " + ex.Message; }
        yield return D.Wait(0.5f);
        yield return D.Shot(name, 10f, ScrollDown(menu, 1.5f, 6.5f));
    }

    private static IEnumerator ScrollDown(Object menu, float delay, float seconds)
    {
        yield return D.Wait(delay);
        ScrollView best = null; float bestH = 0;
        foreach (UIDocument d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            if (d.rootVisualElement != null)
                foreach (ScrollView sv in d.rootVisualElement.Query<ScrollView>().ToList())
                {
                    if (sv.worldBound.height <= 0 || sv.resolvedStyle.display == DisplayStyle.None) continue;
                    float h = sv.contentContainer.layout.height - sv.contentViewport.layout.height;
                    if (h > bestH) { bestH = h; best = sv; }
                }
        if (best == null) { D.Note = "no scroll"; yield break; }
        float t = 0;
        while (t < seconds)
        {
            float u = t / seconds; u = u * u * (3f - 2f * u);
            best.scrollOffset = new Vector2(0f, Mathf.Round(bestH * u / 4f) * 4f);
            t += Time.deltaTime; yield return null;
        }
        best.scrollOffset = new Vector2(0f, bestH);
    }

    // ── batches ───────────────────────────────────────────────────────────

    public static IEnumerator GrowingUp()
    {
        yield return WalkIn("walk_elem", "Town_Elementary", new Vector2(33f, 22.6f), new Vector2(41f, 22.6f), 4f);
        yield return WalkIn("walk_hs", "Town_HighSchool", new Vector2(36.5f, 21.4f), new Vector2(44.5f, 21.6f), 4f);
        yield return WalkIn("walk_college", "Town_College", new Vector2(37f, 23.6f), new Vector2(45f, 24f), 4f);
        yield return TownChange("change_elem_hs", "Town_Elementary", "Town_HighSchool", new Vector2(28f, 20.5f));
        yield return TownChange("change_hs_college", "Town_HighSchool", "Town_College", new Vector2(30f, 19.5f));
        yield return NightRare("night_rare");
        yield return LevelQuest("lv2_quest", "Town_HighSchool", "h_i8_riverbed", false);
        yield return LevelQuest("mastered_quest", "Town_College", "c_e5_team", true);
    }

    public static IEnumerator Retakes()
    {
        yield return WalkIn("walk_elem", "Town_Elementary", new Vector2(33f, 22.6f), new Vector2(39f, 22.6f), 3.5f);
        yield return WalkIn("walk_hs", "Town_HighSchool", new Vector2(36.5f, 21.4f), new Vector2(42.5f, 21.6f), 3.5f);
        yield return WalkIn("walk_college", "Town_College", new Vector2(37f, 23.6f), new Vector2(43f, 24f), 3.5f);
        yield return LevelQuest("mastered_quest", "Town_College", "c_e5_team", true);
    }

    public static IEnumerator Retakes2()
    {
        yield return Letter("letter");
        yield return Board("board");
        yield return Remember("remember");
        yield return WakeUp("wake_loop2");
        yield return Progress("progress");
    }

    /// <summary>The skill wheel opened with one skill of every colour.</summary>
    public static IEnumerator Radial(string name)
    {
        F(typeof(vc_TownStage), "WakeUpShownForLoop").SetValue(null, 99);
        vc_LoopMeta.Set(vc_OfflineSession.FreshLoop());
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        D.Teleport(new Vector2(28f, 21f));
        yield return D.Wait(1.5f);
        if (vc_SkillAwardPopup.Instance != null) { try { Call(vc_SkillAwardPopup.Instance, "Hide"); } catch { } }
        D.CloseDialogs();
        foreach (string l in new[] { "R", "I", "A", "S", "E", "C" })
        {
            var s = Skills(l); if (s.Length == 0) continue;
            vc_PlayerInventory.Instance.AddSkill(s[0]); vc_SkillManager.Instance.EquipToCategorySlot(s[0]);
        }
        yield return D.Wait(0.8f);
        yield return D.Shot(name, 5f, RadialAction());
    }

    private static IEnumerator RadialAction()
    {
        yield return D.Wait(0.8f);
        var hud = Object.FindFirstObjectByType<SunodGame.UI.GameHUDUIToolkit>();
        var radial = F(typeof(SunodGame.UI.GameHUDUIToolkit), "_radial").GetValue(hud);
        radial.GetType().GetMethod("Open").Invoke(radial, null);
        yield return D.Wait(3.2f);
        radial.GetType().GetMethod("Close").Invoke(radial, null);
    }

    /// <summary>The stage-end dream (Elementary).</summary>
    public static IEnumerator Dream(string name)
    {
        yield return D.LoadTown("Town_Elementary");
        D.SetRatio(4);
        yield return D.Wait(0.5f);
        D.CloseDialogs();
        var stages = vc_TownFlow.Instance.Stages;
        yield return D.Shot(name, 8f, DreamAction(stages[0].title, stages[0].dreamAfter));
    }

    private static IEnumerator DreamAction(string title, string[] lines)
    {
        vc_RunProgressUI.Instance.PlayDream(title, lines, 0, null);
        yield return null;
    }

    public static IEnumerator Extras()
    {
        yield return Radial("radial");
        yield return Dream("dream");
    }

    public static IEnumerator Batch2()
    {
        yield return Retakes();
        yield return TownLife();
        yield return LoopAndResults();
    }

    public static IEnumerator TownLife()
    {
        yield return Fiesta("fiesta_hud", "fiesta_pan");
        yield return EventScene("event_gathering", "health_day", 9f);
        yield return EventScene("event_mural", "mural_day", 8f);
        yield return Letter("letter");
        yield return Cat("cat");
        yield return Board("board");
        yield return BeachHut("beach_hut");
    }

    public static IEnumerator LoopAndResults()
    {
        yield return Remember("remember");
        yield return WakeUp("wake_loop2");
        yield return Noticed("noticed");
        yield return Results("results");
        yield return Progress("progress");
    }
}
#endif
