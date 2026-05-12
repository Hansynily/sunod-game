using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable NPC component that performs a timed action.
/// Call StartAction() to begin; subscribe to OnActionComplete to react when it finishes.
/// </summary>
[DisallowMultipleComponent]
public class vc_NPC_DoAction : MonoBehaviour
{
    [SerializeField] private float actionDuration = 7f;

    /// <summary>Fired once when the timed action completes.</summary>
    public event Action OnActionComplete;

    /// <summary>Overrides the action duration before calling StartAction.</summary>
    public void SetDuration(float seconds)
    {
        actionDuration = seconds;
    }

    /// <summary>Begins the timed action coroutine.</summary>
    public void StartAction()
    {
        StopAllCoroutines();
        StartCoroutine(ActionRoutine());
    }

    private IEnumerator ActionRoutine()
    {
        yield return new WaitForSeconds(actionDuration);
        OnActionComplete?.Invoke();
    }
}
