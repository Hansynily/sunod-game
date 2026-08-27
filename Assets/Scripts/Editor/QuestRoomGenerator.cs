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

    // An explicit local position for one placeholder, overriding the default ring layout.
    struct PlaceholderPos
    {
        public string name;
        public float x, y;
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
        // Optional. Any placeholder named here is dropped at that exact local position;
        // everything else falls back to the default ring layout. Needed because the default
        // spots are pure geometry and know nothing about the room's collision - the index-0
        // default (0, 1.5) sits inside Room_House's vegetation, which buries the skill zone
        // and makes every path in the quest unpressable.
        public PlaceholderPos[] placeholderPositions;
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

    // Pin one placeholder to an exact local position instead of the default ring spot.
    static PlaceholderPos At(string name, float x, float y)
        => new PlaceholderPos { name = name, x = x, y = y };

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

        // S3 
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

        // S4 
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

        // C1 - "Generate the monthly payroll checks for an office"
        new QuestData {
            roomArea = "School", outputName = "Room_School_PayrollRunQuest",
            questId = "c1_payroll", questName = "The Payroll Run", riasec = "C",
            questionCode = "C1",
            objectiveText = "Get the month's payroll out.",
            description = "End of the month and nobody's been paid. The timesheets are still in loose piles on the office desk, and not one of them has been checked against the hours.",
            hints = new[] {
                "The timesheets are scattered across the desk - gather them first.",
                "Or go straight to the hours; someone logged them wrong.",
            },
            // Gathering, checking and issuing payroll are all the same clerical job, so no
            // path here writes a false C1 score.
            solvableWithTags = new[] { "collect", "inspect", "direct" },
            hudTitle = "The Payroll Run", hudObjectives = null,
            // Every path acts on Target_Timesheets, so one skill zone covers the whole quest.
            placeholders = new[] {
                "Target_Timesheets", "Target_Desk",
                "Target_Timesheets_Stacked", "Target_Hours_Checked", "Target_Checks_Issued",
            },
            // The idx4 ring spot (-2, -3.46) is inside Room_School's south wall Blocker.
            // Only cosmetic here - this prop is revealed, never stood on - but the checks
            // would otherwise pop into the wall on success.
            placeholderPositions = new[] { At("Target_Checks_Issued", -2f, -2f) },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Timesheets_Stacked", "Target_Hours_Checked", "Target_Checks_Issued" },
            skillPaths = new[] {
                SPRevealCounter("collect", "Every timesheet off the desk and stacked.", "Target_Timesheets", "Target_Timesheets_Stacked", new[] { "Target_Timesheets" }, "timesheets", 3),
                SPReveal("inspect", "Two people were short-paid. Not this month.", "Target_Timesheets", "Target_Hours_Checked", new[] { "Target_Timesheets" }),
                SPReveal("direct", "Signed, sorted, and out the door on time.", "Target_Timesheets", "Target_Checks_Issued", new[] { "Target_Timesheets" }),
            }
        },

        // C2 - "Inventory supplies using a hand-held computer"
        new QuestData {
            roomArea = "House", outputName = "Room_House_StockroomCountQuest",
            questId = "c2_inventory", questName = "The Stockroom Count", riasec = "C",
            questionCode = "C2",
            objectiveText = "Count what's actually on the shelves.",
            description = "The stockroom hasn't been counted in months. The tally sheet says one thing and the shelves say another, and nobody knows which to believe.",
            hints = new[] {
                "Count it shelf by shelf - the back row counts too.",
                "Or check the old tally against what's really there.",
            },
            // Counting, verifying and re-shelving are all stock-record work - all honest C2.
            solvableWithTags = new[] { "collect", "inspect", "direct" },
            hudTitle = "The Stockroom Count", hudObjectives = null,
            // Every path acts on Target_Supplies, so one skill zone covers the whole quest.
            placeholders = new[] {
                "Target_Supplies", "Target_Shelves",
                "Target_Supplies_Counted", "Target_Tally_Corrected", "Target_Supplies_Sorted",
            },
            // The index-0 default (0, 1.5) is buried in Room_House's vegetation, and this is
            // the prop carrying the skill zone - left there, no path in the quest is pressable.
            placeholderPositions = new[] { At("Target_Supplies", 0f, 3.5f) },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Supplies_Counted", "Target_Tally_Corrected", "Target_Supplies_Sorted" },
            skillPaths = new[] {
                SPRevealCounter("collect", "Shelf counted. The tally's catching up.", "Target_Supplies", "Target_Supplies_Counted", new[] { "Target_Supplies" }, "shelves", 3),
                SPReveal("inspect", "Six short of what the sheet claimed. Now it's right.", "Target_Supplies", "Target_Tally_Corrected", new[] { "Target_Supplies" }),
                SPReveal("direct", "Every item back where its label says it goes.", "Target_Supplies", "Target_Supplies_Sorted", new[] { "Target_Supplies" }),
            }
        },

        // C3 - "Use a computer program to generate customer bills"
        new QuestData {
            roomArea = "House", outputName = "Room_House_CustomerBillsQuest",
            questId = "c3_billing", questName = "The Month's Bills", riasec = "C",
            questionCode = "C3",
            objectiveText = "Get every customer's bill right before it goes out.",
            description = "The store's accounts are due. The billing list has printed a stack of statements, but some of the amounts don't match what the ledger says.",
            hints = new[] {
                "Check the amounts against the ledger before anything goes out.",
                "Or sort them by customer - half of these are in the wrong pile.",
            },
            // Only two paths. Gathering would be a stretch here: the item is about producing
            // correct bills, not collecting them, and an off-item path still writes a full C3.
            solvableWithTags = new[] { "inspect", "direct" },
            hudTitle = "The Month's Bills", hudObjectives = null,
            placeholders = new[] {
                "Target_Bills", "Target_Counter",
                "Target_Bills_Checked", "Target_Bills_Sorted",
            },
            // Same trap as C2 - the index-0 default (0, 1.5) is inside Room_House's vegetation,
            // and this is the prop carrying the skill zone.
            placeholderPositions = new[] { At("Target_Bills", 0f, 3.5f) },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Bills_Checked", "Target_Bills_Sorted" },
            skillPaths = new[] {
                SPReveal("inspect", "Three bills had the wrong total. Caught before they went out.", "Target_Bills", "Target_Bills_Checked", new[] { "Target_Bills" }),
                SPReveal("direct", "One pile per customer, every name in order.", "Target_Bills", "Target_Bills_Sorted", new[] { "Target_Bills" }),
            }
        },

        // C5 - "Compute and record statistical and other numerical data"
        new QuestData {
            roomArea = "School", outputName = "Room_School_TallySheetQuest",
            questId = "c5_tally", questName = "The Tally Sheet", riasec = "C",
            questionCode = "C5",
            objectiveText = "Total the figures and get them into the record.",
            description = "The term's attendance figures are due at the district office. Right now they are loose slips in three piles, with nothing added up.",
            hints = new[] {
                "The slips are in three piles - gather them before you total anything.",
                "Or check the running totals; one column was added wrong.",
            },
            // Computing and recording are the item itself; both paths are the same clerical job.
            solvableWithTags = new[] { "collect", "inspect" },
            hudTitle = "The Tally Sheet", hudObjectives = null,
            placeholders = new[] {
                "Target_Figures", "Target_Logbook",
                "Target_Figures_Gathered", "Target_Totals_Recorded",
            },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Figures_Gathered", "Target_Totals_Recorded" },
            skillPaths = new[] {
                SPRevealCounter("collect", "Another pile totalled onto the sheet.", "Target_Figures", "Target_Figures_Gathered", new[] { "Target_Figures" }, "figures", 3),
                SPReveal("inspect", "One column was off by twelve. The totals balance now.", "Target_Figures", "Target_Totals_Recorded", new[] { "Target_Figures" }),
            }
        },

        // C6 - "Operate a calculator"
        new QuestData {
            roomArea = "School", outputName = "Room_School_CanteenTillQuest",
            questId = "c6_till", questName = "Closing the Till", riasec = "C",
            questionCode = "C6",
            objectiveText = "Balance the till before closing.",
            description = "The canteen is shut and the till still has to be counted. The receipts are spiked on the counter, and the total written in the book does not match the cash in the drawer.",
            hints = new[] {
                "Run the receipts through again - the total in the book is wrong.",
                "Or square the cash away once the number is right.",
            },
            // The thinnest item of the eight; two paths is all it honestly supports.
            solvableWithTags = new[] { "inspect", "direct" },
            hudTitle = "Closing the Till", hudObjectives = null,
            placeholders = new[] {
                "Target_Till", "Target_Receipts",
                "Target_Till_Counted", "Target_Till_Closed",
            },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Till_Counted", "Target_Till_Closed" },
            skillPaths = new[] {
                SPReveal("inspect", "Forty pesos out. Counted again - it balances.", "Target_Till", "Target_Till_Counted", new[] { "Target_Till" }),
                SPReveal("direct", "Cash bagged, book signed, drawer shut.", "Target_Till", "Target_Till_Closed", new[] { "Target_Till" }),
            }
        },

        // C7 - "Handle customers' bank transactions"
        new QuestData {
            roomArea = "House", outputName = "Room_House_PadalaCounterQuest",
            questId = "c7_padala", questName = "The Padala Counter", riasec = "C",
            questionCode = "C7",
            objectiveText = "Put the customer's money through properly.",
            description = "Someone is at the counter to send money home. The form is half filled in, the amount on it does not match what they handed over, and none of it has been logged yet.",
            hints = new[] {
                "Read the form back before any money moves.",
                "Or get it logged and filed under the right name.",
            },
            solvableWithTags = new[] { "inspect", "direct" },
            hudTitle = "The Padala Counter", hudObjectives = null,
            placeholders = new[] {
                "Target_Form", "Target_Counter",
                "Target_Form_Checked", "Target_Form_Logged",
            },
            // Room_House again - the index-0 default is inside the vegetation.
            placeholderPositions = new[] { At("Target_Form", 0f, 3.5f) },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Form_Checked", "Target_Form_Logged" },
            skillPaths = new[] {
                SPReveal("inspect", "The amount was two hundred short. Caught before it went through.", "Target_Form", "Target_Form_Checked", new[] { "Target_Form" }),
                SPReveal("direct", "Logged, receipted, and filed under the right name.", "Target_Form", "Target_Form_Logged", new[] { "Target_Form" }),
            }
        },

        // C8 - "Keep shipping and receiving records"
        new QuestData {
            roomArea = "River", outputName = "Room_River_LandingRecordsQuest",
            questId = "c8_landing", questName = "The Landing Book", riasec = "C",
            questionCode = "C8",
            objectiveText = "Get everything on the landing into the book.",
            description = "Cargo has been coming off the boats all morning and going straight back out again, and not one sack of it has been written down.",
            hints = new[] {
                "Walk the whole landing - nothing gets recorded from one spot.",
                "Or go through what is already stacked and check it against the book.",
            },
            solvableWithTags = new[] { "collect", "inspect" },
            hudTitle = "The Landing Book", hudObjectives = null,
            placeholders = new[] {
                "Target_Cargo", "Target_Book",
                "Target_Cargo_Tallied", "Target_Cargo_Checked", "Target_FarBay",
            },
            // Room_River is the most cluttered of the four - both defaults land in vegetation,
            // and both are load-bearing: Target_Cargo carries the skill zone, Target_FarBay is
            // the walk destination. The open band is y=0/-1, so both sit on it, 5.1 units apart
            // (arrival range is 3, so the walk cannot resolve the moment the quest starts).
            placeholderPositions = new[] {
                At("Target_Cargo",  0f,  0f),
                At("Target_FarBay", 5f, -1f),
            },
            npcFollowObjects = new string[0],
            inactiveOnStart = new[] { "Target_Cargo_Tallied", "Target_Cargo_Checked" },
            skillPaths = new[] {
                SPRevealCounter("collect", "Another load counted onto the tally.", "Target_Cargo", "Target_Cargo_Tallied", new[] { "Target_Cargo" }, "cargo", 3),
                SPReveal("inspect", "Two sacks short of the manifest. Now the book says so.", "Target_Cargo", "Target_Cargo_Checked", new[] { "Target_Cargo" }),
                // The one C item where covering the ground IS the job - walking bay to bay is
                // how a landing gets tallied. Also the only C quest a player with no C skill
                // can finish, which keeps the letter drawable in the solvable tier.
                SPWalkSolve("You walked the length of the landing, checking off every bay.", "Target_FarBay"),
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

        var explicitPos = new Dictionary<string, Vector3>();
        if (q.placeholderPositions != null)
            foreach (var pp in q.placeholderPositions)
            {
                if (string.IsNullOrEmpty(pp.name)) continue;
                // A name that matches no placeholder would otherwise be dropped in silence,
                // leaving the prop at a ring spot the author thought they had overridden.
                if (!placeholderNames.Contains(pp.name))
                    Debug.LogWarning($"[QuestGen] '{q.outputName}': placeholderPositions names '{pp.name}', which is not in placeholders. Ignored - check the spelling.");
                explicitPos[pp.name] = new Vector3(pp.x, pp.y, 0f);
            }

        foreach (string name in placeholderNames)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);

            if (explicitPos.TryGetValue(name, out var authored))
            {
                go.transform.localPosition = authored;
            }
            else
            {
                // Default ring. Pure geometry - it does not know where the room's walls are,
                // so any spot that lands in collision needs a placeholderPositions override.
                int idx = pm.Count;
                float angle = idx * 60f * Mathf.Deg2Rad;
                go.transform.localPosition = idx == 0
                    ? new Vector3(0f, 1.5f, 0f)
                    : new Vector3(Mathf.Cos(angle) * 4f, Mathf.Sin(angle) * 4f, 0f);
            }

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
