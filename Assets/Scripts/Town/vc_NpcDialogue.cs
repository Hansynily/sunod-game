using System.Collections.Generic;

/// <summary>
/// What the townsfolk say when you tap them, Stardew-style: everyone has their own voice,
/// and says one thing per day (it changes every morning). Some lines are little game tips,
/// told the way that person would say it.
///
/// Quest givers are keyed by their npcName (set by vc_TownBuilder); roamers by the names
/// given out in vc_TownLife. Unknown names fall back to a small shared pool.
/// </summary>
public static class vc_NpcDialogue
{
    /// <summary>Today's line for this person (same all day, different tomorrow).</summary>
    public static string DailyLine(string stageId, string name, int day)
    {
        if (!Lines.TryGetValue(name ?? "", out string[] lines) || lines.Length == 0) lines = Fallback;
        int seed = Hash((stageId ?? "") + "/" + name);
        int i = ((seed % lines.Length) + lines.Length + day - 1) % lines.Length;   // a new line each day, in a per-person order
        return lines[i];
    }

    private static int Hash(string s)
    {
        unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h & 0x7fffffff; }
    }

    // ── roamers (named per town in vc_TownLife) ───────────────────────────

    public static readonly string[] ElementaryRoamers = { "Aling Pacing", "Kuya Bert", "Mika", "Mang Isko", "Tess", "Lola Betty" };
    public static readonly string[] HighSchoolRoamers = { "Ram", "Ate Lyn", "Mang Kanor", "Trixie", "Sir Gabe", "Nanay Lumen" };
    public static readonly string[] CollegeRoamers   = { "Patrick", "Ate Grace", "Mang Rudy", "Jess", "Bianca", "Tito Boy" };

    public static string[] RoamersFor(string stageId) =>
        stageId == "highschool" ? HighSchoolRoamers : stageId == "college" ? CollegeRoamers : ElementaryRoamers;

    /// <summary>Little things people say while they're busy at a daily happening.</summary>
    public static readonly Dictionary<string, string[]> EventLines = new Dictionary<string, string[]>
    {
        { "picnic",  new[] { "Want some? It's pandesal with too much butter. The best kind.", "We do this every time the sky is this blue. It's a rule." } },
        { "ball",    new[] { "Heads up! ...Sorry. It does that.", "First to drop it buys the halo-halo." } },
        { "tag",     new[] { "You're it! Just kidding. Unless you want to be.", "Can't talk, running! Hahaha!" } },
        { "kite",    new[] { "The wind's perfect today. Watch it climb!", "My kite's name is Captain. He's been up there since lunch." } },
        { "chat",    new[] { "Oh! We were just talking about you. Good things, promise.", "Did you hear? The bakery's trying a new ube bread." } },
        { "joggers", new[] { "Lap... nine... don't... stop... me...", "Join us tomorrow! Six in the morning. Bring water." } },
    };

    private static readonly string[] Fallback =
    {
        "Nice day for a walk, isn't it?",
        "Oh, hello! Don't mind me, just thinking about lunch.",
        "Everyone in town's been talking about you. In a good way!",
    };

    private static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
    {
        // ═══ Elementary ════════════════════════════════════════════════════
        { "Aling Rosa", new[] {
            "Naku, the rain last night filled all my buckets. Free water for the plants! Never waste a good storm.",
            "Tip from your Aling Rosa: if something's broken, a fixing kind of skill is always quickest. Don't overthink it.",
            "I tried to fix the gate myself this morning. The gate won. We go again tomorrow.",
            "Have you eaten? Don't go helping the whole town on an empty stomach.",
        } },
        { "Mang Ben", new[] {
            "River's calm today. Calm river, calm Ben.",
            "My lolo built the first bridge here with his bare hands. I built the second one. It fell. So now I build boats.",
            "You walk fast for someone so small. Four good deeds a day is plenty, then rest.",
            "Fish don't bite when you're impatient. People are the same.",
        } },
        { "Lolo Ador", new[] {
            "When I was your age, apo, this whole town was rice fields. Now look. A playground!",
            "My knees creak more than my floor ever did. Those, you can't fix.",
            "Listen to an old man: if a job is taking forever, it's not your hands, it's the tool. Try another skill.",
            "I nap after lunch, after merienda, and sometimes before lunch. Life is long, apo.",
        } },
        { "Tita Lorna", new[] {
            "Good morning! Or is it afternoon? Every hour is morning if you stretch first!",
            "Arms up, deep breath... see? Ten percent happier already. I made that number up, but it's true.",
            "Out of energy? Don't push it! Lie down in a shed and you'll bounce back tomorrow, like me.",
            "I walked around the plaza seven times today. The pigeons are getting tired of me.",
        } },
        { "Kuya Migs", new[] {
            "Did you know rabbits can see almost all the way behind them? You can't sneak up on those guys.",
            "I sat by the meadow for an hour and wrote down everything. Three rabbits, one beetle, zero regrets.",
            "Want to know how something works? Watch it long enough. That's basically all science is.",
            "The glowing patch on the floor is where your skills work. Step in first, then press.",
        } },
        { "Teacher Joy", new[] {
            "Remember, class... oh, you're not in my class. Old habit. Hello!",
            "Our mung beans sprouted overnight! The kids named one of them Kevin.",
            "The stars in a quest are just a guide. Doing it right matters more than doing it fast.",
            "Questions are the best part of learning. Ask me anything. Except math after five o'clock.",
        } },
        { "Nico", new[] {
            "I counted the clouds today. Forty-two. Then they moved and I had to start over.",
            "Do fish sleep? I asked my mom and she said go ask the fish.",
            "When I don't know which skill to use, I think about what the person really needs. Then I pick the one that sounds like that!",
            "My dream is to see a whale. A real one. Not a drawing.",
        } },
        { "Paolo", new[] {
            "Ba-dum, ba-dum-tss! I practice on everything. Tables. Pots. My little brother's head. Gently!",
            "Parade day is the best day of the year. Everybody dances. Even Principal Reyes. Badly.",
            "Music is just counting, but with feelings.",
            "Stuck in a quest? Try a different kind of skill. Sometimes the beat is off, not you.",
        } },
        { "Jun", new[] {
            "Banana cue, two for ten! Okay, three for ten, because you're my friend.",
            "My business plan: sell snacks, buy more snacks, sell those too. Genius.",
            "Tip: if your skills don't fit the job, get new ones! Every night before you sleep you get to pick one.",
            "Someday I'll own a whole mall. With a snack stall inside. Mine.",
        } },
        { "Mr. Tan", new[] {
            "Business is good when the kids are smiling. That's all the accounting I need.",
            "A yo-yo, a trumpo, a paper boat. The old toys still sell. Good things last.",
            "Every customer is a little different. Every job too. Read the person first, then act.",
            "I opened this store forty years ago with one shelf. Patience, hijo. Patience and inventory.",
        } },
        { "Ms. Dela Cruz", new[] {
            "Look at that sky. Orange on blue! Somebody up there knows color theory.",
            "There's no such thing as a bad drawing. Only one that isn't finished yet.",
            "Glitter is wonderful. Glitter is also forever. Choose wisely.",
            "The best ideas come when you stop trying so hard. Go for a walk. Look at things.",
        } },
        { "Principal Reyes", new[] {
            "Walking, not running, in the hallways. Also in town. Mostly.",
            "I have a key for every door in the school. I never know which one is which.",
            "A tidy desk is a tidy mind. My desk is... a work in progress.",
            "Keep an eye on the ring in the corner. It shows how far you've come. Every neighbor counts.",
        } },
        { "Bea", new[] {
            "I wrote a song about the wind. It goes whoooo. That's the whole song so far.",
            "Everything rhymes if you sing it loud enough.",
            "Sometimes I hum while I walk and the birds hum back. Okay, maybe not. But maybe!",
            "Done helping for today? Then go rest! Even singers need sleep.",
        } },
        { "Ate Mia", new[] {
            "The little ones had three snacks, two fights and one nap. A normal day!",
            "Being kind isn't hard. It just takes a little noticing.",
            "If someone looks lost, just ask. Most people are only waiting for someone to say hello.",
            "You can tap anyone you're standing next to, you know. Everybody here has something to say.",
        } },
        { "Toto", new[] {
            "I can read the long words now. Like... ex-tra-or-di-na-ry. See?",
            "My favorite book is about a turtle who becomes king. He's slow, but he's fair.",
            "Thanks for being nice to me. Not everybody is.",
            "When I grow up I want to write books. Or be a turtle king. Whichever comes first.",
        } },
        { "Manang Fe", new[] {
            "Adobo today! Don't ask for the recipe. Secrets are secrets.",
            "Exact change, please. I'm joking. But also, please.",
            "Every centavo counts, anak. Count twice, give once.",
            "If you run out of energy, you sleep. If you're hungry, you come to me. Simple.",
        } },

        // ═══ High School ═══════════════════════════════════════════════════
        { "Kuya Dan", new[] {
            "Signal's crisp today. I can catch a station all the way from the city if I hold the antenna and stand on one foot.",
            "Anything with wires is just a puzzle that buzzes.",
            "Most quests have more than one way to solve them. The best-fit skill just gets there faster.",
            "I took apart our TV when I was eight. My mom still hasn't forgiven me. It works better now, though.",
        } },
        { "Ate Carla", new[] {
            "Paint on my shoes again. At this point they're art.",
            "The play needs a sunset backdrop. I'm mixing nine kinds of orange to get the right one.",
            "Don't copy anyone's style. Yours is weird in the right way.",
            "Tired? Rest. Bad art happens at 2 AM. Trust me.",
        } },
        { "Mr. Santos", new[] {
            "Do re mi... you're slightly flat. Kidding! Good afternoon.",
            "A choir is twenty people agreeing to breathe at the same time. That's the magic.",
            "When you lead people, don't shout. Show them.",
            "The ring in the corner fills up as you help more people. Think of it as your applause meter.",
        } },
        { "Liza", new[] {
            "Thirty-one bottles from the river this morning. Thirty-two if you count the one I drank.",
            "Small things add up. One bag of trash, one kind word. It all counts.",
            "The river looks happier today. I know rivers can't be happy. But it does.",
            "If you sleep before helping everyone, don't worry. They'll still be here tomorrow. Nobody gets left behind.",
        } },
        { "Prof. Lim", new[] {
            "The river is a book written in mud. You just have to learn to read it.",
            "Hypothesis: I need more coffee. Experiment: in progress.",
            "Measure first, guess second. Never the other way around.",
            "Each quest has its own best kind of skill. Think about what the job really is before you press anything.",
        } },
        { "Mang Tonyo", new[] {
            "Three boats in, two boats out. One is still deciding.",
            "Forty years on this dock. I know every plank by name. This one's Bert.",
            "Write it down. If it isn't written down, it didn't happen.",
            "Heavy fog this morning. Shipped a crate of mangoes to the wrong island. Again.",
        } },
        { "Coach Ramon", new[] {
            "Practice doesn't make perfect. Practice makes it work most of the time. That's enough.",
            "Our robot fell off the table twice today. Character building!",
            "Hustle! Four quests a day, then rest. Champions sleep.",
            "Test everything. Then test it again. Then trust it.",
        } },
        { "Ms. Garcia", new[] {
            "Two hundred and six bones in the human body. I've named all of them. Don't ask.",
            "Your heart beats about a hundred thousand times a day. Make a few of them count.",
            "Curiosity isn't a distraction. It's the whole point.",
            "Lost in a quest? Look around the room. The things in it usually hint at the skill that fits.",
        } },
        { "Mrs. Ong", new[] {
            "A to Z. Then by year. Then by color. I'm not obsessive, I'm organized.",
            "Somebody filed 'Banana' under 'Fruit, yellow.' I'm still recovering.",
            "A good system saves you a hundred small headaches.",
            "Your progress is saved online, so you can keep playing on another phone. Isn't that tidy?",
        } },
        { "Joy", new[] {
            "Glue gun's warm. Let's build something!",
            "Two hundred kits this week. My fingers are mostly glue now.",
            "If it's broken, fix it. If it's not broken, make it better.",
            "Out of energy? Nearest shed, lie down, done. Don't walk all the way home, silly.",
        } },
        { "Marco", new[] {
            "Picture this: sisig, but in a cone. I'm telling you, it's the future.",
            "Confidence is ninety percent of a pitch. The other ten is having a good product.",
            "If they say no, you just pitch it better tomorrow.",
            "Future CEO tip: pick a skill that fits the job and you finish faster. Faster means more stars.",
        } },
        { "Aling Nena", new[] {
            "The eggplants did well this season. The tomatoes are sulking.",
            "My notebook says I picked 312 kalamansi this week. My back says a thousand.",
            "Plant, water, wait. Most good things work like that.",
            "Count carefully, anak. Numbers don't lie, but tired eyes do.",
        } },
        { "Direk Paulo", new[] {
            "Places, everyone! ...Oh, it's just you. Hi.",
            "All the world's a stage, and ours has a squeaky trapdoor. Mind the left side.",
            "A good scene needs a little silence. Not everything has to be loud.",
            "Feeling lost? Step back and look at the whole picture. Directors do that all day.",
        } },
        { "Lola Ising", new[] {
            "Come here, let me look at you. You've grown! Last week you were this tall.",
            "My garden, my cat, my radio drama. What else does a woman need?",
            "Be gentle with people, iho. Everyone is carrying something.",
            "When the stars start going out in a quest, don't panic. Slow and careful still gets it done.",
        } },
        { "Mang Ramil", new[] {
            "Fresh cut? First one's free. Second one's free too, if you sweep.",
            "In this chair, people tell me everything. I know all the town's secrets. I keep them.",
            "Talking to people is a skill like any other. Practice it.",
            "People ask for help where they live, not just by the plaza. Walk around, look for the '!'.",
        } },
        { "Ate Joan", new[] {
            "Everything here has a story. This jacket survived three typhoons and one wedding.",
            "One person's old shirt is another person's lucky shirt.",
            "Bargaining is just friendly arguing.",
            "If a quest feels impossible, check your skill slots. Maybe you need a different kind.",
        } },

        // ═══ College ═══════════════════════════════════════════════════════
        { "Dr. Cruz", new[] {
            "Bioluminescence! I've waited my whole career to use that word casually.",
            "The forest is the oldest lab in the world. We're just borrowing it.",
            "Every answer gives you two new questions. That's how you know you're doing it right.",
            "Not sure which skill fits? Looking closer never hurts. Inspect or Survey usually tells you something.",
        } },
        { "Ms. Rivera", new[] {
            "Everyone asks me what they should become. I ask them back: what makes you lose track of time?",
            "No career is a straight line. Mine went teacher, nurse, baker, counselor.",
            "The quests you shine in and the skills you reach for say a lot about you. Pay attention to that.",
            "Finish all 48 and you'll see where your path leans. It's not a box. It's a map.",
        } },
        { "Ana", new[] {
            "My centrifuge sounds like a jet engine. I named it Hector.",
            "Label everything. Future you is a stranger who won't remember.",
            "Lab rule one: no eating in the lab. Lab rule two: see rule one, Kevin.",
            "Precision first, speed second. The stars come once your hands know the work.",
        } },
        { "Kevin", new[] {
            "Headline: Local Student Eats Lunch at 3 PM. It's a slow news day.",
            "Everyone has a story. My job is to keep asking until they tell it.",
            "Deadline in two hours and I've written the title. Progress!",
            "Fun fact: I'm banned from the bio lab. Ana's still mad about the siopao.",
        } },
        { "Mr. Lee", new[] {
            "Welcome! Oh, you're not a guest. Welcome anyway.",
            "Good service is remembering names. Bad service is calling everyone 'sir.'",
            "The secret to chaos: smile, breathe, do the most important thing first.",
            "Plan your day. Four energy, four people. Choose who you help first.",
        } },
        { "Ms. Uy", new[] {
            "Debit on the left, credit on the right, coffee in the middle.",
            "Money is easy. People and money together, that's the hard part.",
            "Save a little every day. Forty-year-old you will say thank you.",
            "Every quest you finish is saved online, the moment you finish it. Your progress is safe.",
        } },
        { "Ms. Ria", new[] {
            "Meetings could be emails. Emails could be nods. Nods are the future.",
            "A good leader listens more than they talk. I'm still working on that.",
            "Delegate! Not everything has to be done by you.",
            "You've grown so much since elementary, you know. I can tell by how you carry yourself.",
        } },
        { "Mr. Go", new[] {
            "Payday is the happiest day of the month. For everyone except me.",
            "I double-check every envelope. Then I triple-check the ones I double-checked.",
            "A spreadsheet is just a very quiet friend.",
            "Little mistakes become big ones when nobody looks. So look.",
        } },
        { "Jay", new[] {
            "Computer 7 is haunted. Or it's the mouse. Probably the mouse.",
            "Nineteen hours awake. Coffee is my operating system.",
            "Ctrl+S. Always. Learn that and you'll survive college.",
            "Quitting in the middle of a quest counts as a fail. Finish what you start, even if you're losing.",
        } },
        { "Dr. Santos", new[] {
            "The cat from yesterday is eating again. Best news all week.",
            "Animals can't tell you what's wrong. You have to notice.",
            "Be patient with the scared ones. They're usually the sweetest, after.",
            "Think about the problem before you grab a tool. The right skill makes the job half as long.",
        } },
        { "Agent Mel", new[] {
            "Location, location, location! And a working faucet.",
            "I can sell a house in one walk-through. I can't sell my old car to save my life.",
            "First impressions matter. So do second ones. People forget the first.",
            "Know what the buyer wants before you start talking. Same with any job.",
        } },
        { "Kuya Joel", new[] {
            "How are you, really? Not the polite answer. The real one.",
            "It's okay to have a heavy day. Just don't carry it alone.",
            "Sometimes the most helpful thing you can say is nothing. Just stay.",
            "Take care of yourself too, okay? When the day is done, rest. You can't pour from an empty cup.",
        } },
        { "Aling Cora", new[] {
            "My kids argued about everything today. This morning it was the color of the sky.",
            "Families are like rice. A little sticky, but they hold together.",
            "Eat together whenever you can. Problems look smaller from the dinner table.",
            "Salamat for always helping. Somebody out there is very proud of you.",
        } },
        { "Mang Erning", new[] {
            "If it rattles, tighten it. If it squeaks, oil it. If it smokes, run.",
            "Forty years with these machines. They talk. You just have to listen.",
            "Measure twice, cut once. Grind three times if you're me.",
            "A good tool on the wrong job is a bad tool. Match the skill to the work.",
        } },
        { "Direk Bong", new[] {
            "Lights, camera, action, and then a whole lot of waiting. That's filmmaking.",
            "We shot the same scene twenty times today. Take seven was the best.",
            "Every good story has a moment where everything goes wrong. That's where it gets good.",
            "Be brave with your choices. Safe choices make boring movies.",
        } },
        { "Engr. Paz", new[] {
            "Kilowatts, megawatts... I love watts. Engineer joke. You'll laugh in four years.",
            "When the power goes out, everyone suddenly remembers the engineer.",
            "Build it to last first. Make it look nice second.",
            "Heard the storm warnings? The generator will hold. I made sure.",
        } },

        // ═══ Roamers: Elementary ═══════════════════════════════════════════
        { "Aling Pacing", new[] {
            "Did you hear? Mang Ben's boat has a new name. He won't tell anyone what it is. Suspicious.",
            "I don't gossip, anak. I share news early.",
            "Your house is getting fixed up nicely! Every time you help someone, it gets a little stronger. I've been watching.",
            "Lolo Ador says he was a dancer in his youth. I have questions.",
            "I saw a lizard in my kitchen this morning. We've agreed to share.",
        } },
        { "Kuya Bert", new[] {
            "Sorbetes! Cheese, ube, chocolate! Ring ring!",
            "Best flavor? Cheese. Don't argue with a professional.",
            "I've pushed this cart up every hill in town. My legs are basically tree trunks now.",
            "Long day? The trick is to rest before you're too tired. Sheds are all over town, you know.",
            "A kid paid me in bottle caps once. I gave him a scoop anyway. Business is about heart.",
        } },
        { "Mika", new[] {
            "This is Chiquita. She's a chicken. She's also my best friend.",
            "Chiquita laid an egg today! Okay, it was a rock. But she looked very proud.",
            "Wanna know a secret? If you tap someone you're standing next to, they talk to you! Even me!",
            "My kuya says chickens can't fly. Chiquita flew onto the roof once. So.",
            "I'm going to be a farmer. Or an astronaut. Chiquita votes farmer.",
        } },
        { "Mang Isko", new[] {
            "Tricycle's out of gas, so today I walk like everyone else. Very humbling.",
            "Twenty pesos to anywhere in town. Thirty if you're carrying a goat.",
            "I know every road here with my eyes closed. Please don't make me prove it.",
            "Before you ride, decide where you're going. Same with a quest. Know what it needs first.",
            "My radio only gets one station. Lucky for me, I love that song.",
        } },
        { "Tess", new[] {
            "Look, a heart-shaped rock! That's number forty in my collection.",
            "Rocks are good listeners. They never interrupt.",
            "Some rocks sparkle if you hold them to the sun. Try it! ...Okay, not this one.",
            "Helping people is like finding good rocks. You have to look carefully.",
            "Mama says I can't bring rocks to the dinner table anymore. Rude.",
        } },
        { "Lola Betty", new[] {
            "This is Bantay. He's thirteen. Same as a dog's ninety. We're about the same age.",
            "Bantay barked at a leaf for ten minutes today. He won.",
            "Slow down, iho. The town will still be here. So will I, God willing.",
            "When I was young, we had a whole day for each good deed. You young ones do four before lunch!",
            "Bantay likes you. He doesn't like anyone. Except the sorbetero.",
        } },

        // ═══ Roamers: High School ══════════════════════════════════════════
        { "Ram", new[] {
            "Learned a new chord today. It sounds terrible. I love it.",
            "My band has three members and zero songs. We're still deciding on a name.",
            "Pro gamer tip: the quest timer doesn't wait. Pick fast, but pick right.",
            "Skated into the canteen door this morning. The door's fine. I'm okay too, mostly.",
            "Everyone's stressed about exams. I'm stressed about the guitar solo. Priorities.",
        } },
        { "Ate Lyn", new[] {
            "Student council meeting at four! No, you can't skip it. Okay, you can, you're not a member.",
            "I have three planners. One for school, one for council, one to plan the other two.",
            "Help everyone in a stage and you get to move on to the next. Very organized, I approve.",
            "Our fundraiser made two thousand pesos! And one very confused rooster. Long story.",
            "Leadership tip: say thank you a lot. People remember it.",
        } },
        { "Mang Kanor", new[] {
            "Taho! Taaahooo! Warm and sweet, extra sago for you.",
            "I've woken up this whole street every morning for twenty years. They hate me. They love me.",
            "Don't skip breakfast. Then you'll have energy for four whole quests. Science!",
            "The secret to good taho is patience and a strong voice.",
            "My son wants to be a programmer. I said fine, but first, carry the taho.",
        } },
        { "Trixie", new[] {
            "Hi guys, welcome back to my channel! ...Oh. You're not my viewers. Hi anyway!",
            "Today's vlog: 'I Tried Every Snack at the Fair.' Spoiler: my stomach did not survive.",
            "Like and subscribe! Wait, can you even subscribe in real life?",
            "Content idea: following you around while you help people. Kidding. Unless?",
            "Twelve views on my last video. Three were my mom. Growing!",
        } },
        { "Sir Gabe", new[] {
            "Jog, don't walk! Your heart will thank you. Your legs won't.",
            "Twenty jumping jacks. Go! ...I'll count that as a maybe.",
            "Even athletes rest. When your energy's gone, sleep. Coach's orders.",
            "I used to play for the province. Now I mostly blow the whistle. Same energy.",
            "Teamwork wins games. Kindness wins everything else.",
        } },
        { "Nanay Lumen", new[] {
            "Sari-sari store's open till nine. Yes, we have load. No, we don't have change for a thousand.",
            "Everybody owes me a little. I write it all in my notebook. The notebook never forgets.",
            "Buy one candy, get one candy. That's not a promo, that's just how candy works.",
            "You help people the way my husband used to. Quietly, without being asked.",
            "Rain's coming tonight. My knee says so, and my knee is never wrong.",
        } },

        // ═══ Roamers: College ══════════════════════════════════════════════
        { "Patrick", new[] {
            "Thesis status: chapter one, page one, word one. Going great.",
            "My adviser said 'interesting.' I don't know if that's good or bad.",
            "Sleep is for people who've finished their thesis. So, not me.",
            "If a quest goes badly, it's okay. Everything counts, the good and the bad. That's data!",
            "Third coffee today. My hands are vibrating at a frequency only dogs can hear.",
        } },
        { "Ate Grace", new[] {
            "Iced coffee, oat milk, extra shot? I already know your order. I know everyone's order.",
            "I can tell how someone's day is going by how they say 'good morning.'",
            "You looked tired yesterday. Get some sleep tonight, okay?",
            "Coffee art tip: it's all in the wrist. Life tip: same thing.",
            "Somebody left a tip that just said 'you're doing great.' I kept it on the register.",
        } },
        { "Mang Rudy", new[] {
            "Good evening po... I mean, good morning. Night shift brain.",
            "I've guarded this campus for fifteen years. Nothing has ever happened. That's how you know I'm good.",
            "Lost? Everything is walking distance if you're patient.",
            "The kids think I'm strict. I just want everyone to get home safe.",
            "They say a storm's coming soon. Stay close to the people you love, okay?",
        } },
        { "Jess", new[] {
            "Five kilometers before breakfast! Okay, three. Okay, one. But I ran it very sincerely.",
            "Pace yourself. The ones who sprint first are the ones walking at the end.",
            "Running clears your head. So does helping someone, oddly enough.",
            "Lost my water bottle again. That's the fourth one this month.",
            "Your best stars come when you don't panic. Breathe, then move.",
        } },
        { "Bianca", new[] {
            "I'm painting the whole city in blue. Don't ask why. Art doesn't explain itself.",
            "My professor called my work 'brave.' I think that means confusing.",
            "Every face in this town would make a good portrait. Even yours. Especially yours.",
            "When you're stuck, change something small. A color, a skill, a direction.",
            "I sold my first painting! For two hundred pesos. To my tita. It still counts.",
        } },
        { "Tito Boy", new[] {
            "See that bird? Maya. The little brown one. Smartest bird in the Philippines.",
            "Retired three years now. I have never been busier, and I don't know how.",
            "Back in my day we didn't have phones. We had patience. And very bad maps.",
            "Do your best, iho. And if your best isn't enough today, there's always tomorrow.",
            "My wife says I talk to the birds too much. The birds have never complained.",
        } },
    };
}
