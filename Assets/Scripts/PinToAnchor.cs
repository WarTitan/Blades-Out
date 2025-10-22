using UnityEngine;

/// Pins a transform to an anchor at a fixed local position/rotation/scale.
/// Runs very late so it overrides earlier scripts that try to move it.
[DefaultExecutionOrder(10000)]
[AddComponentMenu("Cards/Pin To Anchor")]
public class PinToAnchor : MonoBehaviour
{
    [Header("Anchor")]
    public Transform anchor;
    public Vector3 localPosition;
    public Quaternion localRotation = Quaternion.identity;
    public Vector3 localScale = Vector3.one;

    [Header("External Scale (multiplies localScale)")]
    public bool allowExternalScale = true;
    [Min(0f)] public float externalScale = 1f;

    void LateUpdate()
    {
        if (!anchor) return;

        var t = transform;

        // Ensure we stay parented
        if (t.parent != anchor) t.SetParent(anchor, false);

        // Force the exact local TRS (with optional external scale multiplier)
        t.localPosition = localPosition;
        t.localRotation = localRotation;

        if (allowExternalScale)
            t.localScale = localScale * Mathf.Max(0f, externalScale);
        else
            t.localScale = localScale;
    }

    /// Optional helper for other scripts (e.g., hover tweener)
    public void SetExternalScale(float s)
    {
        externalScale = Mathf.Max(0f, s);
    }
}
