using System;
using System.Collections.Generic;

namespace SunodGame.Models
{
    //  Simple register. Backend generates player_id server-side.
    //  Used only when you want to pre-register without a run.


    [Serializable]
    public class UserCreateRequest
    {
        public string username;
        public string password;
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
        public string created_at;
        public string last_login;
    }

    [Serializable]
    public class ErrorResponse
    {
        public string detail;
    }

    //  MAIN TELEMETRY ENDPOINT
    //  POST /api/telemetry/quest-attempt
    //
    //  What it sends:
    //    - creates user if new (by player_id)
    //    - creates quest attempt
    //    - inserts skills used
    //    - updates RIASEC profile
    //  THESE AREN'T FINAL. ONLY TO SHOWCASE THE TELEMETRY!!

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
}
