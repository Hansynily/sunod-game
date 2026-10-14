using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace SunodGame.Core
{
    /// <summary>
    /// Cozy SFX player (no music). Self-creating DontDestroyOnLoad singleton; clips are the
    /// synthesized WAVs in Resources/Sfx, loaded by file name ("ui_click", "quest_complete"...).
    ///
    /// Wires most sounds itself so gameplay scripts stay untouched:
    ///  - every UI Toolkit button click on every panel (sound chosen by button name)
    ///  - skill used / skill gathered / skill zone entered / quest start + end
    /// Scripts with moment-specific sounds (footsteps, typing, doors, timer) call Play().
    /// Volume persists in PlayerPrefs "sunod.sfx_volume" (0..1).
    /// </summary>
    public class vc_Sfx : MonoBehaviour
    {
        private const string VolumePrefKey = "sunod.sfx_volume";
        private const int PoolSize = 8;

        private static vc_Sfx _instance;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly HashSet<IPanel> _hookedPanels = new HashSet<IPanel>();
        private AudioSource[] _pool;
        private int _next;
        private float _volume = 1f;

        // Instance-event hooks (objects come and go with scenes).
        private vc_SkillManager _hookedSkills;
        private vc_PlayerInventory _hookedInventory;
        private vc_QuestTimer _hookedTimer;

        public static float Volume
        {
            get => _instance != null ? _instance._volume : PlayerPrefs.GetFloat(VolumePrefKey, 1f);
            set
            {
                float v = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumePrefKey, v);
                if (_instance != null) _instance._volume = v;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[Sunod SFX]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<vc_Sfx>();
        }

        /// <summary>Play a clip by name. pitchJitter = random +/- semitone-ish spread for variety.</summary>
        public static void Play(string clipName, float volume = 1f, float pitchJitter = 0f, float minInterval = 0f)
        {
            if (_instance != null) _instance.PlayInternal(clipName, volume, pitchJitter, minInterval);
        }

        /// <summary>Play one of name_0 .. name_{count-1} at random (footstep variations).</summary>
        public static void PlayRandom(string baseName, int count, float volume = 1f, float pitchJitter = 0.06f)
        {
            Play($"{baseName}_{Random.Range(0, count)}", volume, pitchJitter);
        }

        private void Awake()
        {
            _volume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);

            foreach (AudioClip clip in Resources.LoadAll<AudioClip>("Sfx"))
                _clips[clip.name] = clip;

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool[i] = src;
            }

            vc_SkillZone.PlayerEnteredZone += OnZoneEntered;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            vc_SkillZone.PlayerEnteredZone -= OnZoneEntered;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Panels are re-created per scene; HookPanels picks the new ones up.
        }

        private void Update()
        {
            HookPanels();
            HookGameplay();
        }

        // ── Playback ─────────────────────────────────────────────────────────

        private void PlayInternal(string clipName, float volume, float pitchJitter, float minInterval)
        {
            if (_volume <= 0f || string.IsNullOrEmpty(clipName)) return;
            if (!_clips.TryGetValue(clipName, out AudioClip clip) || clip == null) return;

            float now = Time.unscaledTime;
            if (minInterval > 0f && _lastPlayed.TryGetValue(clipName, out float last) && now - last < minInterval) return;
            _lastPlayed[clipName] = now;

            AudioSource src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.pitch = pitchJitter > 0f ? 1f + Random.Range(-pitchJitter, pitchJitter) : 1f;
            src.PlayOneShot(clip, Mathf.Clamp01(volume) * _volume);
        }

        // ── UI Toolkit: one trickle-down click hook per panel catches every button ──

        private void HookPanels()
        {
            foreach (UIDocument doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                IPanel panel = doc.rootVisualElement?.panel;
                if (panel == null || _hookedPanels.Contains(panel)) continue;
                _hookedPanels.Add(panel);
                panel.visualTree.RegisterCallback<ClickEvent>(OnPanelClick, TrickleDown.TrickleDown);
                panel.visualTree.RegisterCallback<ChangeEvent<bool>>(_ => Play("ui_click", 0.8f, 0f, 0.05f), TrickleDown.TrickleDown);
            }
            _hookedPanels.RemoveWhere(p => p == null || p.visualTree == null || p.visualTree.panel == null);
        }

        private void OnPanelClick(ClickEvent evt)
        {
            Button button = FindButton(evt.target as VisualElement);
            if (button == null || !button.enabledInHierarchy) return;

            string clip = ClipForButton(button.name ?? string.Empty);
            if (clip != null) Play(clip, 0.8f, 0f, 0.05f);   // no pitch jitter: one steady click
        }

        private static Button FindButton(VisualElement el)
        {
            for (VisualElement e = el; e != null; e = e.parent)
                if (e is Button b) return b;
            return null;
        }

        // One consistent soft click for every menu button (easy on the ear over long sessions).
        private static string ClipForButton(string n)
        {
            if (n.StartsWith("slot-btn-")) return null;                 // skill_use comes from SkillManager
            if (n.StartsWith("slot-")) return "skill_equip";             // inventory equip slots
            if (n == "btn-skip") return "cut_whoosh";
            return "ui_click";
        }

        // ── Gameplay events ─────────────────────────────────────────────────

        private void HookGameplay()
        {
            vc_SkillManager skills = vc_SkillManager.Instance;
            if (skills != _hookedSkills)
            {
                if (_hookedSkills != null) _hookedSkills.SkillUsed -= OnSkillUsed;
                _hookedSkills = skills;
                if (skills != null) skills.SkillUsed += OnSkillUsed;
            }

            vc_PlayerInventory inv = vc_PlayerInventory.Instance;
            if (inv != _hookedInventory)
            {
                if (_hookedInventory != null) _hookedInventory.OnSkillAdded -= OnSkillAdded;
                _hookedInventory = inv;
                if (inv != null) inv.OnSkillAdded += OnSkillAdded;
            }

            vc_QuestTimer timer = vc_QuestTimer.Instance;
            if (timer != _hookedTimer)
            {
                if (_hookedTimer != null) _hookedTimer.QuestEnded -= OnQuestEnded;
                _hookedTimer = timer;
                if (timer != null) timer.QuestEnded += OnQuestEnded;
            }
        }

        private void OnSkillUsed(int slot, vc_PlayerSkill skill) => Play("skill_use", 0.9f, 0.04f);
        private void OnSkillAdded(vc_SkillData data) => Play("skill_pickup", 0.9f, 0f, 0.1f);
        private void OnZoneEntered() => Play("zone_enter", 0.7f, 0f, 0.5f);

        private void OnQuestEnded(vc_QuestTimer.QuestCompletionResult result)
        {
            if (result == null) return;
            Play(result.DidPassQuest ? "quest_complete" : "quest_fail", 0.9f, 0f, 0.5f);
        }
    }
}
