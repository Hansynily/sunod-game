using UnityEngine;

/// <summary>
/// The three towns, laid out like a planned barangay (2026-11-07 redesign): each has a
/// centre (a round plaza), streets that curve and fork into districts, houses lined up on
/// their street with little fenced yards, a school/campus quarter, a park, and the sea with a
/// beach along the bottom. Every quest giver stands at their own place (shop door, pond, beach).
/// Coordinates: 1 unit = 1 tile, origin = the town's bottom-left; objects by bottom-centre.
/// Named spots: "play" (daily happenings), "hang" (where roamers go), "stage" (gathering
/// events), "board" (the plaza quest board).
/// </summary>
public static partial class vc_TownBuilder
{
    // ── Elementary: a small seaside barangay ─────────────────────────────────
    //  north: the school (yard, playground, canteen) · centre: the round plaza
    //  west: two residential lanes + the garden · east: fair stalls, toy store, park + pond,
    //  the meadow · south: the beach, a beach hut, the dock, a stream running into the sea
    private static Town ElementaryPlan()
    {
        var t = new Town
        {
            scene = "Town_Elementary", id = "elementary", title = "Elementary", w = 56, h = 44, seed = 11,
            spawn = new Vector2(28f, 13f),
            intro = "You're in grade school now. People around the neighborhood need a hand - look for anyone with a ! above their head.",
            complete = "You helped the whole neighborhood! The years go by, and high school is next.",
            borderTrees = new[] { "tree_green", "tree_round", "tree_pine" },
        };
        t.nightDreams = new[]
        {
            "You dream of your house again - the roof torn open, the street empty. It feels far away... for now.",
            "In the dream, the shadowy figure points at the sky. \"Learn,\" it says. \"Learn everything you can.\"",
            "Tonight the dream is quiet. Just the sound of wind, and a clock ticking somewhere.",
        };
        t.dreamAfter = new[]
        {
            "The years pass. Grade school ends, and you start to grow up.",
            "The dreams don't stop. If anything, they're clearer now: cracked walls, flooded streets, people running.",
            "Whatever is coming, you're not a little kid anymore. High school is next.",
        };

        // ground: sea + beach, plaza, schoolyard, garden soil, pond + stream
        t.Sea(4.5f, 1.1f, 4.2f);
        t.Blob("plaza", 28f, 23f, 4.8f, 4.2f, 0.04f);
        t.Blob("sand", 31f, 33.4f, 12f, 2.6f, 0.04f);
        t.Blob("dirt", 19.5f, 30.2f, 4.2f, 1.7f, 0.05f);
        t.Blob("water", 46f, 16.4f, 3.8f, 2.5f, 0.1f);
        t.Road("water", 2.2f, 48.2f, 14.2f, 49f, 11.5f, 48.6f, 8f, 48.8f, 3f);

        // streets
        t.Road("dirt", 2.6f, 28f, 9.5f, 28f, 14f, 28.6f, 17f, 28f, 20f);                         // beach -> plaza
        t.Road("dirt", 2.6f, 28f, 26f, 27.6f, 29f, 28f, 32f);                                     // plaza -> school
        t.Road("dirt", 2.2f, 23.4f, 23f, 19f, 23.9f, 14f, 23.5f, 9f, 24.4f, 3.5f, 26.4f);        // upper lane (west)
        t.Road("dirt", 2.2f, 27.4f, 15.8f, 22f, 16.2f, 16f, 15.8f, 9f, 16f, 3.5f, 16.4f);      // lower lane (west)
        t.Road("dirt", 2f, 12.2f, 16.3f, 11.6f, 20f, 12.2f, 23.6f);                               // lane to lane
        t.Road("dirt", 2.2f, 27.4f, 10.8f, 22f, 10.3f, 15f, 10.1f, 9.6f, 10.3f);                     // home lane by the beach
        t.Road("dirt", 2.4f, 32.6f, 23f, 37f, 22.4f, 42f, 23.2f, 47f, 22.4f, 53.5f, 23.4f);       // east road
        t.Road("dirt", 2f, 32.4f, 19.8f, 37.4f, 19.4f, 37.8f, 14f, 37.6f, 12.4f);                // plaza -> beach hut
        t.Road("dirt", 2f, 44.6f, 23.4f, 44.8f, 28.4f, 46.8f, 29.4f, 47f, 31f);                   // east road -> meadow
        t.Bridge("plaza", 2.2f, 47.2f, 9.8f, 50.2f, 9.8f);                                        // plank crossing on the beach

        // the school quarter (north)
        t.Obj("hall_grand", 28f, 35f);
        t.Obj("hall_small2", 21.4f, 35.2f);
        t.Obj("sign_school", 24.6f, 33.4f);
        t.Obj("flagpole", 31.8f, 34.6f);
        t.Obj("swing", 35.2f, 35.4f);
        t.Obj("slide", 37.8f, 34.4f);
        t.Obj("cabin", 42.6f, 35f);
        t.Obj("sign_canteen", 45.6f, 34.4f);
        t.Obj("bench", 39.8f, 32.2f);

        // plaza: lamps around the edge, the drum stage, benches
        t.Obj("lamp_white", 23.6f, 26.4f); t.Obj("lamp_white", 32.4f, 26.4f);
        t.Obj("lamp_white", 23.6f, 19.4f);
        t.Obj("drum_stage", 31.2f, 24.9f);
        t.Obj("bench_long", 25.2f, 20.2f); t.Obj("plant_pot", 30.6f, 20f);
        t.Spot("board", 24.8f, 25.2f);
        t.Spot("stage", 28f, 23.6f);
        t.Spot("hang", 28f, 21.2f);

        // fair + toy store (east road, north side)
        t.Obj("stall_orange", 35.2f, 25f);
        t.Obj("stall_green", 38.6f, 25f);
        t.Obj("sign_fair", 41.9f, 25.4f);
        t.Obj("shop_reddoor", 48.2f, 25.4f);
        t.Obj("sign_toys", 51.6f, 25.2f);
        t.Obj("lamp_white", 44f, 21f);

        // west: residential lanes (houses face their lane), the garden, the reading tree
        t.House("house_red", 17f, 26f);
        t.House("house_peach", 9.6f, 26.6f);
        t.House("house_slate", 8.6f, 18.4f);
        t.House("house_red_window", 16.4f, 18.2f);
        t.House("house_peach3", 23.4f, 18.6f);
        t.Obj("lamp_blue", 14.6f, 21.2f);
        t.Obj("garden_bed", 17.6f, 30f); t.Obj("garden_bed", 20f, 30f); t.Obj("garden_bed", 22.4f, 30f);
        t.Obj("sign_garden", 14.6f, 29.6f);
        t.Obj("bench", 9.6f, 31.4f);
        t.Obj("tree_round", 7f, 32.4f);
        t.home = "house_peach2"; t.homePos = new Vector2(15.6f, 12.8f);
        t.Spot("stall", 6.4f, 12.4f);
        t.requestPos = new Vector2(28f, 21f);

        // park + pond (south-east), the meadow (north-east)
        t.Obj("sign_park", 41.2f, 19.4f);
        t.Obj("bench", 44f, 19.6f); t.Obj("bench", 48.6f, 19.8f);
        t.Spot("hang", 46.2f, 20.6f);
        t.Spot("play", 47.6f, 29f);

        // the beach
        t.Obj("beach_hut", 36.4f, 8.8f);
        t.Obj("umbrella_red", 20.6f, 7.2f); t.Obj("towel_red", 22.2f, 6.6f, false);
        t.Obj("umbrella_blue", 31.4f, 7.4f); t.Obj("towel_blue", 33f, 6.8f, false);
        t.Obj("sandcastle", 25.6f, 6.6f, false);
        t.Obj("shell1", 12.6f, 6.4f, false); t.Obj("shell2", 29.2f, 6.1f, false); t.Obj("starfish", 41.2f, 6.6f, false);
        t.Obj("shell1", 53f, 7f, false); t.Obj("starfish", 7.4f, 7.2f, false);
        t.Obj("tree_palm", 4.6f, 9.2f); t.Obj("tree_palm", 11.6f, 7.6f); t.Obj("tree_palm", 41.4f, 9.4f); t.Obj("tree_palm", 53.4f, 9.6f);
        t.Obj("dock", 43.6f, 4.2f, false);
        t.Obj("boat", 47.8f, 2.4f, false);
        t.Spot("play", 26.4f, 8.2f);
        t.Spot("hang", 36.4f, 6.9f);

        // trees: corners and between districts
        t.Trees(2, 31, 6, 9, 5, "tree_green", "tree_pine");
        t.Trees(13, 37, 6, 5, 3, "tree_green", "tree_round");
        t.Trees(48, 32, 7, 9, 5, "tree_round", "tree_pink");
        t.Trees(51, 12, 4, 8, 3, "tree_round", "tree_green");
        t.Trees(2, 19, 4, 4, 2, "tree_green");
        t.Trees(34, 14, 6, 4, 2, "tree_round");

        const string R = "Room_";
        t.N("R6", "Aling Rosa", "amelia_0", 17f, 24.9f, R + "House_BrokenPumpQuestNew",
            "My yard pump's leaking again! My late husband always fixed it by hand... please don't replace anything.", "No more drip-drip-drip. Thank you!");
        t.N("R2", "Mang Ben", "bob_1", 46.2f, 11.4f, R + "River_RiverCrossing",
            "My old bridge is broken and the river's too deep. That bridge was my father's, you know...", "Now I can get to the other side again!");
        t.N("R8", "Lolo Ador", "bob_3", 8.6f, 17.4f, R + "House_CreakyFloorboards",
            "These boards creak with every step. Just... keep it quiet if you can, apo. I nap at noon.", "So quiet now. Salamat, apo!");
        t.N("S4", "Tita Lorna", "amelia_2", 18.6f, 7.6f, R + "Forest_MorningExercise",
            "Show me some morning stretches? I always forget them... and my back's been so stiff lately.", "I feel so awake now!");
        t.N("I2", "Kuya Migs", "adam_1", 46.8f, 32.2f, R + "Forest_WatchTheRabbits",
            "There are rabbits in the meadow! Want to watch them? Shh, don't scare them off...", "You noticed so much about them!");
        t.N("I3", "Teacher Joy", "amelia_1", 20f, 28.5f, R + "Forest_SeedlingStudy",
            "Help me check our seedlings? I want the kids to see how you measure them.", "Great notes, little scientist!");
        t.N("I6", "Nico", "adam_2", 43.6f, 13.2f, R + "River_FishCount",
            "How many fish live in this pond? Can we count them together, row by row?", "Wow, that's a lot of fish!");
        t.N("A6", "Paolo", "alex_1", 31.2f, 24f, R + "School_ParadeDrum",
            "The parade needs a drummer! Our drum is dented, though... I wish it looked as cool as it sounds.", "You kept the beat perfectly!");
        t.N("E2", "Jun", "alex_2", 35.2f, 24.1f, R + "School_SnackStall",
            "Help me sell snacks at our stall? I'm too shy to sell... maybe just be friendly?", "We sold out! Thanks, partner!");
        t.N("E8", "Mr. Tan", "bob_2", 48.2f, 24.5f, R + "House_ToyStore",
            "My toy store is so busy today! Help me run it? The kids always run straight to the train set.", "Best helper in town!");
        t.N("A3", "Ms. Dela Cruz", "amelia_3", 25.2f, 32.4f, R + "School_FairPoster",
            "We need a poster for the fair! The kids cut out paper shapes for it too, if you want to use them.", "That poster is beautiful!");
        t.N("C2", "Principal Reyes", "bob_0", 28f, 34f, R + "House_MissingKeyQuest",
            "The spare key is lost somewhere in the supply room. Please leave the shelves neat when you're done.", "Everything's back in order. Thank you!");
        t.N("A4", "Bea", "alex_3", 21.4f, 34.2f, R + "School_ClassSong",
            "Our class needs a song for the program! I want everyone to be able to sing along.", "Everyone's humming our song!");
        t.N("S6", "Ate Mia", "amelia_0", 36.6f, 33.2f, R + "School_LostFriendQuest",
            "A little one got lost on the way to class and won't stop crying. She's scared... please be gentle.", "They had so much fun with you!");
        t.N("S7", "Toto", "adam_3", 11f, 30.6f, R + "School_StoryTime",
            "Can you help us read this storybook? My little sister hides whenever she's shy...", "I read the whole book!");
        t.N("C6", "Manang Fe", "amelia_2", 42.6f, 34f, R + "School_CanteenChange",
            "The canteen line is so long! Help me with the change? And keep the coins tidy for me, ha?", "Not a single peso missing!");
        return t;
    }

