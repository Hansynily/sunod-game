using UnityEngine;

public class NPCController : MonoBehaviour
{
    public bool isCharmed { get; set; }
    public float maxDistance = 5.0f;
    public GameObject player;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isCharmed = false;
        player = GameObject.FindGameObjectWithTag("Player");
    }

    // Update is called once per frame
    void Update()
    {
        if (isCharmed)
        {
            gameObject.transform.position = Vector2.MoveTowards(gameObject.transform.position, player.transform.position, 2.0f * Time.deltaTime);
        }
        else
        {
            if (Vector2.Distance(gameObject.transform.position, player.transform.position) < maxDistance)
            {
                gameObject.transform.position += (gameObject.transform.position - player.transform.position).normalized * 2.0f * Time.deltaTime;
            }
        }
    }
}
