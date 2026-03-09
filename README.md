Run with (important!): [Sunod Game Server](https://github.com/Hansynily/sunod-game-server)

# SUNOD - Students Unlocking New Occupational Directions


Roguelite meets career discovery. SUNOD is a 2D mobile roguelite where high school and college students explore a modern-fantasy world, collect magic skills, and face exam-style evaluations. Each run reveals insights about which career paths actually fit them.

---
## TO-DO-s
- [x] Implement actual user authentication
- [ ] New different score system on each quest
- [ ] asdasdasd Model


## About the Game

| Property | Details |
|---|---|
| **Genre** | Modern Fantasy Puzzle Roguelite / Immersive Sim / Raising Simulator |
| **Platform** | Android (Mobile / Tablet) |
| **View** | Top-down, 2D orthographic |
| **Players** | Single-player |

**The core loop:** Each run starts in a procedurally generated map. Players manage a schedule bar and energy meter, accepting quests from NPCs, collecting magic skills tied to RIASEC career categories, and using those skills to solve puzzles and overcome challenges. Death means starting a new run with randomized skills and quests that also encourages players to explore different play styles and career paths.

---


## Repository Structure

```
SUNOD/
├── Assets/
│   ├── Scripts/          # C# game logic
│   ├── Scenes/           # Unity scene files
│   ├── Prefabs/          # Reusable game objects
│   ├── Art/              # Sprites, tilesets, pixel art assets
│   ├── Audio/            # SFX and music
│   └── UI/               # HUD, menus, overlays
├── Packages/             # Unity package dependencies
├── ProjectSettings/      # Unity project configuration
└── README.md
```

---


## Tech Stack

- **Engine:** Unity (LTS recommended)
- **Language:** C#
- **Platform Target:** Android (OpenGL ES 3.0)
- **Minimum OS:** Android 8.0 (Oreo / API 26)
- **Min RAM:** 1 GB

---


## Getting Started

### Prerequisites

- Unity Hub + Unity Editor (Version 6000.3.10f1) (With also Android Build Support module installed via Unity Hub)
- Android SDK & JDK configured in Unity preferences


## Local Telemetry Server

This game uses telemetry and login system to track player sessions and career data.  
The backend: **[sunod-telemetry](https://github.com/Hansynily/sunod-telemetry)**

Clone and run the telemetry server locally before hitting Play in the editor, or career tracking and login will not function.

Refer to the telemetry repo's README for setup instructions.

> **If you wish to bypass the user portal scene:**  
> Open `Assets/Scenes/DemoPlayScene.unity` directly in Unity and hit Play so you can jump straight into the game.


### Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/sunod-game/sunod.git
   cd sunod
   ```

2. Open the project in Unity Hub by pointing it to the cloned folder.

3. Let Unity resolve packages and import assets on first open (this may take a few minutes).

4. Set build target to Android:
   `File → Build Settings → Android → Switch Platform`

5. Open the main scene and hit Play to run in the editor.

---

## Building for Android

1. Go to `File → Build Settings → Android`
2. Configure `Player Settings` (package name, icons, minimum API level → 26)
3. Click **Build** or **Build and Run** with a device connected

---

## Contributing

1. Branch off `demo` for new features: `git checkout -b feature/your-feature`
3. Please keep commits focused and descriptive
3. Open a pull request against `demo` with a clear description of changes

---

## License

This project is a thesis research project. All rights reserved by the Team Bravo unless otherwise stated.

