using UnityEngine;

namespace SunodGame.Demo
{
    public partial class DemoGameplayManager
    {
        private static readonly string[] SkillLetters = { "R", "I", "A", "S", "E", "C" };
        private static readonly string[] SkillActionNames = { "Build", "Track", "Mimic", "Bond", "Charm", "Plan" };
        private static readonly Color[] SkillColors =
        {
            new(0.95f, 0.35f, 0.35f, 1f),
            new(0.35f, 0.55f, 0.95f, 1f),
            new(0.95f, 0.70f, 0.25f, 1f),
            new(0.35f, 0.85f, 0.45f, 1f),
            new(0.95f, 0.55f, 0.20f, 1f),
            new(0.70f, 0.70f, 0.75f, 1f),
        };
    }
}
