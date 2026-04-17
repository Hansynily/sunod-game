using System;
using SunodGame.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class vc_QuestFlowGuide : MonoBehaviour
{
    [SerializeField] private vc_DirectionalArrow floatingArrow;
    [SerializeField] private vc_QuestRoom[] orderedQuestRooms = Array.Empty<vc_QuestRoom>();
    [SerializeField] private Transform[] orderedQuestTargets = Array.Empty<Transform>();
    [SerializeField] private Transform finalSceneTarget;
    [SerializeField] private bool autoResolveFromScene = true;

    private bool isBound = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsGuidedScene(scene.name))
        {
            return;
        }

        if (FindFirstObjectByType<vc_QuestFlowGuide>() != null)
        {
            return;
        }

        GameObject guideObject = new GameObject("QuestFlowGuide");
        guideObject.AddComponent<vc_QuestFlowGuide>();
    }

    private void Awake()
    {
        ResolveSceneReferences();
        BindQuestRooms();
        ShowInitialGuideTarget();
    }

    private void OnDestroy()
    {
        UnbindQuestRooms();
    }

    private void ResolveSceneReferences()
    {
        if (floatingArrow == null)
        {
            GameObject guideAnchor = FindSceneObjectLoose("GuideArrowAnchor");
            floatingArrow = guideAnchor != null
                ? guideAnchor.GetComponent<vc_DirectionalArrow>()
                : FindFirstObjectByType<vc_DirectionalArrow>();
        }

        if (orderedQuestRooms == null || orderedQuestRooms.Length == 0 || orderedQuestTargets == null || orderedQuestTargets.Length != orderedQuestRooms.Length)
        {
            if (autoResolveFromScene)
            {
                AutoResolveQuestRoute();
            }
        }

        if (finalSceneTarget == null && orderedQuestRooms != null && orderedQuestRooms.Length > 0)
        {
            vc_QuestRoom lastRoom = orderedQuestRooms[orderedQuestRooms.Length - 1];
            if (lastRoom != null && lastRoom.NextLevelButton != null)
            {
                finalSceneTarget = lastRoom.NextLevelButton.transform;
            }
        }
    }

    private void AutoResolveQuestRoute()
    {
        orderedQuestRooms = FindObjectsByType<vc_QuestRoom>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Array.Sort(orderedQuestRooms, (left, right) =>
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.CurrentQuestNumber.CompareTo(right.CurrentQuestNumber);
        });

        orderedQuestTargets = new Transform[orderedQuestRooms.Length];

        for (int i = 0; i < orderedQuestRooms.Length; i++)
        {
            vc_QuestRoom room = orderedQuestRooms[i];
            if (room == null)
            {
                continue;
            }

            orderedQuestTargets[i] = FindTargetForQuest(room.QuestId);
        }
    }

    private void BindQuestRooms()
    {
        if (isBound || orderedQuestRooms == null)
        {
            return;
        }

        for (int i = 0; i < orderedQuestRooms.Length; i++)
        {
            vc_QuestRoom room = orderedQuestRooms[i];
            if (room == null)
            {
                continue;
            }

            room.QuestStarted -= HandleQuestStarted;
            room.QuestStarted += HandleQuestStarted;
            room.QuestCompleted -= HandleQuestCompleted;
            room.QuestCompleted += HandleQuestCompleted;
        }

        isBound = true;
    }

    private void UnbindQuestRooms()
    {
        if (orderedQuestRooms == null)
        {
            return;
        }

        for (int i = 0; i < orderedQuestRooms.Length; i++)
        {
            vc_QuestRoom room = orderedQuestRooms[i];
            if (room == null)
            {
                continue;
            }

            room.QuestStarted -= HandleQuestStarted;
            room.QuestCompleted -= HandleQuestCompleted;
        }

        isBound = false;
    }

    private void ShowInitialGuideTarget()
    {
        if (floatingArrow == null)
        {
            return;
        }

        Transform firstTarget = GetQuestTarget(0);
        if (firstTarget != null)
        {
            floatingArrow.SetTarget(firstTarget);
            floatingArrow.ShowArrow();
            return;
        }

        floatingArrow.ClearTarget();
        floatingArrow.HideArrow();
    }

    private void HandleQuestStarted(vc_QuestRoom questRoom)
    {
        if (floatingArrow == null || questRoom == null)
        {
            return;
        }

        floatingArrow.HideArrow();
    }

    private void HandleQuestCompleted(vc_QuestRoom questRoom)
    {
        if (floatingArrow == null || questRoom == null)
        {
            return;
        }

        int roomIndex = GetQuestRoomIndex(questRoom);
        if (roomIndex < 0)
        {
            floatingArrow.ClearTarget();
            floatingArrow.HideArrow();
            return;
        }

        bool isLastQuest = questRoom.IsLastQuestInScene || roomIndex >= 0 && roomIndex == orderedQuestRooms.Length - 1;

        Transform nextTarget = null;
        if (!isLastQuest)
        {
            nextTarget = GetQuestTarget(roomIndex + 1);
        }

        if (nextTarget == null && isLastQuest)
        {
            nextTarget = finalSceneTarget;
        }

        if (nextTarget == null && isLastQuest && questRoom.NextLevelButton != null)
        {
            nextTarget = questRoom.NextLevelButton.transform;
        }

        if (nextTarget != null)
        {
            floatingArrow.SetTarget(nextTarget);
            floatingArrow.ShowArrow();
        }
        else
        {
            floatingArrow.ClearTarget();
            floatingArrow.HideArrow();
        }
    }

    private int GetQuestRoomIndex(vc_QuestRoom questRoom)
    {
        if (questRoom == null || orderedQuestRooms == null)
        {
            return -1;
        }

        for (int i = 0; i < orderedQuestRooms.Length; i++)
        {
            if (orderedQuestRooms[i] == questRoom)
            {
                return i;
            }
        }

        return -1;
    }

    private Transform GetQuestTarget(int index)
    {
        if (orderedQuestTargets == null || index < 0 || index >= orderedQuestTargets.Length)
        {
            return null;
        }

        return orderedQuestTargets[index];
    }

    private static bool IsGuidedScene(string sceneName)
    {
        return sceneName == SceneLoader.SCENE_PLAY
            || sceneName == "Level2_Scene"
            || sceneName == "Level3_Scene";
    }

    private Transform FindTargetForQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            return null;
        }

        string targetName = questId.Trim() switch
        {
            "L1_CatQuest" => "GuideTarget_L1_CatQuest",
            "L1_LostFriend" => "GuideTarget_L1_LostFriend",
            "L2_MissingKey" => "GuideTarget_L2_MissingKey",
            "L2_CatMedicine" => "GuideTarget_L2_CatMedicine",
            "L3_FallenSparrow" => "GuideTarget_L3_FallenSparrow",
            "L3_BlockedPath" => "GuideTarget_L3_BlockedPath",
            "L3_SlipperyWay" => "GuideTarget_L3_SlipperyWay",
            _ => null
        };

        return targetName != null ? FindSceneObjectLoose(targetName)?.transform : null;
    }

    private static GameObject FindSceneObjectLoose(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform match = FindChildRecursiveLoose(rootObjects[i].transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildRecursiveLoose(Transform parent, string objectName)
    {
        if (parent == null || objectName == null)
        {
            return null;
        }

        if (string.Equals(parent.name.Trim(), objectName.Trim(), StringComparison.Ordinal))
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform childMatch = FindChildRecursiveLoose(parent.GetChild(i), objectName);
            if (childMatch != null)
            {
                return childMatch;
            }
        }

        return null;
    }
}
