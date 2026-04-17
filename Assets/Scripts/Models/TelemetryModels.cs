using System;
using System.Collections.Generic;

namespace SunodGame.Models
{
    //  Simple register. Backend generates player_id server-side.
    //  Used only when you want to pre-register without a run.


    [Serializable]
    public class UserCreateRequest
    {
        public string name;
        public string birthdate;
        public string gender;
        public string username;
        public string password;
        public string email;
    }

    [Serializable]
    public class UserLoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    public class AuthResponse
    {
        public int    id;
        public string player_id;
        public string username;
        public string email;
        public string name;
        public string birthdate;
        public string gender;
        public string created_at;
        public string last_login;
        public string role;
        public string approval_state;
        public string email_verification_state;
        public bool   tutorial_completed;
        public string tutorial_completed_at;
        public bool   can_login;
        public string next_step;
        public string message;
        public string access_token;
        public string token_type;
    }

    [Serializable]
    public class ErrorResponse
    {
        public string detail;
    }

    [Serializable]
    public class TutorialCompletionResponse
    {
        public bool success;
        public string message;
        public bool tutorial_completed;
        public string tutorial_completed_at;
    }

    // Legacy per-quest telemetry payloads. The active demo flow now uses run-complete telemetry.

    [Serializable]
    public class SelectedSkill
    {
        public string riasec_code;
        public string skill_name; 
    }

    [Serializable]
    public class QuestAttemptTelemetryIn
    {
        public string player_id;          // unique per device/player, use SystemInfo.deviceUniqueIdentifier
        public string username;
        public string email;              // optional, can be "".
        public string quest_id;           // e.g. "floor_01", "dungeon_boss"
        public string quest_result;       // "success" or "failure"
        public int    time_spent_seconds;
        public List<SelectedSkill> selected_skills = new();
    }

    [Serializable]
    public class QuestAttemptTelemetryOut
    {
        public bool   success;
        public string message;
        public string holland_code;
        public string career_result;
    }

    [Serializable]
    public class ChallengeRoundTelemetryPayload
    {
        public string challenge_id;
        public string primary_riasec;
        public bool solved;
        public int stars_earned;
        public int retry_count;
        public float time_spent_seconds;
        public int skill_use_r;
        public int skill_use_i;
        public int skill_use_a;
        public int skill_use_s;
        public int skill_use_e;
        public int skill_use_c;
    }

    [Serializable]
    public class RunSummaryTelemetryPayload
    {
        public string player_id;
        public string username;
        public string session_id;
        public string scene_version = "single_room_v1";
        public float total_time_spent_seconds;
        public List<ChallengeRoundTelemetryPayload> rounds = new();
    }

    [Serializable]
    public class RiasecScoresDto
    {
        public int r;
        public int i;
        public int a;
        public int s;
        public int e;
        public int c;
    }

    [Serializable]
    public class RunSummaryTelemetryOut
    {
        public bool success;
        public string message;
        public string source;
        public RiasecScoresDto riasec_scores;
        public string holland_code;
        public string career_family;
        public string career_result;
        public string model_version;
    }

    [Serializable]
    public class PredictionRequestPayload
    {
        public float[] features;
    }

    [Serializable]
    public class PredictionResponsePayload
    {
        public int predicted_cluster;
        public int career_cluster;
        public string career_result;
        public string cluster_label;
        public string career_family;
        public string cluster_holland_code;
        public string[] cluster_example_careers;
        public string source;
        public string model_version;
    }

    [Serializable]
    public class SessionClusterTelemetryPayload
    {
        public string player_id;
        public string session_id;
        public int predicted_cluster;
        public int career_cluster;
        public string career_result;
    }

    [Serializable]
    public class SessionClusterTelemetryOut
    {
        public bool success;
        public string message;
        public int predicted_cluster;
        public int career_cluster;
        public string career_result;
        public string holland_code;
        public string career_family;
        public string cluster_label;
        public string[] cluster_example_careers;
        public string source;
        public string model_version;
    }
}
