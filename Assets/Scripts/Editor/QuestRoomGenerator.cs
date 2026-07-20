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
    }

    struct QuestData
    {
        public string roomArea;        // Forest / House / River / School
        public string outputName;      // prefab filename without .prefab
        public string questId;
        public string questName;
        public string riasec;
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
    };

    // ── Entry point ─────────────────────────────────────────────────────────

    [MenuItem("SUNOD/Generate 3 Quest Rooms")]
    static void Generate()
    {
        int count = 0;
        foreach (var q in Quests)
            if (GenerateOne(q)) count++;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[QuestGen] Done - {count}/{Quests.Length} prefabs written to {ROOMS_PATH}");
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