    // ── High School: a river town on the coast ──────────────────────────────
    //  the river comes down from the hills, under two bridges, across the beach to the sea ·
    //  centre: the market plaza (tents) · east: main street (radio club, barbershop, thrift) ·
    //  north-east: the high school quad · north-west: Lola Ising's house on the hill ·
    //  south: the harbour promenade, docks and the beach · west of the river: houses, home
    private static Town HighSchoolPlan()
    {
        var t = new Town
        {
            scene = "Town_HighSchool", id = "highschool", title = "High School", w = 58, h = 46, seed = 22,
            spawn = new Vector2(30f, 13.6f),
            intro = "You're in high school now, and the town is busier than ever. Look for people with a ! - they could use your help.",
            complete = "The whole town knows your name now. The dreams are getting sharper... college is next.",
            borderTrees = new[] { "tree_green", "tree_pine", "tree_round", "tree_pink" },
        };
        t.nightDreams = new[]
        {
            "You dream of the town square, but the buildings are leaning, and the river is far too high.",
            "The shadowy figure is closer tonight. \"You're getting stronger,\" it says. \"Keep going.\"",
            "In the dream you see your friends, helping each other carry things to higher ground.",
        };
        t.dreamAfter = new[]
        {
            "High school is over. The town you grew up in feels smaller now.",
            "Every night the dream is the same, and every night it's a little closer.",
            "You leave for college. You can feel it: the day is almost here.",
        };

        t.Sea(4f, 1.2f, 4.6f);
        t.Road("water", 3.4f, 12.6f, 47f, 12.6f, 38f, 15.6f, 36.6f, 15.6f, 28f, 12.8f, 26.4f, 12.8f, 12.4f, 10.8f, 10.6f, 10.8f, 2f);
        t.Blob("plaza", 30f, 22f, 5.8f, 4.4f, 0.04f);
        t.Blob("plaza", 44f, 34.4f, 9.4f, 2.6f, 0.04f);
        t.Blob("dirt", 22.4f, 15.6f, 3.6f, 1.8f, 0.05f);

        t.Road("cobble", 2.6f, 35.6f, 22f, 41f, 21.4f, 47f, 22f, 55.5f, 21.4f);                 // main street
        t.Road("cobble", 2.6f, 24.4f, 22.2f, 19f, 22.8f, 8f, 22.8f, 3f, 23.6f);                 // west road
        t.Road("cobble", 2.6f, 30f, 26f, 30.2f, 31.6f, 35.6f, 32.2f);                               // up to school
        t.Road("cobble", 2.6f, 30f, 17.6f, 30.4f, 14f, 29.6f, 11.6f);                            // down to the harbour
        t.Road("plaza", 2.4f, 17.6f, 11.4f, 24f, 11.8f, 30f, 11.4f, 38f, 11.8f, 46f, 11.2f, 53f, 11.6f);   // promenade
        t.Road("dirt", 2f, 19f, 34.6f, 21.6f, 36.4f);                                            // Lola's path
        t.Bridge("cobble", 2.8f, 8.6f, 22.8f, 17f, 22.8f);
        t.Bridge("cobble", 2.4f, 13.6f, 34.6f, 19f, 34.6f);
        t.Bridge("plaza", 2.2f, 8.8f, 8.6f, 12.8f, 8.6f);                                        // boardwalk over the river mouth

        // school quarter
        t.Obj("hall_grand", 44f, 36.6f);
        t.Obj("hall_small", 36f, 36.4f);
        t.Obj("hall_deck", 52.6f, 36.4f);
        t.Obj("flagpole", 40.6f, 36.4f);
        t.Obj("sign_hs", 38.8f, 35.4f);
        t.Obj("sign_choir", 48.6f, 35.4f);
        t.Obj("bench_long", 47f, 31.6f);

        // market plaza
        t.Obj("tent_red", 25.8f, 24.6f);
        t.Obj("tent_green", 34.2f, 24.6f);
        t.Obj("lamp_red", 24.6f, 18.4f); t.Obj("lamp_red", 35.4f, 18.4f);
        t.Obj("bench_long", 27.6f, 18.6f); t.Obj("plant_pot", 32.6f, 18.6f);
        t.Obj("stall_blue", 19.2f, 16.4f);
        t.Obj("garden_bed", 21.8f, 15.6f); t.Obj("garden_bed", 24.2f, 15.6f);
        t.Spot("board", 25.4f, 20.4f);
        t.Spot("stage", 30f, 21.6f);
        t.Spot("hang", 28.4f, 19.8f);

        // main street
        t.House("house_red2", 38.6f, 23.8f, false);
        t.Obj("sign_radio", 42.2f, 23.4f);
        t.House("house_slate2", 46f, 24f, false);
        t.Obj("sign_barber", 49.6f, 23.4f);
        t.House("house_peach3", 53.2f, 23.8f, false);
        t.Obj("lamp_red", 44f, 18.6f); t.Obj("bench", 47.6f, 19.8f);
        t.Spot("hang", 47.6f, 18.8f);

        // west of the river: houses, home; Lola on the hill
        t.House("house_red", 5.8f, 25.8f);
        t.home = "house_slate"; t.homePos = new Vector2(5f, 30.4f);
        t.Spot("stall", 5.4f, 35.4f);
        t.House("house_peach", 22f, 38.4f);
        t.requestPos = new Vector2(30f, 20f);

        // harbour + beach
        t.Obj("dock", 33f, 4.4f, false); t.Obj("dock", 36.2f, 4.6f, false);
        t.Obj("boat", 40.6f, 2.4f, false);
        t.Obj("crates", 41.6f, 13.4f); t.Obj("crates", 43.8f, 13.6f);
        t.Obj("beach_hut", 48.4f, 8.8f);
        t.Obj("sign_river", 17.6f, 13.8f);
        t.Obj("umbrella_red", 23f, 7.6f); t.Obj("towel_red", 24.6f, 7f, false);
        t.Obj("shell2", 28f, 6.6f, false); t.Obj("starfish", 52f, 7f, false); t.Obj("shell1", 5f, 6.8f, false);
        t.Obj("tree_palm", 19.4f, 9.2f); t.Obj("tree_palm", 44.6f, 9f); t.Obj("tree_palm", 55f, 9.4f); t.Obj("tree_palm", 3.6f, 10.6f);
        t.Spot("hang", 35f, 9.2f);
        t.Spot("play", 26.6f, 8.4f);
        t.Spot("play", 45.6f, 29.8f);
        t.Spot("play", 21f, 27.6f);

        t.Trees(2, 34, 9, 9, 6, "tree_pine", "tree_green");
        t.Trees(16, 40, 12, 3, 3, "tree_green", "tree_round");
        t.Trees(24, 29, 4, 4, 2, "tree_pink", "tree_round");
        t.Trees(54, 26, 3, 6, 2, "tree_pink", "tree_round");
        t.Trees(45, 13, 8, 4, 3, "tree_round");
        t.Trees(2, 12, 6, 8, 2, "tree_green");

        const string R = "Room_";
        t.N("R4", "Kuya Dan", "adam_0", 38.6f, 22.9f, R + "School_RadioRepair",
            "The radio club's old radio stopped working. Keep it original if you can, it's vintage!", "We're back on the air!");
        t.N("A8", "Ate Carla", "amelia_1", 49.6f, 33.4f, R + "House_MuralQuestNew",
            "We need a set painted for the town play. I want people to be able to touch it, you know?", "The stage looks amazing!");
        t.N("A1", "Mr. Santos", "bob_0", 52.6f, 35.4f, R + "School_ChoirPractice",
            "The choir needs someone to lead them today. Let each section shine on its own, okay?", "What a performance!");
        t.N("S2", "Liza", "alex_2", 15.6f, 13.6f, R + "Forest_FallenSparrowQuest",
            "A sparrow's hurt here by the river. I want the kids to learn how to care for it too.", "The riverbank looks so much better!");
        t.N("I8", "Prof. Lim", "bob_1", 16.6f, 19.2f, R + "River_RiverbedMap",
            "Help me map the river bottom for our field trip? My students should see it measured by hand.", "This map is perfect for the class!");
        t.N("C8", "Mang Tonyo", "bob_3", 35.4f, 8.4f, R + "River_DocksideLedger",
            "Boats keep coming in! Keep track for me? And keep the delivery slips, I need them.", "Every box accounted for!");
        t.N("R1", "Coach Ramon", "adam_3", 36f, 35.4f, R + "School_RoboticsParts",
            "Check every part before the contest? A test rig the team could reuse would be great...", "Not a single bad part!");
        t.N("I1", "Ms. Garcia", "amelia_3", 42.2f, 35.6f, R + "School_SkeletonModel",
            "Help me put the skeleton model together? Lay them out by body region so the class can follow.", "You know your bones!");
        t.N("C4", "Mrs. Ong", "amelia_0", 45.8f, 35.6f, R + "School_StudentRecords",
            "The student records are all mixed up! Some of them even have mistakes in them...", "Everything's filed. Thank you!");
        t.N("R7", "Joy", "alex_0", 25.8f, 23.6f, R + "School_FundraiserKits",
            "Help me put together the fundraiser kits? Some pieces came broken, though...", "We'll raise so much with these!");
        t.N("E1", "Marco", "adam_2", 34.2f, 23.6f, R + "School_StallPitch",
            "I want to pitch my food stall to the fair committee. If they could just taste it...", "They said yes!");
        t.N("C5", "Aling Nena", "amelia_2", 22.4f, 13.8f, R + "River_HarvestTally",
            "Help me tally today's harvest? Bring the baskets together for me, my back hurts...", "Neat and exact. Salamat!");
        t.N("A2", "Direk Paulo", "alex_1", 30f, 25.2f, R + "School_OpeningNight",
            "Opening night is tonight! Help me direct? And the costumes still aren't done!", "Standing ovation!");
        t.N("S8", "Lola Ising", "amelia_1", 22f, 37.2f, R + "House_WalkLolaHome",
            "Walk me to my chair, hijo? My knees ache so much today...", "You're such a kind young one.");
        t.N("E4", "Mang Ramil", "bob_2", 46f, 23f, R + "House_Barbershop",
            "My barbershop has a long line! Help me run it? They get grumpy when they wait.", "Every customer left smiling!");
        t.N("E6", "Ate Joan", "alex_3", 53.2f, 22.8f, R + "House_ThriftShop",
            "Run a shift at the thrift shop for me? Someone keeps eyeing that vintage jacket...", "Best sales day this month!");
        return t;
    }

