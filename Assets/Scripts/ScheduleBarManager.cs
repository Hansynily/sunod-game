using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScheduleBarManager : MonoBehaviour
{
    public static ScheduleBarManager Instance;

    public int maxCapacity = 100;
    public int currentCapacity;

    public RectTransform fillTransform;      
    public GameObject questBoxPrefab;
    public RectTransform questBoxContainer;

    private float originalWidth;
    private LayoutElement fillLayout;

    private void Awake()
    {
        if (Instance == null) 
            Instance = this;
        else 
            Destroy(gameObject);
    }

    void Start()
    {
        currentCapacity = maxCapacity;

        fillLayout = fillTransform.GetComponent<LayoutElement>();

        originalWidth = questBoxContainer.rect.width;

        UpdateFill();
    }

    public void AddQuest(QuestData quest)
    {
        currentCapacity -= quest.cost;
        if (currentCapacity < 0)
            currentCapacity = 0;

        UpdateFill();

        CreateQuestBox(quest);
    }

    void UpdateFill()
    {
        float newWidth = originalWidth * ((float)currentCapacity / maxCapacity);
        fillLayout.preferredWidth = newWidth;
    }

    void CreateQuestBox(QuestData quest)
    {
        GameObject box = Instantiate(questBoxPrefab, questBoxContainer);

        Image img = box.GetComponent<Image>();
        TMP_Text text = box.GetComponentInChildren<TMP_Text>();

        text.text = quest.questTitle;
        img.color = GetCategoryColor(quest.category);

        LayoutElement layout = box.GetComponent<LayoutElement>();
        layout.preferredWidth = originalWidth * ((float)quest.cost / maxCapacity);
    }

    Color GetCategoryColor(QuestCategory category)
    {
        return category switch
        {
            QuestCategory.Currency => Color.yellow,
            QuestCategory.Skill => Color.red,
            QuestCategory.Rest => Color.blue,
            _ => Color.white
        };
    }
}