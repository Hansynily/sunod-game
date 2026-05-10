using System.Collections;
using System.Collections.Generic;
using SunodGame.Core;
using UnityEngine;

[DisallowMultipleComponent]
public class vc_TestHarness : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;

    private void Awake()
    {
        // Lock all quest triggers immediately before any physics runs.
        // Prevents the player's default Game_Scene position from accidentally
        // firing a quest trigger before the teleport happens.
        foreach (var qr in FindObjectsByType<vc_QuestRoom>(FindObjectsSortMode.None))
        {
            Collider2D col = qr.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }
    }

    private IEnumerator Start()
    {
        // Wait for Game_Scene singletons to finish loading (async additive load)
        yield return new WaitUntil(() =>
            vc_SkillManager.Instance != null &&
            vc_QuestHUD.Instance != null &&
            vc_QuestTimer.Instance != null);

        // Teleport player to spawn point
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null && spawnPoint != null)
            player.transform.position = spawnPoint.position;

        Physics2D.SyncTransforms();

        // Now re-enable quest triggers — player is already in position.
        // OnTriggerEnter2D / Stay2D fires on the next physics step,
        // starting the quest naturally.
        foreach (var qr in FindObjectsByType<vc_QuestRoom>(FindObjectsSortMode.None))
        {
            Collider2D col = qr.GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }

        // Register any vc_SpawnPoints in the test scene with vc_SkillSpawner
        if (vc_SkillSpawner.Instance != null)
        {
            List<Transform> spawnPoints = new List<Transform>();
            foreach (var sp in FindObjectsByType<vc_SpawnPoint>(FindObjectsSortMode.None))
                spawnPoints.Add(sp.transform);
            if (spawnPoints.Count > 0)
                vc_SkillSpawner.Instance.SetSpawnPoints(spawnPoints);
        }

        Debug.Log("[TestHarness] Ready. Quest will start on next physics step.");
    }
}
