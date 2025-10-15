using UnityEngine;

/// Pins a transform to an anchor at a fixed local position/rotation/scale.
/// Runs very late so it overrides earlier scripts that try to move it.
[DefaultExecutionOrder(10000)]
[AddComponentMenu("Cards/Pin To Anchor")]
public class PinToAnchor : MonoBehaviour
{
    public Transform anchor;
    public Vector3 localPosition;
    public Quaternion localRotation = Quaternion.identity;
    public Vector3 localScale = Vector3.one;

    void LateUpdate()
    {
        if (!anchor) return;
        var t = transform;
        // Ensure we stay parented
        if (t.parent != anchor) t.SetParent(anchor, false);
        // Force the exact local TRS
        t.localPosition = localPosition;
        t.localRotation = localRotation;
        t.localScale = localScale;
    }
}
