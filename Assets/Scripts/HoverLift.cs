using UnityEngine;

/// Scale-only hover animation: 1.0 -> scaleMultiplier over durationSeconds, and back.
/// Drives PinToAnchor.externalScale when present; otherwise falls back to transform.localScale.
/// Uses continuous easing in LateUpdate (no per-frame restart jitter).
[DefaultExecutionOrder(20000)]
[AddComponentMenu("Cards/Hover Scale (Simple)")]
public class HoverLift : MonoBehaviour
{
    [Header("Scale Tween")]
    public float scaleMultiplier = 1.20f;   // target scale on hover (relative)
    public float durationSeconds = 1.0f;    // time to go from 1.0 to 1.2 (and back)

    [Header("Integration")]
    public bool preferPinExternalScale = true; // if a PinToAnchor is present, drive its externalScale

    [Header("Debug")]
    public bool logWhenDrivingPin = false;

    [SerializeField] private bool hovered;

    // Pin path
    private PinToAnchor pin;
    private float baseFactor = 1f;      // externalScale baseline at Awake/OnEnable
    private float curFactor = 1f;       // current factor we are animating
    private float velFactor = 0f;       // SmoothDamp velocity

    // Fallback (no pin)
    private Vector3 baseScale;
    private Vector3 curScale;
    private Vector3 velScale;           // SmoothDamp velocity for Vector3

    void Awake()
    {
        pin = GetComponent<PinToAnchor>();

        if (preferPinExternalScale && pin != null && pin.allowExternalScale)
        {
            baseFactor = (pin.externalScale <= 0f ? 1f : pin.externalScale);
            curFactor = baseFactor;
            if (logWhenDrivingPin)
                Debug.Log("[HoverLift] Driving PinToAnchor.externalScale on " + gameObject.name);
        }
        else
        {
            baseScale = transform.localScale;
            curScale = baseScale;
        }
    }

    void OnEnable()
    {
        // Reset baselines on enable
        pin = GetComponent<PinToAnchor>();
        if (preferPinExternalScale && pin != null && pin.allowExternalScale)
        {
            baseFactor = (pin.externalScale <= 0f ? 1f : pin.externalScale);
            curFactor = baseFactor;
            velFactor = 0f;
        }
        else
        {
            baseScale = transform.localScale;
            curScale = baseScale;
            velScale = Vector3.zero;
        }
    }

    public void SetHovered(bool on)
    {
        hovered = on;
        // No immediate jump; LateUpdate will ease toward the new target.
    }

    void LateUpdate()
    {
        float smooth = Mathf.Max(0.0001f, durationSeconds);

        if (preferPinExternalScale && pin != null && pin.allowExternalScale)
        {
            float target = hovered ? baseFactor * scaleMultiplier : baseFactor;
            curFactor = Mathf.SmoothDamp(curFactor, target, ref velFactor, smooth);
            pin.externalScale = curFactor; // apply
            return;
        }

        // Fallback: no PinToAnchor found
        Vector3 targetScale = hovered ? baseScale * scaleMultiplier : baseScale;
        curScale = new Vector3(
            Mathf.SmoothDamp(curScale.x, targetScale.x, ref velScale.x, smooth),
            Mathf.SmoothDamp(curScale.y, targetScale.y, ref velScale.y, smooth),
            Mathf.SmoothDamp(curScale.z, targetScale.z, ref velScale.z, smooth)
        );
        transform.localScale = curScale;
    }
}
