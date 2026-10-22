using UnityEngine;

/// <summary>
/// A small shed with a cot where the player sleeps for the night. Each town has a few,
/// spread around, so the player never has to walk across town to end the day: Sleep walks
/// them to the nearest shed, and the next morning they wake up in front of that same shed.
/// (The player's home is the house from the dream - it's being rebuilt, so nobody sleeps there.)
/// </summary>
[DisallowMultipleComponent]
public class vc_TownShed : MonoBehaviour
{
    public SpriteRenderer body;
    [Tooltip("Small floating \"Zz\" sign over the door (optional).")]
    public SpriteRenderer sign;
    public float signBob = 0.06f;
    [Tooltip("Open-door overlay (hidden while closed; the closed door is part of the shed art).")]
    public SpriteRenderer doorOpen;

    private Vector3 _signBase;

    public bool IsOpen => doorOpen != null && doorOpen.enabled;

    /// <summary>Swing the door open (bedtime / morning). Only ever driven by the sleep cutscene.</summary>
    public void OpenDoor()
    {
        if (doorOpen == null || doorOpen.enabled) return;
        doorOpen.enabled = true;
        SunodGame.Core.vc_Sfx.Play("clack", 0.45f, 0.06f);
    }

    public void CloseDoor()
    {
        if (doorOpen == null || !doorOpen.enabled) return;
        doorOpen.enabled = false;
        SunodGame.Core.vc_Sfx.Play("clack", 0.35f, 0.1f);
    }

    /// <summary>Front door: bottom-middle of the shed sprite.</summary>
    public Vector3 DoorPosition
    {
        get
        {
            if (body != null && body.sprite != null)
            {
                Bounds b = body.bounds;
                return new Vector3(b.center.x, b.min.y + 0.25f, transform.position.z);
            }
            return transform.position;
        }
    }

    /// <summary>Where the player stands in the morning: just in front of the door.</summary>
    public Vector3 WakePosition => DoorPosition + new Vector3(0f, -0.9f, 0f);

    private void Awake()
    {
        if (sign != null) _signBase = sign.transform.localPosition;
        if (doorOpen != null) doorOpen.enabled = false;
    }

    private void Update()
    {
        if (sign == null) return;
        sign.transform.localPosition = _signBase + new Vector3(0f, Mathf.Round(Mathf.Sin(Time.time * 2f) * 2f) / 2f * signBob, 0f);
    }
}
