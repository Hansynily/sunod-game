using UnityEngine;

[DisallowMultipleComponent]
public class vc_FloatingMarker : MonoBehaviour
{
    public enum MarkerType { Main, POI }

    [SerializeField] private MarkerType markerType;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 2f;

    private Vector3 _baseLocalPos;
    private float _phaseOffset;

    private void Awake()
    {
        _baseLocalPos = transform.localPosition;
        _phaseOffset = Random.value * Mathf.PI * 2f;
    }

    private void Update()
    {
        float y = Mathf.Sin(Time.time * bobSpeed + _phaseOffset) * bobAmplitude;
        transform.localPosition = _baseLocalPos + new Vector3(0f, y, 0f);
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);

    public MarkerType Type => markerType;
}
