using System;

namespace SunodGame.Core
{
    public enum RiasecCode
    {
        R,
        I,
        A,
        S,
        E,
        C
    }

    [Serializable]
    public class ChallengeRoundResult
    {
        public string challenge_id;
        public RiasecCode primary_riasec;
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
}
