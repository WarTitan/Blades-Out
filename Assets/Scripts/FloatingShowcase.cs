using UnityEngine;

[AddComponentMenu("Cards/Floating Showcase")]
public class FloatingShowcase : MonoBehaviour
{
    public enum SpinAxisMode { WorldUp, ParentUp, ModelUp, CustomWorld }
    public enum SpinDirection { Clockwise, CounterClockwise }

    [Header("Spin")]
    [Tooltip("Degrees per second (magnitude only).")]
    public float spinSpeedY = 45f;

    [Tooltip("Pick which way to spin when viewed from above.")]
    public SpinDirection spinDirection = SpinDirection.CounterClockwise;

    [Tooltip("Which axis to spin around.")]
    public SpinAxisMode spinAxisMode = SpinAxisMode.WorldUp;

    [Tooltip("Used when SpinAxisMode = CustomWorld (world-space axis).")]
    public Vector3 customWorldAxis = Vector3.up;

    [Tooltip("Randomize the starting yaw so multiple items don't spin in sync.")]
    public bool randomizeSpinStart = true;

    [Range(0f, 0.5f)]
    [Tooltip("Adds ±% jitter to spin speed so items slowly drift out of sync.")]
    public float spinSpeedJitter = 0.1f;

    [Header("Bob")]
    public float bobAmplitude = 0.05f;  // meters
    public float bobFrequency = 1.0f;   // Hz
    public float startHeight = 0.15f;   // base height above anchor
    public bool randomizePhase = true;

    // internals
    private Vector3 baseLocalPos;
    private float t0;
    private float yawDeg;                 // accumulated yaw (degrees)
    private Quaternion initialWorldRot;   // remember starting world rotation
    private float actualSpinSpeedY;       // signed degrees/sec after direction + jitter

    void Start()
    {
        // base position (with lift)
        baseLocalPos = transform.localPosition + Vector3.up * startHeight;
        transform.localPosition = baseLocalPos;

        // bob phase
        t0 = randomizePhase ? Random.value * 1000f : 0f;

        // cache starting world rotation so we can spin around a stable world/parent axis
        initialWorldRot = transform.rotation;

        // random start angle
        yawDeg = randomizeSpinStart ? Random.Range(0f, 360f) : 0f;

        // direction + jitter
        float sign = (spinDirection == SpinDirection.Clockwise) ? 1f : -1f;
        actualSpinSpeedY = spinSpeedY * sign;
        if (spinSpeedJitter > 0f)
        {
            float j = Random.Range(-spinSpeedJitter, spinSpeedJitter); // ±percent
            actualSpinSpeedY *= (1f + j);
        }

        // apply initial yaw immediately
        ApplySpin(0f); // uses yawDeg as set above
    }

    void Update()
    {
        // --- Spin around chosen WORLD-SPACE axis (never cartwheel) ---
        if (Mathf.Abs(actualSpinSpeedY) > 0.01f)
        {
            yawDeg += actualSpinSpeedY * Time.deltaTime;
            ApplySpin(0f);
        }

        // --- Bob ---
        if (bobAmplitude > 0f && bobFrequency > 0f)
        {
            float y = bobAmplitude * Mathf.Sin((t0 + Time.time) * Mathf.PI * 2f * bobFrequency);
            transform.localPosition = baseLocalPos + Vector3.up * y;
        }
    }

    private void ApplySpin(float extraYaw)
    {
        Vector3 axisWorld = GetAxisWorld();
        if (axisWorld.sqrMagnitude < 1e-6f) axisWorld = Vector3.up;

        // Spin around a fixed world-space axis, then apply the original world rotation.
        // This guarantees a pure yaw around that axis — no roll/pitch/cartwheel.
        Quaternion spin = Quaternion.AngleAxis(yawDeg + extraYaw, axisWorld.normalized);
        transform.rotation = spin * initialWorldRot;
    }

    private Vector3 GetAxisWorld()
    {
        switch (spinAxisMode)
        {
            case SpinAxisMode.WorldUp: return Vector3.up;
            case SpinAxisMode.ParentUp: return transform.parent ? transform.parent.up : Vector3.up;
            case SpinAxisMode.ModelUp: return transform.up; // current model up in world-space
            case SpinAxisMode.CustomWorld:
                return customWorldAxis;
            default: return Vector3.up;
        }
    }
}
