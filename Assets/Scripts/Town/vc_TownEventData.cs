using System.Collections.Generic;

/// <summary>
/// What happens in town besides the requests (vc_TownEvents): the rare town events, the
/// morning letters and the plaza bulletin board. Text only; the schedule and the scenes live
/// in vc_TownEvents.
///
/// Town events are rare on purpose: about one stage in two has one, on day 2-4, and a whole
/// loop has at most one fiesta (about one loop in three). Each event teaches the player
/// something from one RIASEC kind, so it trains that kind's skills (+2 XP to every owned skill
/// of the kind) and pays a few bonus stars. Nothing here touches quests, the timer or scoring.
/// </summary>
public static class vc_TownEventData
{
    public enum Kind
    {
        /// <summary>Someone in front of a house has the sparkle; talk to them and help out.</summary>
        Visit,
        /// <summary>A small stage at the plaza: a talk the town gathers for (a short cutscene).</summary>
        Gathering,
    }

    public class Event
    {
        public string id, letter, title;
        public Kind kind;
        public string[] hosts;          // host name per stage (Elementary, High School, College)
        public string[] looks;          // host look per stage (vc_NpcSprites)
        public string invite;           // what the host says when you walk up
        public string[] lines;          // the scene, in order. "* ..." = narration
        public string lesson;           // shown with the reward
        public string rumor;            // the bulletin board, without naming anyone. {side} = part of town
    }

    public const int EventXp = vc_SkillLevels.XpTownEvent;   // +2 to every owned skill of the kind
    public const int EventStars = 3;
    public const int FiestaXp = 1;                            // +1 to EVERY owned skill
    public const int FiestaStars = 5;
    public const int LetterGiftStars = 2;

