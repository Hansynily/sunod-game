using System.Collections;
using UnityEngine;

/// <summary>Runs one coroutine for a static caller (vc_OfflineSession.Run), then removes itself.</summary>
public class vc_OfflineRunner : MonoBehaviour
{
    public void Begin(IEnumerator routine) => StartCoroutine(Wrap(routine));

    private IEnumerator Wrap(IEnumerator routine)
    {
        yield return routine;
        Destroy(gameObject);
    }
}
