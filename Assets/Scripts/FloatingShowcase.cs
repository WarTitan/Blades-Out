using UnityEngine;

[AddComponentMenu("Cards/Floating Showcase")]
public class FloatingShowcase : MonoBehaviour
{
    [Header("Spin")]
    [Tooltip("Degrees per second. Positive = clockwise when viewed from above.")]
    public float spinSpeedY = 45f;

    [Tooltip("Yaw-only spin: prevents roll/pitch (no barrel rolls).")]
    public bool yawOnly = true;

    [Tooltip("Randomize the starting yaw so multiple items don't spin in sync.")]
    public bool randomizeSpinStart = true;

    [Tooltip("Adds a small random +/- jitter to spin speed (in percent). 0 = off, e.g. 0.1 = ±10%.")]
    [Range(0f, 0.5f)] public float spinSpeedJitter = 0.1f;

    [Header("Bob")]
    public float bobAmplitude = 0.05f;  // meters
    public float bobFrequency = 1.0f;   // Hz
    public float startHeight = 0.15f;   // base height above anchor
    public bool randomizePhase = true;  // bobbing phase (already desyncs up/down motion)

    // internals
    private Vector3 baseLocalPos;
    private float t0;
    private float yaw;                      // accumulated yaw in degrees
    private Quaternion initialLocalRot;     // starting pose to preserve tilt
    private float actualSpinSpeedY;         // spin after jitter

    void Start()
    {
        // base position (with lift)
        baseLocalPos = transform.localPosition + Vector3.up * startHeight;
        transform.localPosition = baseLocalPos;

        // bob phase
        t0 = randomizePhase ? Random.value * 1000f : 0f;

        // cache initial orientation (so yaw-only keeps tilt)
        initialLocalRot = transform.localRotation;

        // randomize start yaw + speed jitter
        yaw = randomizeSpinStart ? Random.Range(0f, 360f) : 0f;

        if (spinSpeedJitter > 0f)
        {
            float j = Random.Range(-spinSpeedJitter, spinSpeedJitter); // ±percent
            actualSpinSpeedY = spinSpeedY * (1f + j);
        }
        else
        {
            actualSpinSpeedY = spinSpeedY;
        }

        // apply initial yaw immediately
        if (yawOnly)
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * initialLocalRot;
        else
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * initialLocalRot; // still good as a start
    }

    void Update()
    {
        // --- Spin ---
        if (Mathf.Abs(actualSpinSpeedY) > 0.01f)
        {
            if (yawOnly)
            {
                yaw += actualSpinSpeedY * Time.deltaTime;
                transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * initialLocalRot;
            }
            else
            {
                transform.Rotate(0f, actualSpinSpeedY * Time.deltaTime, 0f, Space.Self);
            }
        }

        // --- Bob ---
        if (bobAmplitude > 0f && bobFrequency > 0f)
        {
            float y = bobAmplitude * Mathf.Sin((t0 + Time.time) * Mathf.PI * 2f * bobFrequency);
            transform.localPosition = baseLocalPos + Vector3.up * y;
        }
    }
}
