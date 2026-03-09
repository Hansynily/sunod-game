using System;

namespace SunodGame.Core
{
    public static class GameSessionData
    {
        public static int[] skillUseCount = new int[6];
        public static string[] skillNames = { "R", "I", "A", "S", "E", "C" };
        public static string hollandCode = "";
        public static string careerResult = "";
        public static string backendMessage = "";
        public static bool usedBackendResult = false;

        // Tie-break helper: lower value means earlier first-use.
        public static int[] firstUseOrder = new int[6];

        static GameSessionData()
        {
            Reset();
        }

        public static void Reset()
        {
            skillUseCount = new int[6];
            firstUseOrder = new int[6];
            for (int i = 0; i < firstUseOrder.Length; i++)
                firstUseOrder[i] = int.MaxValue;

            hollandCode = string.Empty;
            careerResult = string.Empty;
            backendMessage = string.Empty;
            usedBackendResult = false;
        }
    }
}
