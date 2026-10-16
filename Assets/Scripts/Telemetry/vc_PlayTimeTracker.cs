using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SunodGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SunodGame.Telemetry
{
    // Play-time telemetry, one record per app launch ("play session").
    //   active_seconds - focused, unpaused time (backgrounded / unfocused time is excluded)
    //   wall_seconds   - real time since the launch started
    //   scene_seconds  - active time per active scene
    //   pause_count    - times the app went to the background
    // Sent as a cumulative heartbeat every minute, when the app is backgrounded and on quit,
    // so a dropped request loses nothing (the next one carries the same totals). The server
    // sums launches into total play time per player. Nothing is sent until the player is
    // signed in; a different account signing in starts a fresh play session.
    public class vc_PlayTimeTracker : MonoBehaviour
    {
        public static vc_PlayTimeTracker Instance { get; private set; }

        private const float HeartbeatSeconds = 60f;
        // Longer frame gaps (e.g. an OS suspend the pause callback missed) are not play time.
        private const float MaxFrameSeconds = 5f;

        private string _appSessionId;
        private string _playerId;
        private float _activeSeconds;
        private float _wallStart;
        private int _pauseCount;
        private bool _focused = true;
        private bool _paused;
        private bool _ended;
        private bool _awaitingNewLogin;
        private float _sinceBeat;
        private readonly Dictionary<string, float> _sceneSeconds = new Dictionary<string, float>();

        public float ActiveSeconds => _activeSeconds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[PlayTimeTracker]");
            DontDestroyOnLoad(go);
            go.AddComponent<vc_PlayTimeTracker>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            StartNewSession(null);
        }

        private void StartNewSession(string playerId)
        {
            _appSessionId = Guid.NewGuid().ToString("N");
            _playerId = playerId;
            _activeSeconds = 0f;
            _wallStart = Time.realtimeSinceStartup;
            _pauseCount = 0;
            _sinceBeat = 0f;
            _ended = false;
            _sceneSeconds.Clear();
        }

        private void Update()
        {
            string currentPlayer = SessionState.Instance != null ? SessionState.Instance.AuthPlayerId : null;
            if (string.IsNullOrEmpty(currentPlayer))
            {
                if (_ended) _awaitingNewLogin = true;
            }
            else if (_awaitingNewLogin || (!string.IsNullOrEmpty(_playerId) && currentPlayer != _playerId))
            {
                // Signed in again after a logout, or another account: a fresh session.
                _awaitingNewLogin = false;
                StartNewSession(currentPlayer);
            }
            else if (string.IsNullOrEmpty(_playerId))
            {
                // First sign-in of this launch keeps the time played so far (login screen).
                _playerId = currentPlayer;
            }

            if (_ended || _paused || !_focused) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds);
            _activeSeconds += dt;

            string scene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(scene))
            {
                _sceneSeconds.TryGetValue(scene, out float s);
                _sceneSeconds[scene] = s + dt;
            }

            _sinceBeat += dt;
            if (_sinceBeat >= HeartbeatSeconds)
            {
                _sinceBeat = 0f;
                SendHeartbeat(false);
            }
        }

        private void OnApplicationFocus(bool hasFocus) => _focused = hasFocus;

        private void OnApplicationPause(bool pause)
        {
            _paused = pause;
            if (pause)
            {
                _pauseCount++;
                SendHeartbeat(false);
            }
        }

        private void OnApplicationQuit() => EndSession();

        // Final beat for this launch's account (quit, logout, privacy decline).
        public void EndSession()
        {
            if (_ended) return;
            SendHeartbeat(true);
            _ended = true;
        }

        private void SendHeartbeat(bool ended)
        {
            if (string.IsNullOrEmpty(_playerId) || TelemetryManager.Instance == null) return;
            // Only send for the account that owns this session's time.
            if (SessionState.Instance == null || SessionState.Instance.AuthPlayerId != _playerId) return;
            TelemetryManager.Instance.PutMyPlaySession(BuildJson(ended), null, _ => { });
        }

        private string BuildJson(bool ended)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"app_session_id\":\"").Append(_appSessionId).Append("\",");
            sb.Append("\"active_seconds\":").Append(_activeSeconds.ToString("0.0", inv)).Append(',');
            sb.Append("\"wall_seconds\":").Append((Time.realtimeSinceStartup - _wallStart).ToString("0.0", inv)).Append(',');
            sb.Append("\"pause_count\":").Append(_pauseCount).Append(',');
            sb.Append("\"platform\":\"").Append(Escape(Application.platform.ToString())).Append("\",");
            sb.Append("\"app_version\":\"").Append(Escape(Application.version)).Append("\",");
            sb.Append("\"scene_seconds\":{");
            bool first = true;
            foreach (var kvp in _sceneSeconds)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(Escape(kvp.Key)).Append("\":").Append(kvp.Value.ToString("0.0", inv));
            }
            sb.Append("},");
            sb.Append("\"ended\":").Append(ended ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static string Escape(string value) =>
            string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