    public static readonly Event[] Events =
    {
        new Event
        {
            id = "bayanihan", letter = "R", title = "Bayanihan", kind = Kind.Visit,
            hosts = new[] { "Mang Celso", "Mang Celso", "Kapitan Lito" },
            looks = new[] { "bob_2", "bob_2", "adam_3" },
            invite = "The Dela Paz family is moving their whole bahay kubo across the street today. The old way: everybody lifts it together. Will you help carry?",
            lines = new[]
            {
                "Everyone grab a pole! On three. Isa... dalawa... TATLO!",
                "* Twenty neighbors lift the little house onto their shoulders. You squeeze in under a pole.",
                "Feel that? Alone, nobody here could lift it. Together, it's as light as a basket.",
                "When the big storms come, this is how a town stands back up. Remember that.",
            },
            lesson = "Many hands make heavy work light.",
            rumor = "Heard shouting and laughing on the {side} side of town. Sounds like a whole street needs strong arms today.",
        },
        new Event
        {
            id = "sky_talk", letter = "I", title = "Reading the Sky", kind = Kind.Gathering,
            hosts = new[] { "Teacher Lina", "Ms. Ocampo", "Dr. Villareal" },
            looks = new[] { "amelia_2", "amelia_3", "alex_3" },
            invite = "A talk about the weather is starting at the plaza stage! Want to listen?",
            lines = new[]
            {
                "Good morning, everyone! Who here looked at the sky today? I mean really looked?",
                "Clouds tell stories. Tall dark clouds like towers? Rain is coming. A ring around the moon? Wet days ahead.",
                "* The speaker holds up an old notebook full of numbers. Years of weather, written down every single day.",
                "We measure, we write it down, we compare. That's how we warn people early. Every answer starts with a question!",
            },
            lesson = "Watch closely, write it down, and you'll see what's coming.",
            rumor = "A visitor who studies the weather is giving a talk at the plaza stage today. Bring your questions!",
        },
        new Event
        {
            id = "mural_day", letter = "A", title = "Mural Day", kind = Kind.Visit,
            hosts = new[] { "Ate Pia", "Ate Pia", "Kuya Jomar" },
            looks = new[] { "amelia_1", "amelia_1", "alex_2" },
            invite = "We're painting the whole wall today: the river, the hills, all of us. Want to grab a brush?",
            lines = new[]
            {
                "Don't be scared of mistakes. A drip can turn into a bird if you let it.",
                "* You paint a small yellow sun in one corner. A little kid adds rays to it, then a smiling face.",
                "See? Now it's both of yours. That's the best kind of art.",
                "A town that makes beautiful things together never forgets who it is.",
            },
            lesson = "Make something, share it, and let others add to it.",
            rumor = "Smells like fresh paint on the {side} side of town. Someone over there could use more brushes.",
        },
        new Event
        {
            id = "health_day", letter = "S", title = "Barangay Health Day", kind = Kind.Gathering,
            hosts = new[] { "Nurse Ellen", "Nurse Ellen", "Dr. Ramos" },
            looks = new[] { "amelia_0", "amelia_0", "adam_2" },
            invite = "Health Day at the plaza! Free check-ups and a first-aid class. Join in?",
            lines = new[]
            {
                "If someone gets hurt: first, stay calm. Second, make sure it's safe. Third, call for help.",
                "A clean cloth and steady pressure can stop bleeding. Let's all practice. Pair up!",
                "* Your partner wraps your arm a little too tight. You both laugh and try again.",
                "Caring for people starts with asking, \"Are you okay? What do you need?\" Then really listening.",
            },
            lesson = "Stay calm, ask, listen, then help.",
            rumor = "Free check-ups and a first-aid class at the plaza stage today. Everyone is welcome.",
        },
        new Event
        {
            id = "assembly", letter = "E", title = "Town Assembly", kind = Kind.Gathering,
            hosts = new[] { "Kapitan Lito", "Kapitan Lito", "Kapitan Lito" },
            looks = new[] { "adam_3", "adam_3", "adam_3" },
            invite = "Kapitan is calling the whole town to the plaza stage. Want to see what it's about?",
            lines = new[]
            {
                "Thank you all for coming! This month we clean every canal before the rainy season.",
                "Each street needs one leader. Who will volunteer?",
                "* Your hand goes up before you even think about it. A few others follow.",
                "Ha! That's the spirit. A leader isn't the loudest one. It's the first one to say, \"I'll go.\"",
            },
            lesson = "Step up first, and others will follow.",
            rumor = "Kapitan is calling everyone to the plaza stage today. Something about needing volunteers.",
        },
        new Event
        {
            id = "census", letter = "C", title = "Census Day", kind = Kind.Visit,
            hosts = new[] { "Ms. Ilagan", "Ms. Ilagan", "Mr. Bautista" },
            looks = new[] { "amelia_3", "amelia_3", "bob_3" },
            invite = "I'm counting every household today, and my helper got sick. Could you hold the list and check names for me?",
            lines = new[]
            {
                "Name, age, how many live in the house. Write it neatly. A messy list helps no one.",
                "* You check off forty-two names, house by house. You count everything twice to be sure.",
                "Not one mistake! You have a sharp eye.",
                "When help comes after a disaster, this list tells them who to look for. Every name matters.",
            },
            lesson = "Neat records make sure nobody gets left out.",
            rumor = "Someone with a clipboard is going door to door on the {side} side of town. They look a little overwhelmed.",
        },
    };

    public static Event ById(string id) => System.Array.Find(Events, e => e.id == id);

    // ── Fiesta: the town decorates itself, an instant boost for everyone ──

    public static readonly string[] FiestaLines =
    {
        "Banderitas over every street, music from the plaza, and the smell of lechon everywhere. It's the barangay fiesta!",
        "The whole town is out today. Banderitas wave over the streets and there's a band warming up at the plaza. It's fiesta!",
        "Fiesta day! Your neighbors have been cooking since dawn, and the streets are full of color.",
    };

    // ── Morning letters ───────────────────────────────────────────────────

    public enum LetterType { Gift, Tip, Message }

    public class Letter
    {
        public LetterType type;
        public string from, body;
        public int minStage, maxStage = 2;
        public bool repeatLoopsOnly;    // only from loop 2 on
    }

