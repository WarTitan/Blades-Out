using UnityEngine;

/// Scale-only hover animation: 1.0 -> scaleMultiplier over durationSeconds, and back.
/// No movement, no tilt. If hover toggles mid-tween, it keeps easing smoothly.
[DefaultExecutionOrder(20000)]
[AddComponentMenu("Cards/Hover Scale (Simple)")]
public class HoverLift : MonoBehaviour
{
    [Header("Scale Tween")]
    public float scaleMultiplier = 1.20f;     // target scale on hover
    public float durationSeconds = 1.0f;      // time to reach target (hover and unhover)

    [Header("Runtime (read-only)")]
    [SerializeField] private bool hovered;

    private Vector3 baseScale;                // at-rest scale (tracked when idle)
    private Vector3 fromScale;                // tween start
    private Vector3 toScale;                  // tween target
    private float animStartTime;              // when current tween started
    private bool animating;

    public void SetHovered(bool on)
    {
        hovered = on;

        // Start a new tween from the current scale to the new target
        fromScale = transform.localScale;
        toScale = hovered ? baseScale * scaleMultiplier : baseScale;
        animStartTime = Time.time;
        animating = true;
    }

    void Awake()
    {
        baseScale = transform.localScale;
        fromScale = baseScale;
        toScale = baseScale;
        animStartTime = Time.time;
        animating = false;
    }

    void OnEnable()
    {
        // Reset to current as baseline to avoid jumps when enabling
        baseScale = transform.localScale;
        fromScale = baseScale;
        toScale = baseScale;
        animStartTime = Time.time;
        animating = false;
    }

    void LateUpdate()
    {
        // Keep baseScale in sync whenever we are idle at rest (not hovered and not animating)
        if (!hovered && !animating)
            baseScale = transform.localScale;

        if (!animating) return;

        float dur = Mathf.Max(0.0001f, durationSeconds);
        float t = Mathf.Clamp01((Time.time - animStartTime) / dur);

        // SmoothStep easing (no snap, nice ease in/out)
        float s = t * t * (3f - 2f * t);
        transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, s);

        if (t >= 1f)
        {
            animating = false;
            if (!hovered) baseScale = transform.localScale;
        }
    }
}
