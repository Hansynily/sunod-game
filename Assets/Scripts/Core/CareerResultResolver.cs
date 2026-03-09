using System.Collections.Generic;

namespace SunodGame.Core
{
    // THIS IS ALL HARDCODED FROM A SAMPLE DATA. PLS MODIFY ONCE MODEL IS DEPLOYED.
    public static class CareerResultResolver
    {
        private static readonly Dictionary<string, string> CareerByCode = new()
        {
            { "RIA", "Architect / Industrial Designer" },
            { "RIC", "Engineer / Technician" },
            { "IAS", "Researcher / Scientist" },
            { "IAE", "Creative Director" },
            { "ASE", "Marketing / PR Specialist" },
            { "SEC", "Educator / Counselor" },
            { "ECS", "Business Manager" },
            { "REC", "Project Manager" },
            { "ESA", "Entrepreneur" },
            { "CSI", "Data Analyst" },
        };

        private static readonly Dictionary<char, string> FallbackByTopLetter = new()
        {
            { 'R', "Trades / Engineering" },
            { 'I', "Science / Research" },
            { 'A', "Arts / Design" },
            { 'S', "Social Work / Teaching" },
            { 'E', "Business / Leadership" },
            { 'C', "Administration / Finance" },
        };

        public static void ResolveAndStore(int[] skillUseCount, int[] firstUseOrder)
        {
            if (skillUseCount == null || skillUseCount.Length < 6)
                return;

            int[] order = { 0, 1, 2, 3, 4, 5 };

            for (int i = 0; i < order.Length - 1; i++)
            {
                for (int j = i + 1; j < order.Length; j++)
                {
                    int a = order[i];
                    int b = order[j];

                    int countCompare = skillUseCount[b].CompareTo(skillUseCount[a]);
                    if (countCompare > 0)
                    {
                        order[i] = b;
                        order[j] = a;
                        continue;
                    }

                    if (countCompare < 0)
                        continue;

                    int firstA = (firstUseOrder != null && firstUseOrder.Length > a) ? firstUseOrder[a] : int.MaxValue;
                    int firstB = (firstUseOrder != null && firstUseOrder.Length > b) ? firstUseOrder[b] : int.MaxValue;

                    if (firstB < firstA)
                    {
                        order[i] = b;
                        order[j] = a;
                    }
                }
            }

            string code = $"{GameSessionData.skillNames[order[0]]}{GameSessionData.skillNames[order[1]]}{GameSessionData.skillNames[order[2]]}";
            GameSessionData.hollandCode = code;

            if (CareerByCode.TryGetValue(code, out string career))
            {
                GameSessionData.careerResult = career;
                return;
            }

            char top = GameSessionData.skillNames[order[0]][0];
            GameSessionData.careerResult = FallbackByTopLetter.TryGetValue(top, out string fallback)
                ? fallback
                : "Career path not found";
        }
    }
}
