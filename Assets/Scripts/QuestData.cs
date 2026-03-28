// LEGACY-PARKED: Not good for demo. Only use when on non-demo phase.
public enum QuestCategory
{
    Currency,
    Skill,
    Rest
}

[System.Serializable]
public class QuestData
{
    public string questTitle;
    public int cost;
    public QuestCategory category;
}