    // ── College: a city on the bay ──────────────────────────────────────────
    //  centre: the roundabout plaza · a curved boulevard west-east (hotel, bank, office, cafe) ·
    //  north: the campus quad (main hall, lab, campus paper, career booth) · north-east: the
    //  park + pond and the mushroom grove · south-west: clinic, realty, beach-front houses ·
    //  south: the port avenue down to the workshop, the film studio and the pier
    private static Town CollegePlan()
    {
        var t = new Town
        {
            scene = "Town_College", id = "college", title = "College", w = 60, h = 48, seed = 33,
            spawn = new Vector2(31.2f, 16.4f),
            intro = "College, and the city is full of people who need you. The disaster is close now. Help everyone you can.",
            complete = "You're ready. Whatever comes next, everything you've learned will decide your future.",
            borderTrees = new[] { "tree_pine", "tree_round", "tree_green", "tree_palm" },
            decor = new[] { "flower_white", "flower_blue", "tiny_flower1", "tiny_flower3", "clover", "clover", "bush", "bloom_blue", "mush_brown" },
            decorDensity = 0.06f,
        };
        t.nightDreams = new[]
        {
            "You dream of the city at night. The lights flicker, then go out, one street at a time.",
            "\"It's almost time,\" the shadowy figure whispers. \"Everything you've learned will matter.\"",
            "You dream of the port. The water is still. Too still.",
        };
        t.dreamAfter = new[]
        {
            "And then, just before you become an adult... it happens. The disaster strikes.",
            "But this time, you're ready. Every person you helped, every skill you learned, is part of who you are now.",
            "Let's see what path all of that has opened for you.",
        };

        t.Sea(4f, 0.8f, 3.8f);
        t.Blob("plaza", 31f, 22.6f, 5f, 4.2f, 0.03f);
        t.Blob("cobble", 30f, 36.4f, 11.4f, 2.8f, 0.03f);
        t.Blob("water", 52.4f, 33f, 3.4f, 2.4f, 0.1f);

        t.Road("plaza", 2.8f, 2.5f, 24.6f, 12f, 24f, 21f, 24.8f, 26.4f, 23f);                    // boulevard (west)
        t.Road("plaza", 2.8f, 35.6f, 23f, 42f, 24.4f, 50f, 23.6f, 58f, 24.4f);                   // boulevard (east)
        t.Road("plaza", 2.8f, 31f, 26.6f, 30.6f, 30f, 31f, 33.8f);                               // campus avenue
        t.Road("plaza", 2.8f, 31f, 18.4f, 31.2f, 12.6f, 44.6f, 11.8f, 44.8f, 9.4f);                // port avenue
        t.Road("cobble", 2.2f, 27.2f, 19.8f, 24.6f, 19.6f, 24.2f, 15.4f, 6f, 15f);                 // south-west lane
        t.Road("cobble", 2.2f, 45.6f, 23f, 46f, 13.2f, 52.4f, 12.6f);                              // studio lane
        t.Bridge("cobble", 3f, 44.6f, 9.4f, 45f, 1.6f);                                          // the pier

        // campus
        t.Obj("hall_grand", 30f, 38.2f);
        t.Obj("hall_small", 21f, 38f);
        t.Obj("hall_deck", 39.6f, 38f);
        t.Obj("sign_campus", 26.2f, 36.8f);
        t.Obj("sign_lab", 43.4f, 37.6f);
        t.Obj("sign_paper", 17.6f, 37.6f);
        t.Obj("stall_orange", 25f, 34.4f);
        t.Obj("bench_long", 35.4f, 34.2f);
        t.Spot("play", 34.6f, 31.4f);

        // the roundabout + boulevard
        t.Obj("lamp_white", 13f, 21f); t.Obj("lamp_white", 22f, 21f); t.Obj("lamp_white", 41f, 21f); t.Obj("lamp_white", 53f, 21f);
        t.Obj("plant_pot", 28.6f, 19.4f); t.Obj("plant_pot2", 33.4f, 19.4f);
        t.Spot("board", 27.6f, 25.4f);
        t.Spot("stage", 31f, 22.4f);
        t.Spot("hang", 31f, 19.8f);
        t.House("house_slate_tall", 8f, 26.8f, false); t.Obj("sign_hotel", 4.4f, 26.6f);
        t.House("house_red_tall", 16.6f, 26.8f, false); t.Obj("sign_bank", 20.2f, 26.6f);
        t.House("house_slate3", 40.4f, 26.8f, false); t.Obj("sign_office", 36.4f, 26.6f);
        t.House("house_peach", 49.6f, 26.8f, false); t.Obj("sign_cafe", 53.4f, 26.6f);
        t.Spot("hang", 49.6f, 22.4f);
        // street trees + hedges along the boulevard
        foreach (float x in new[] { 5f, 14f, 50.4f, 56f }) t.Obj("tree_round", x, 19.8f);
        foreach (float x in new[] { 3.6f, 12.2f, 24.4f, 35.6f, 45.2f, 55.6f }) t.Obj("tree_green", x, 28.6f);
        for (float x = 4f; x < 26f; x += 1.6f) if (Mathf.Abs(x - 13f) > 1f && Mathf.Abs(x - 22f) > 1f) t.Obj("bush", x, 20.9f, false);
        for (float x = 37f; x < 58f; x += 1.6f) if (Mathf.Abs(x - 41f) > 1f && Mathf.Abs(x - 53f) > 1f && Mathf.Abs(x - 45.8f) > 1.6f) t.Obj("bush", x, 21f, false);

        // south-west: clinic, realty, beach-front houses + home
        t.Obj("hall_small2", 9.6f, 17.6f); t.Obj("sign_clinic", 6f, 17.6f);
        t.House("house_peach_tall", 20.6f, 18.6f, false); t.Obj("sign_realty", 17f, 18.4f);
        t.House("house_slate2", 8.6f, 10.6f);
        t.home = "house_red"; t.homePos = new Vector2(17f, 10.8f);
        t.Spot("stall", 23.6f, 10.4f);
        t.requestPos = new Vector2(31f, 20f);

        // waterfront: workshop, studio, port, beach
        t.Obj("cabin", 38.6f, 14.6f); t.Obj("sign_workshop", 42.4f, 14.4f);
        t.Obj("tent_blue", 52.4f, 14.6f); t.Obj("sign_studio", 56f, 14.4f);
        t.Obj("crates", 48f, 7.4f); t.Obj("sign_port", 41f, 8.4f);
        t.Obj("boat", 52f, 2.2f, false);
        t.Obj("bench", 27f, 8.2f); t.Obj("umbrella_blue", 31f, 7.4f); t.Obj("towel_blue", 32.6f, 6.8f, false);
        t.Obj("shell1", 36f, 6.2f, false); t.Obj("starfish", 13f, 6.6f, false);
        t.Obj("tree_palm", 4f, 8.6f); t.Obj("tree_palm", 13.6f, 8.4f); t.Obj("tree_palm", 34.4f, 8.2f); t.Obj("tree_palm", 57f, 8.6f);
        t.Spot("hang", 45f, 5.6f);
        t.Spot("play", 22f, 6.8f);

        // park (north-east)
        t.Obj("bench", 48.4f, 36.4f); t.Obj("bench", 55.4f, 30f);
        t.Spot("play", 49f, 30f);

        t.Trees(2, 30, 7, 10, 6, "tree_pine", "tree_round");
        t.Trees(45, 39, 12, 5, 5, "tree_round", "tree_pine");
        t.Trees(56, 28, 2, 8, 2, "tree_round");
        t.Trees(2, 18, 2, 5, 2, "tree_green");
        t.Trees(56, 16, 2, 6, 2, "tree_round");
        t.Trees(11, 30, 8, 5, 3, "tree_green", "tree_round");

        const string R = "Room_";
        t.N("I5", "Dr. Cruz", "bob_1", 54.8f, 37.6f, R + "Forest_GlowingMushrooms",
            "We found glowing mushrooms! Help us study them? Let's run a proper experiment.", "This could be a big discovery!");
        t.N("S1", "Ms. Rivera", "amelia_3", 25f, 33.6f, R + "School_CareerFair",
            "Students keep asking about careers. Help at the booth? Explain things to them, don't just walk them over.", "You really helped them find a direction.");
        t.N("I7", "Ana", "alex_2", 39.6f, 37f, R + "School_BioLab",
            "Help me with the lab samples? Write down a test plan too, I'll need it later.", "Perfect results. Great lab work!");
        t.N("A5", "Kevin", "adam_0", 21f, 37f, R + "School_CampusPaper",
            "The campus paper needs a story by tonight! It needs pictures though, people skip plain text.", "Front page material!");
        t.N("E3", "Mr. Lee", "bob_0", 8f, 25.9f, R + "House_HotelLobby",
            "Guests are arriving all at once! That one's very cranky... maybe a smile would help.", "Every guest checked in happy.");
        t.N("C7", "Ms. Uy", "amelia_0", 16.6f, 25.9f, R + "House_BankTeller",
            "The bank line is long! Handle a window for me? Send people to the right windows, please.", "Every peso balanced. Thank you!");
        t.N("E5", "Ms. Ria", "amelia_1", 39.2f, 25.9f, R + "School_InternTeam",
            "My intern team needs a leader today. Go easy on them, they're tired...", "The team did great under you.");
        t.N("C1", "Mr. Go", "bob_3", 41.8f, 25.9f, R + "School_PaydayOffice",
            "It's payday! Help me prepare everyone's envelopes? Sort them by name, please.", "Everyone got paid on time!");
        t.N("C3", "Jay", "adam_1", 49.6f, 25.9f, R + "School_NetCafe",
            "Help me make the customer bills? Hand each one to the right customer, ha.", "No billing mistakes today!");
        t.N("I4", "Dr. Santos", "bob_2", 9.6f, 16.6f, R + "House_CatMedicineQuest",
            "A sick cat was brought in. Help me find the right treatment? Test carefully, one by one.", "She's going to be okay. Thank you.");
        t.N("E7", "Agent Mel", "alex_0", 20.6f, 17.6f, R + "House_OpenHouse",
            "I'm holding an open house! The buyer keeps staring out the window... they love that view.", "Sold! You're a natural.");
        t.N("S3", "Kuya Joel", "adam_3", 25.4f, 7.8f, R + "School_SupportCorner",
            "A friend's going through a hard time. Maybe just walk with them?", "Thank you for being there for them.");
        t.N("S5", "Aling Cora", "amelia_2", 8.6f, 9.6f, R + "House_FamilyTalk",
            "My family is having a hard day. They need to learn to take turns, I think...", "We feel so much better. Salamat.");
        t.N("R5", "Mang Erning", "bob_1", 38.6f, 13.6f, R + "House_WorkshopGrinder",
            "The pump's motor bracket is too rough to fit. Grind it down? My grinding wheel's gone dull, though...", "A perfect fit!");
        t.N("A7", "Direk Bong", "alex_1", 52.4f, 13.6f, R + "Forest_StuntSet",
            "Our student film needs a stunt double. Up for it? Safety first, always!", "That stunt was incredible!");
        t.N("R3", "Engr. Paz", "adam_2", 45f, 2.8f, R + "River_PortGenerator",
            "The port generator broke down. Get it running? A backup line so it never dies again would be ideal.", "Power's back on. Great work!");
        return t;
    }
}
