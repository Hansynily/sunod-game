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