    public static readonly Letter[] Letters =
    {
        // gifts (+2 stars)
        new Letter { type = LetterType.Gift, from = "Tita Baby (in Dubai)", maxStage = 1,
            body = "Anak! Here's a little something for your baon. Study hard, okay? And don't spend it all on ice candy!" },
        new Letter { type = LetterType.Gift, from = "Lolo Pedring",
            body = "I found these coins in my old jar. A man my age doesn't need them. You do. Buy something that makes you smile." },
        new Letter { type = LetterType.Gift, from = "Mama and Papa", minStage = 2,
            body = "We sold the extra mangoes this week, so here's a little for your allowance. We're proud of you. Eat properly, ha?" },
        new Letter { type = LetterType.Gift, from = "Kuya (working in Manila)", minStage = 1,
            body = "First paycheck! Sharing a bit with my favorite sibling. Don't tell the others. Work hard, but rest too." },

        // tips (in someone's voice)
        new Letter { type = LetterType.Tip, from = "Teacher Joy", maxStage = 0,
            body = "Some skills help other skills! If you look closely first, the next job can become easy. Try it!" },
        new Letter { type = LetterType.Tip, from = "your seatmate", maxStage = 1,
            body = "psst. the dots under the timer are your tries. if they run out, the quest is over. don't just press everything!!" },
        new Letter { type = LetterType.Tip, from = "Coach Ramon", minStage = 1,
            body = "The more you use a skill, the stronger it gets. Practice the ones you enjoy and they'll level up." },
        new Letter { type = LetterType.Tip, from = "Prof. Lim", minStage = 2,
            body = "A recommended level is advice, not a rule. Try anyway. A lower level just means you'll be a little slower." },
        new Letter { type = LetterType.Tip, from = "the Barangay Hall",
            body = "Reminder: check the bulletin board at the plaza. Sometimes something special is happening in town." },

        // just a message
        new Letter { type = LetterType.Message, from = "Lola",
            body = "Apo, I dreamt about you last night. You were all grown up and very brave. Come visit, and eat, ha?" },
        new Letter { type = LetterType.Message, from = "your best friend", maxStage = 1,
            body = "Saw a shooting star last night and wished we'd always be friends. Don't laugh. OK you can laugh a little." },
        new Letter { type = LetterType.Message, from = "a neighbor",
            body = "Thank you for helping around town. People notice, you know. The street feels kinder lately." },
        new Letter { type = LetterType.Message, from = "Mama", minStage = 2,
            body = "The house is so quiet without you. Your old room still has your drawings on the wall. We miss you." },

        // loop 2+: someone who knows
        new Letter { type = LetterType.Message, from = "???", repeatLoopsOnly = true,
            body = "You've been here before. Keep learning. Every skill you gain now will matter when it comes." },
        new Letter { type = LetterType.Message, from = "???", repeatLoopsOnly = true,
            body = "{threat} again. Not today. But soon. Help the ones who need you, and get ready." },
    };

    // ── Bulletin board notices (the rest of the board) ────────────────────

    public static readonly string[][] Notices =
    {
        new[]   // Elementary
        {
            "LOST: orange cat. Answers to \"Muning\". Usually asleep on someone's doorstep.",
            "Ice candy for sale! 5 pesos. Ask Jun.",
            "Basketball at the plaza after school. Everyone can play, even if you're short!",
            "Reminder: no swimming in the river after it rains.",
            "Reading corner at the barangay hall every Saturday. Free stories!",
        },
        new[]   // High School
        {
            "Free math tutoring. Look for Ms. Garcia's room.",
            "Robotics club needs members (and screws).",
            "Band practice moved to Saturday. Sorry, neighbors.",
            "SK clean-up drive this month. Sign up at the hall!",
            "Found: one left shoe, size 7. Ask at the canteen.",
        },
        new[]   // College
        {
            "Room for rent near campus. Quiet people only, please.",
            "Thesis survey! Please answer. 5 minutes only, promise.",
            "Job fair next month. Bring your resume and your best smile.",
            "Free wifi at the net cafe... for real this time.",
            "Volunteers needed for the river clean-up. Free lunch!",
        },
    };

    public static readonly string[] PrepNotices =
    {
        "Barangay notice: Know your evacuation route. Ask at the hall if you're not sure.",
        "Get a go-bag ready: water, a flashlight, a radio, medicine and copies of your papers.",
        "Listen to the radio for weather warnings. Signal numbers mean it's time to get ready.",
    };
}
