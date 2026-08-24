using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class QuestRoomGenerator
{
    const string ROOMS_PATH = "Assets/Prefabs/Rooms/";
    const string MAIN_MARKER_PATH = "Assets/Prefabs/Markers/Prefab_MainMarker.prefab";
    const string POI_MARKER_PATH = "Assets/Prefabs/Markers/Prefab_POIMarker.prefab";
    const string SKILL_ZONE_PATH = "Assets/Prefabs/Markers/Prefab_SkillZone.prefab";
    const string NPC_PATH = "Assets/Prefabs/NPC/Prefab_NPC.prefab";

    static readonly HashSet<string> NpcNames = new HashSet<string>
    {
        "Friend", "InjuredNPC", "Child",
        "Actor1", "Actor2", "Actor3",
        "Customer1", "Customer2", "Customer3",
        "Student1", "Student2", "Student3", "Student4",
    };

    struct SkillPathData
    {
        public string tag;
        public string message;
        public int interactionType; // 0=ProximityTrigger 1=Checkpoint 2=NPCFollow 3=NPCGoTo 4=ItemReveal
        public string targetName;
        public string arrowTargetName;
        public string npcFollowName;
        public string npcGoToName;
        public string destinationName;
        public string propToRevealName;
        public string[] propsToHideNames;
        public int objectiveIndex;
        public string counterGroup;    // empty = no counter
        public int counterTarget;      // presses needed before the path resolves
        public bool startsActive;      // walk-solve: armed at quest start, no skill press
        public bool isStageStep;       // non-terminal chain step
    }

    struct QuestData
    {
        public string roomArea;        // Forest / House / River / School
        public string outputName;      // prefab filename without .prefab
        public string questId;
        public string questName;
        public string riasec;
        public string questionCode;    // Option B item slot (R1..C8). Blank = scores nothing.
        public string[] hints;         // vc_QuestRoom.questHints - house style is exactly 2
        public string[] solvableWithTags;
        public int lockType;           // 0=None 1=Soft 2=Hard
        public bool isExposureEligible;
        public string objectiveText;
        public string description;
        public string hudTitle;
        public string[] hudObjectives; // null = none
        public string[] placeholders;
        public string[] npcFollowObjects;
        public string[] inactiveOnStart;
        public SkillPathData[] skillPaths;
    }

    // ── Helper factories ────────────────────────────────────────────────────

    static SkillPathData SP(string tag, string msg, int type, string target, int objIdx = -1)
        => new SkillPathData { tag = tag, message = msg, interactionType = type, targetName = target, objectiveIndex = objIdx };

    static SkillPathData SPReveal(string tag, string msg, string target, string reveal, string[] hide, int objIdx = -1)
        => new SkillPathData { tag = tag, message = msg, interactionType = 4, targetName = target, propToRevealName = reveal, propsToHideNames = hide, objectiveIndex = objIdx };

    static SkillPathData SPCheckpoint(string tag, string msg, string target, string arrow, int objIdx)
        => new SkillPathData { tag = tag, message = msg, interactionType = 1, targetName = target, arrowTargetName = arrow, objectiveIndex = objIdx };

    static SkillPathData SPNPCFollow(string tag, string msg, string npcName, string dest, int objIdx)
        => new SkillPathData { tag = tag, message = msg, interactionType = 2, targetName = npcName, npcFollowName = npcName, destinationName = dest, arrowTargetName = dest, objectiveIndex = objIdx };

    static SkillPathData SPNPCGoTo(string tag, string msg, string npcName, string dest, int objIdx = -1)
        => new SkillPathData { tag = tag, message = msg, interactionType = 3, targetName = npcName, npcGoToName = npcName, destinationName = dest, objectiveIndex = objIdx };

    // Counter reveal: the path re-fires on every qualifying press, showing "msg (n/target)".
    // The reveal/hide swap runs ONCE, when the group reaches its target - see
    // vc_GenericQuestEngine.HandleSkillPressed. So one prop pair covers the whole counter.
    static SkillPathData SPRevealCounter(string tag, string msg, string target, string reveal, string[] hide, string group, int target_, int objIdx = -1)
        => new SkillPathData { tag = tag, message = msg, interactionType = 4, targetName = target, propToRevealName = reveal, propsToHideNames = hide, counterGroup = group, counterTarget = target_, objectiveIndex = objIdx };

    // Walk-solve: ReachLocation armed at quest start, no skill press. Leave the tag empty.
    static SkillPathData SPWalkSolve(string msg, string target, int objIdx = -1)
        => new SkillPathData { tag = "", message = msg, interactionType = 6, targetName = target, startsActive = true, objectiveIndex = objIdx };

    // ── Quest definitions ───────────────────────────────────────────────────

    static readonly QuestData[] Quests = new QuestData[]
    {
        // R2
        new QuestData {
            roomArea = "House", outputName = "Room_House_BrokenPumpQuest",
            questId = "r_pump", questName = "Broken Pump", riasec = "R",
            objectiveText = "Fix the water pump.", description = "The yard pump is broken. Fix it.",
            hudTitle = "Broken Pump", hudObjectives = null,
            placeholders = new[] { "Target_Pump" },
            npcFollowObjects = new string[0], inactiveOnStart = new string[0],
            skillPaths = new[] {
                SP("repair", "You repaired the pump!", 0, "Target_Pump"),
                SP("build",  "You rebuilt the pump!",  0, "Target_Pump"),
                SP("push",   "You forced it back on!", 0, "Target_Pump"),
            }
        },
        // A2
        new QuestData {
            roomArea = "House", outputName = "Room_House_MuralQuest",
            questId = "a_mural", questName = "The Mural", riasec = "A",
            objectiveText = "Paint the wall.", description = "The wall needs a mural. Finish it.",
            hudTitle = "The Mural", hudObjectives = null,
            placeholders = new[] { "Target_Wall" },
            npcFollowObjects = new string[0], inactiveOnStart = new string[0],
            skillPaths = new[] {
                SP("paint", "You painted the mural!",  0, "Target_Wall"),
                SP("craft", "You crafted the design!", 0, "Target_Wall"),
            }
        },
        // I3
        new QuestData {
            roomArea = "River", outputName = "Room_River_ChartBankQuest",
            questId = "i_map", questName = "Chart the Bank", riasec = "I",
            objectiveText = "Survey and map the riverbank.", description = "Someone needs this area charted.",
            hudTitle = "Chart the Bank", hudObjectives = null,
            placeholders = new[] { "Target_MapPoint" },
            npcFollowObjects = new string[0], inactiveOnStart = new string[0],
            skillPaths = new[] {
                SP("map",    "You mapped the whole area!",   0, "Target_MapPoint"),
                SP("survey", "Your scan charted the bank!", 0, "Target_MapPoint"),
                SP("plan",   "You planned out the layout!", 0, "Target_MapPoint"),
            }
        },

        // S3 - "Help people who have problems with drugs or alcohol"
        new QuestData {
            roomArea = "River", outputName = "Room_River_BitterSpringQuest",
            questId = "s3_spring", questName = "The Bitter Spring", riasec = "S",
            questionCode = "S3",
            objectiveText = "Help Mang Tino away from the bitter spring.",
            description = "Mang Tino keeps going back to the bitter spring. The water has made him sick, and he can't leave it alone on his own.",
            hints = new[] {
                "His hands are shaking - steady him first.",
                "He won't leave until someone shows him why he should.",
            },
            solvableWithTags = new[] { "heal", "teach", "guide" },
            hudTitle = "The Bitter Spring", hudObjectives = null,
            placeholders = new[] {
                "Target_Tino_Sick", "Target_Spring", "Target_HealerHut",
                "Target_Tino_Steady", "Target_Tino_TurnedAway", "Target_Tino_AtHut",
            },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Tino_Steady", "Target_Tino_TurnedAway", "Target_Tino_AtHut" },
            skillPaths = new[] {
                SPReveal("heal",  "His hands are steady again.", "Target_Tino_Sick", "Target_Tino_Steady",     new[] { "Target_Tino_Sick" }),
                SPReveal("teach", "He looked at the spring a long time - then turned his back on it.", "Target_Tino_Sick", "Target_Tino_TurnedAway", new[] { "Target_Tino_Sick" }),
                SPReveal("guide", "You walked him to the healer's door.", "Target_Tino_Sick", "Target_Tino_AtHut", new[] { "Target_Tino_Sick" }),
            }
        },

        // S4 - "Teach children how to play sports"
        new QuestData {
            roomArea = "School", outputName = "Room_School_TeachTheGameQuest",
            questId = "s4_game", questName = "Teach Them the Game", riasec = "S",
            questionCode = "S4",
            objectiveText = "Get the children playing properly.",
            description = "The kids have a ball and an empty schoolyard but no idea how to play. One of them hangs back by the wall.",
            hints = new[] {
                "Show them how it's done - one child at a time.",
                "The quiet one by the wall is only waiting to be asked.",
            },
            // 'heal' deliberately excluded: ComputeItemScore ignores which skill solved the
            // quest, so a heal solve would write "interested in teaching children sports"
            // into slot S4 for a player who only gave first aid. teach/guide are on-item.
            solvableWithTags = new[] { "teach", "guide" },
            hudTitle = "Teach Them the Game", hudObjectives = null,
            placeholders = new[] {
                "Target_Kids_Idle", "Target_Court", "Target_Ball", "Target_ShyKid",
                "Target_Kids_Playing",
            },
            // The shy kid physically walks to the line, so it needs the follow/goto components.
            npcFollowObjects = new[] { "Target_ShyKid" },
            inactiveOnStart = new[] { "Target_Kids_Playing" },
            skillPaths = new[] {
                SPRevealCounter("teach", "That's it - you've got it now!", "Target_Kids_Idle", "Target_Kids_Playing", new[] { "Target_Kids_Idle" }, "kids", 3),
                // guide = the player leads her over: she follows, and the quest resolves when
                // she reaches the court. Destination is Target_Court, about 7 tiles away.
                SPNPCFollow("guide", "She joined the line. The game's on.", "Target_ShyKid", "Target_Court", -1),
            }
        },

        // C4 - "Maintain employee records"
        new QuestData {
            roomArea = "School", outputName = "Room_School_StaffLedgerQuest",
            questId = "c4_ledger", questName = "The Staff Ledger", riasec = "C",
            questionCode = "C4",
            objectiveText = "Put the staff records in order.",
            description = "The school office is behind on its staff records - forms in loose piles on the floor, names entered wrong in the ledger, nothing in the right drawer.",
            hints = new[] {
                "Three piles are still on the floor - gather them first.",
                "Or read the ledger properly; the errors are in the names.",
            },
            // All three are literally record-keeping, so none of them writes a false C4 score.
            solvableWithTags = new[] { "collect", "inspect", "direct" },
            hudTitle = "The Staff Ledger", hudObjectives = null,
            // Every path acts on Target_Records, so one skill zone covers the whole quest.
            placeholders = new[] {
                "Target_Records", "Target_Cabinet",
                "Target_Records_Filed", "Target_Records_Checked", "Target_Records_Sorted",
            },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Records_Filed", "Target_Records_Checked", "Target_Records_Sorted" },
            skillPaths = new[] {
                SPRevealCounter("collect", "Every form off the floor and stacked.", "Target_Records", "Target_Records_Filed", new[] { "Target_Records" }, "records", 3),
                SPReveal("inspect", "Three names were spelled wrong. Not any more.", "Target_Records", "Target_Records_Checked", new[] { "Target_Records" }),
                SPReveal("direct", "Filed, drawer by drawer. Anyone could find these now.", "Target_Records", "Target_Records_Sorted", new[] { "Target_Records" }),
            }
        },
    };

    // ── Entry point ─────────────────────────────────────────────────────────

    // Default run skips any prefab that already exists, so re-running to add a new quest
    // never clobbers rooms that have since been hand-tuned in the Editor.
    [MenuItem("SUNOD/Generate Quest Rooms (new only)")]
    static void Generate() => Run(force: false);

    [MenuItem("SUNOD/Generate Quest Rooms (FORCE overwrite)")]
    static void GenerateForce()
    {
        if (!EditorUtility.DisplayDialog(
                "Overwrite quest rooms?",
                $"This rebuilds all {Quests.Length} quest room prefabs in {ROOMS_PATH} from the table, " +
                "discarding any hand-tuning done in the Editor.\n\nContinue?",
                "Overwrite", "Cancel"))
            return;

        Run(force: true);
    }

    static void Run(bool force)
    {
        int written = 0, skipped = 0;
        foreach (var q in Quests)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(ROOMS_PATH + q.outputName + ".prefab") != null)
            {
                skipped++;
                continue;
            }

            if (GenerateOne(q)) written++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[QuestGen] Done - {written} written, {skipped} skipped (already exist) in {ROOMS_PATH}");
    }

    const string PLACEHOLDER_SPRITE_PATH = "Assets/Art/Generated/Quests/crate.png";

    // Gives every Target_* placeholder a visible sprite, drawn above the room's tilemaps.
    // NPC-named placeholders are skipped - they already carry the NPC prefab's own visuals.
    static void AddPlaceholderSprites(GameObject root, Dictionary<string, GameObject> pm)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PLACEHOLDER_SPRITE_PATH);
        if (sprite == null)
        {
            Debug.LogWarning($"[QuestGen] {PLACEHOLDER_SPRITE_PATH} missing - props will be invisible. " +
                             "Run SUNOD/Generate Quest Sprites first.");
            return;
        }

        // Draw above whatever the base room's tilemaps use, whichever layer that is.
        int topOrder = 0;
        string layer = "Default";
        foreach (var tr in root.GetComponentsInChildren<UnityEngine.Tilemaps.TilemapRenderer>(true))
            if (tr.sortingOrder >= topOrder) { topOrder = tr.sortingOrder; layer = tr.sortingLayerName; }

        foreach (var kvp in pm)
        {
            if (NpcNames.Contains(kvp.Key) || kvp.Value == null) continue;

            var go = kvp.Value;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            sr.sprite = sprite;
            sr.sortingLayerName = layer;
            sr.sortingOrder = topOrder + 10;

            // Yellow marks the main target (the skill zone lives there); green marks a prop
            // that starts hidden and is revealed by a skill.
            bool isMain = go.GetComponentInChildren<vc_SkillZone>(true) != null;
            sr.color = isMain ? new Color(1f, 0.85f, 0.2f) : Color.white;
        }
    }

    static bool GenerateOne(QuestData q)
    {
        string basePath = ROOMS_PATH + "Room_" + q.roomArea + ".prefab";
        GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
        if (basePrefab == null)
        {
            Debug.LogError($"[QuestGen] Base prefab not found: {basePath}");
            return false;
        }

        string outPath = ROOMS_PATH + q.outputName + ".prefab";

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        root.name = q.outputName;

        // ── Load shared prefabs ──────────────────────────────────────────
        GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NPC_PATH);
        GameObject mainMarkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MAIN_MARKER_PATH);
        GameObject poiMarkerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(POI_MARKER_PATH);
        GameObject skillZonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SKILL_ZONE_PATH);

        if (npcPrefab == null)
            Debug.LogError($"[QuestGen] {NPC_PATH} missing - NPC instantiation skipped for '{q.outputName}'. Empty GameObjects used instead.");

        // ── Resolve main target + Target_Center ──────────────────────────
        string mainTargetName = (q.skillPaths != null && q.skillPaths.Length > 0) ? q.skillPaths[0].targetName : null;
        bool useTargetCenter = string.IsNullOrEmpty(mainTargetName);
        if (useTargetCenter) mainTargetName = "Target_Center";

        var placeholderNames = new List<string>();
        if (q.placeholders != null) placeholderNames.AddRange(q.placeholders);
        if (useTargetCenter && !placeholderNames.Contains(mainTargetName))
            placeholderNames.Add(mainTargetName);

        // ── Create + position placeholders ───────────────────────────────
        var pm = new Dictionary<string, GameObject>();

        foreach (string name in placeholderNames)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);

            int idx = pm.Count;
            float angle = idx * 60f * Mathf.Deg2Rad;
            go.transform.localPosition = idx == 0
                ? new Vector3(0f, 1.5f, 0f)
                : new Vector3(Mathf.Cos(angle) * 4f, Mathf.Sin(angle) * 4f, 0f);

            pm[name] = go;
        }

        // ── Replace NPC-named placeholders with real NPC prefab ──────────
        if (npcPrefab != null)
        {
            foreach (string name in placeholderNames)
            {
                if (!NpcNames.Contains(name)) continue;

                GameObject empty = pm[name];
                Vector3 pos = empty.transform.localPosition;
                Object.DestroyImmediate(empty);

                var npc = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab, root.transform);
                npc.name = name;
                npc.transform.localPosition = pos;
                pm[name] = npc;

                // POI marker floating over the NPC
                if (poiMarkerPrefab != null)
                {
                    var poi = (GameObject)PrefabUtility.InstantiatePrefab(poiMarkerPrefab, npc.transform);
                    poi.transform.localPosition = Vector3.zero;
                }
            }
        }

        if (q.inactiveOnStart != null)
            foreach (string name in q.inactiveOnStart)
                if (pm.TryGetValue(name, out var go)) go.SetActive(false);

        if (q.npcFollowObjects != null)
            foreach (string name in q.npcFollowObjects)
                if (pm.TryGetValue(name, out var go))
                {
                    go.AddComponent<vc_NPC_Follow>();
                    go.AddComponent<vc_NPC_GoTo>();
                }

        // ── Main target: MainMarker + SkillZone, capture marker for hide ─
        vc_FloatingMarker mainFloatingMarker = null;
        if (pm.TryGetValue(mainTargetName, out var mainObj))
        {
            if (mainMarkerPrefab != null)
            {
                var marker = (GameObject)PrefabUtility.InstantiatePrefab(mainMarkerPrefab, mainObj.transform);
                marker.transform.localPosition = Vector3.zero;
                mainFloatingMarker = marker.GetComponentInChildren<vc_FloatingMarker>();
            }
            if (skillZonePrefab != null)
            {
                var zone = (GameObject)PrefabUtility.InstantiatePrefab(skillZonePrefab, mainObj.transform);
                zone.transform.localPosition = Vector3.zero;
            }
        }

        // ── Skill zones on every OTHER path target ───────────────────────
        // Skills only fire inside a vc_SkillZone. A quest whose paths point at different
        // props (e.g. "help the shy kid" vs "help the hurt kid") needs a zone at each one,
        // or those paths are unpressable: the player can never be within proximityRange of
        // the target while standing in the single main zone.
        if (skillZonePrefab != null && q.skillPaths != null)
        {
            var zoned = new HashSet<string> { mainTargetName };
            foreach (var spd in q.skillPaths)
            {
                string name = spd.targetName;
                if (string.IsNullOrEmpty(name) || zoned.Contains(name)) continue;
                if (!pm.TryGetValue(name, out var tgtObj)) continue;

                var extraZone = (GameObject)PrefabUtility.InstantiatePrefab(skillZonePrefab, tgtObj.transform);
                extraZone.transform.localPosition = Vector3.zero;
                zoned.Add(name);
            }
        }

        // ── Visible placeholder art ──────────────────────────────────────
        // Without this every Target_* is an empty GameObject and the room looks empty in
        // Play mode, which makes a quest impossible to test. Real art replaces the sprite
        // later; the point here is only that the builder can SEE where each prop sits.
        AddPlaceholderSprites(root, pm);

        // ── QuestTrigger ─────────────────────────────────────────────────
        var qtGO = new GameObject("QuestTrigger");
        qtGO.transform.SetParent(root.transform, false);
        qtGO.transform.localPosition = Vector3.zero;

        var questRoom = qtGO.AddComponent<vc_QuestRoom>(); // RequireComponent auto-adds BoxCollider2D
        var col = qtGO.GetComponent<BoxCollider2D>();
        if (col != null) col.size = new Vector2(16f, 16f);

        var engine = qtGO.AddComponent<vc_GenericQuestEngine>();

        // ── vc_QuestRoom fields ──────────────────────────────────────────
        var roomSO = new SerializedObject(questRoom);
        roomSO.FindProperty("questLogic").objectReferenceValue = engine;
        roomSO.FindProperty("questId").stringValue = q.questId;
        roomSO.FindProperty("questName").stringValue = q.questName;
        roomSO.FindProperty("primaryRiasec").stringValue = q.riasec;
        roomSO.FindProperty("objectiveText").stringValue = q.objectiveText;
        roomSO.FindProperty("questDescription").stringValue = q.description;

        // Option B: without a questionCode the quest plays fine but scores nothing -
        // vc_RiasecAdapter drops uncoded records into IgnoredRecords.
        roomSO.FindProperty("questionCode").stringValue = q.questionCode ?? string.Empty;
        roomSO.FindProperty("lockType").enumValueIndex = q.lockType;
        roomSO.FindProperty("isExposureEligible").boolValue = q.isExposureEligible;

        var hintsProp = roomSO.FindProperty("questHints");
        string[] hints = q.hints ?? new string[0];
        hintsProp.arraySize = hints.Length;
        for (int i = 0; i < hints.Length; i++)
            hintsProp.GetArrayElementAtIndex(i).stringValue = hints[i];

        var solvableProp = roomSO.FindProperty("solvableWithTags");
        string[] solvable = q.solvableWithTags ?? new string[0];
        solvableProp.arraySize = solvable.Length;
        for (int i = 0; i < solvable.Length; i++)
            solvableProp.GetArrayElementAtIndex(i).stringValue = solvable[i];

        roomSO.ApplyModifiedProperties();

        // ── vc_GenericQuestEngine fields ─────────────────────────────────
        var engSO = new SerializedObject(engine);
        engSO.FindProperty("hudCounter").stringValue = "Quest";
        engSO.FindProperty("hudTitle").stringValue = q.hudTitle;

        var hudObjProp = engSO.FindProperty("hudObjectives");
        string[] objectives = q.hudObjectives ?? new[] { q.objectiveText };
        hudObjProp.arraySize = objectives.Length;
        for (int i = 0; i < objectives.Length; i++)
            hudObjProp.GetArrayElementAtIndex(i).stringValue = objectives[i];

        var spArr = engSO.FindProperty("skillPaths");
        spArr.arraySize = q.skillPaths != null ? q.skillPaths.Length : 0;

        if (q.skillPaths != null)
        {
            for (int i = 0; i < q.skillPaths.Length; i++)
            {
                var spd = q.skillPaths[i];
                var sp = spArr.GetArrayElementAtIndex(i);

                sp.FindPropertyRelative("skillTag").stringValue = spd.tag;
                sp.FindPropertyRelative("successMessage").stringValue = spd.message;
                sp.FindPropertyRelative("interactionType").enumValueIndex = spd.interactionType;
                sp.FindPropertyRelative("objectiveIndex").intValue = spd.objectiveIndex;
                sp.FindPropertyRelative("requiresPathIndex").intValue = -1;
                sp.FindPropertyRelative("proximityRange").floatValue = 2f;
                sp.FindPropertyRelative("npcArrivalRange").floatValue = 3f;
                sp.FindPropertyRelative("completionDelay").floatValue = 0f;
                sp.FindPropertyRelative("counterGroup").stringValue = spd.counterGroup ?? string.Empty;
                sp.FindPropertyRelative("counterTarget").intValue = spd.counterTarget;
                sp.FindPropertyRelative("startsActive").boolValue = spd.startsActive;
                sp.FindPropertyRelative("isStageStep").boolValue = spd.isStageStep;

                string resolvedTarget = spd.targetName;
                if (string.IsNullOrEmpty(resolvedTarget) && useTargetCenter)
                    resolvedTarget = "Target_Center";

                if (resolvedTarget != null && pm.TryGetValue(resolvedTarget, out var tgt))
                    sp.FindPropertyRelative("targetTransform").objectReferenceValue = tgt.transform;

                if (spd.arrowTargetName != null && pm.TryGetValue(spd.arrowTargetName, out var arrow))
                    sp.FindPropertyRelative("arrowTarget").objectReferenceValue = arrow.transform;

                if (spd.npcFollowName != null && pm.TryGetValue(spd.npcFollowName, out var npcGO))
                {
                    var npcFollow = npcGO.GetComponent<vc_NPC_Follow>();
                    if (npcFollow != null)
                        sp.FindPropertyRelative("npcFollow").objectReferenceValue = npcFollow;
                }

                if (spd.npcGoToName != null && pm.TryGetValue(spd.npcGoToName, out var npcGO2))
                {
                    var npcGoTo = npcGO2.GetComponent<vc_NPC_GoTo>();
                    if (npcGoTo != null)
                        sp.FindPropertyRelative("npcGoTo").objectReferenceValue = npcGoTo;
                }

                if (spd.destinationName != null && pm.TryGetValue(spd.destinationName, out var dest))
                    sp.FindPropertyRelative("destination").objectReferenceValue = dest.transform;

                if (spd.propToRevealName != null && pm.TryGetValue(spd.propToRevealName, out var reveal))
                    sp.FindPropertyRelative("propToReveal").objectReferenceValue = reveal;

                if (spd.propsToHideNames != null)
                {
                    var hideProp = sp.FindPropertyRelative("propsToHide");
                    hideProp.arraySize = spd.propsToHideNames.Length;
                    for (int j = 0; j < spd.propsToHideNames.Length; j++)
                        if (pm.TryGetValue(spd.propsToHideNames[j], out var hideGO))
                            hideProp.GetArrayElementAtIndex(j).objectReferenceValue = hideGO;
                }
            }
        }

        var globalMarkersProp = engSO.FindProperty("globalMarkersToHide");
        if (mainFloatingMarker != null)
        {
            globalMarkersProp.arraySize = 1;
            globalMarkersProp.GetArrayElementAtIndex(0).objectReferenceValue = mainFloatingMarker;
        }
        else
        {
            globalMarkersProp.arraySize = 0;
        }

        engSO.ApplyModifiedProperties();

        // ── Fallback: wire globalMarkersToHide from first placeholder's MainMarker child ──
        if (q.placeholders != null && q.placeholders.Length > 0
            && pm.TryGetValue(q.placeholders[0], out var firstPlaceholder))
        {
            var marker = firstPlaceholder.GetComponentInChildren<vc_FloatingMarker>();
            if (marker != null)
            {
                var gmProp = engSO.FindProperty("globalMarkersToHide");
                gmProp.arraySize = 1;
                gmProp.GetArrayElementAtIndex(0).objectReferenceValue = marker;
                engSO.ApplyModifiedProperties();
            }
        }

        // ── Save ─────────────────────────────────────────────────────────
        PrefabUtility.SaveAsPrefabAsset(root, outPath);
        Object.DestroyImmediate(root);

        Debug.Log($"[QuestGen] Created {outPath}");
        return true;
    }
}
