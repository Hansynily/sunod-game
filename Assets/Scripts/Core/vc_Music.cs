using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Core
{
    /// <summary>
    /// Background music. Self-creating DontDestroyOnLoad singleton (like vc_Sfx); tracks are
    /// the MP3s in Resources/Music, picked by what the player is doing:
    ///  - Menu    : login, main menu, cutscene, end screen
    ///  - Town    : towns, tutorial, corridor floors (anything else in-game)
    ///  - quest_1 / quest_2 : while a quest timer runs; they alternate quest to quest
    /// Switching crossfades between two sources, and a track keeps playing (no restart) when
    /// the next scene wants the same one. Volume persists in PlayerPrefs "sunod.music_volume",
    /// default 0.5 (half volume).
    /// </summary>
    public class vc_Music : MonoBehaviour
    {
        private const string VolumePrefKey = "sunod.music_volume";
        private const float DefaultVolume = 0.5f;
        private const float FadeSeconds = 1.2f;

        private const string TrackMenu = "Menu";
        private const string TrackTown = "Town";
        private static readonly string[] QuestTracks = { "quest_1", "quest_2" };

        private static vc_Music _instance;

        private AudioSource[] _sources;
        private int _active;            // index of the source that is fading in / playing
        private float _fade = 1f;       // 0 -> 1 progress of the current crossfade
        private string _current;
        private string _sceneTrack = TrackMenu;
        private int _questTrackIndex = -1;
        private bool _inQuest;
        private float _volume = DefaultVolume;

        public static float Volume
        {
            get => _instance != null ? _instance._volume : PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume);
            set
            {
                float v = Mathf.Clamp01(value);
                try { PlayerPrefs.SetFloat(VolumePrefKey, v); PlayerPrefs.Save(); } catch { }
                if (_instance != null) _instance._volume = v;
            }
        }

        private const float MuteFadeSeconds = 0.8f;
        private bool _muted;
        private bool _restartOnUnmute;
        private float _muteLevel = 1f;   // 1 = audible, 0 = silent

        /// <summary>
        /// Fades the music out (true) for story moments like the dream screens, or back in
        /// (false). Coming back starts the track that fits by then (e.g. the next town's) from
        /// the top, fading in, so it lands like a new cue rather than a resumed loop.
        /// </summary>
        public static void SetMuted(bool muted)
        {
            if (_instance == null || _instance._muted == muted) return;
            _instance._muted = muted;
            if (muted) _instance._restartOnUnmute = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[Sunod Music]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<vc_Music>();
        }

        private void Awake()
        {
            try { _volume = PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume); } catch { _volume = DefaultVolume; }

            _sources = new AudioSource[2];
            for (int i = 0; i < 2; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.priority = 0;          // never stolen by SFX voices
                src.volume = 0f;
                _sources[i] = src;
            }

            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneTrack = TrackForScene(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnActiveSceneChanged(Scene _, Scene next)
        {
            _sceneTrack = TrackForScene(next.name);
            // A full scene change ends any dream screen (e.g. final dream -> results), so a
            // mute can never outlive the screen that asked for it.
            if (_muted) SetMuted(false);
        }

        // Additive town/floor loads don't always change the active scene; any gameplay scene
        // that loads means we're in-game.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Additive && TrackForScene(scene.name) == TrackTown) _sceneTrack = TrackTown;
        }

        private static string TrackForScene(string scene)
        {
            switch (scene)
            {
                case SceneLoader.SCENE_LOGIN:
                case SceneLoader.SCENE_MAINMENU:
                case SceneLoader.SCENE_CUTSCENE:
                case SceneLoader.SCENE_END:
                    return TrackMenu;
                default:
                    return TrackTown;
            }
        }

        private void Update()
        {
            // Quest music while a quest timer runs (towns and corridor floors alike).
            vc_QuestTimer timer = vc_QuestTimer.Instance;
            bool inQuest = timer != null && timer.IsRunning() && _sceneTrack == TrackTown;
            if (inQuest && !_inQuest) _questTrackIndex = (_questTrackIndex + 1) % QuestTracks.Length;
            _inQuest = inQuest;

            string wanted = inQuest ? QuestTracks[Mathf.Max(0, _questTrackIndex)] : _sceneTrack;

            // muted (dream screen): fade out, and hold any track change until it's over
            _muteLevel = Mathf.MoveTowards(_muteLevel, _muted ? 0f : 1f, Time.unscaledDeltaTime / MuteFadeSeconds);
            if (_muted)
            {
                if (_muteLevel <= 0f)
                    foreach (AudioSource s in _sources) if (s.isPlaying) s.Pause();
            }
            else if (_restartOnUnmute)
            {
                _restartOnUnmute = false;
                _muteLevel = 1f;          // the fresh cue does its own fade-in from silence
                foreach (AudioSource s in _sources) { s.Stop(); s.volume = 0f; }
                _current = null;
            }
            if (!_muted && wanted != _current) SwitchTo(wanted);

            // crossfade + live volume
            _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / FadeSeconds);
            AudioSource on = _sources[_active], off = _sources[1 - _active];
            on.volume = _volume * _fade * _muteLevel;
            off.volume = _volume * (1f - _fade) * _muteLevel;
            if (_fade >= 1f && off.isPlaying) off.Stop();
        }

        private void SwitchTo(string track)
        {
            _current = track;
            AudioClip clip = Resources.Load<AudioClip>("Music/" + track);
            if (clip == null)
            {
                Debug.LogWarning($"[vc_Music] Missing Resources/Music/{track}");
                return;
            }

            _active = 1 - _active;
            AudioSource src = _sources[_active];
            src.clip = clip;
            src.time = 0f;
            src.volume = 0f;
            src.Play();
            _fade = 0f;
        }
    }
}
