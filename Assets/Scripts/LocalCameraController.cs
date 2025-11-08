// FILE: LocalCameraController.cs
// ORIGINAL + ForceLookAt helper (ASCII only)

using UnityEngine;

[AddComponentMenu("Player/Local Camera Controller")]
[DefaultExecutionOrder(40000)] // run BEFORE mixers (FOV/Roll effects)
public class LocalCameraController : MonoBehaviour
{
    [Header("Refs")]
    public Transform playerBody;          // yaw target
    public Camera playerCamera;           // pitch target; auto-found if null

    [Header("Look")]
    public float sensitivityX = 1.5f;
    public float sensitivityY = 1.5f;
    public bool invertY = false;
    public float minPitch = -85f;
    public float maxPitch = 85f;

    [Header("Behavior")]
    public bool requireCursorLocked = true;
    public bool requireCameraEnabled = true;
    public bool onlyWhenFocused = true;

    [Header("Smoothing")]
    public bool smooth = true;
    public float smoothTime = 0.02f;

    [Header("Tilted Screen Controls")]
    public bool rotateInputWithRoll = true;   // rotate mouse deltas by camera Z roll
    [Range(0f, 1f)] public float rollInputFactor = 1.0f; // 1 = full match, 0.5 = half, 0 = off

    // internal state
    private float yaw;
    private float pitch;
    private float velYaw;
    private float velPitch;
    private float inputX;
    private float inputY;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerBody != null)
            yaw = Normalize180(playerBody.localEulerAngles.y);

        if (playerCamera != null)
        {
            pitch = Normalize180(playerCamera.transform.localEulerAngles.x);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }
    }

    void OnEnable()
    {
        velYaw = 0f;
        velPitch = 0f;
    }

    void Update()
    {
        if (onlyWhenFocused && !Application.isFocused) return;
        if (requireCursorLocked && Cursor.lockState != CursorLockMode.Locked) return;
        if (playerCamera == null || (requireCameraEnabled && !playerCamera.enabled)) return;

        // Raw deltas from desktop axes
        float dx = Input.GetAxisRaw("Mouse X");
        float dy = Input.GetAxisRaw("Mouse Y");

        // Optionally rotate input to follow the current screen tilt (camera Z roll)
        if (rotateInputWithRoll && playerCamera != null && rollInputFactor > 0f)
        {
            float z = Normalize180(playerCamera.transform.localEulerAngles.z);
            // Rotate input by -roll so controls align with the tilted screen axes.
            float r = -z * Mathf.Deg2Rad * rollInputFactor;
            float cs = Mathf.Cos(r);
            float sn = Mathf.Sin(r);
            float rx = dx * cs - dy * sn;
            float ry = dx * sn + dy * cs;
            dx = rx;
            dy = ry;
        }

        // Scale and apply Y inversion (negative makes up move look up by default)
        inputX += dx * sensitivityX;
        inputY += dy * (invertY ? 1f : -1f) * sensitivityY;
    }

    void LateUpdate()
    {
        if (onlyWhenFocused && !Application.isFocused) { inputX = inputY = 0f; return; }
        if (playerCamera == null || (requireCameraEnabled && !playerCamera.enabled)) { inputX = inputY = 0f; return; }
        if (requireCursorLocked && Cursor.lockState != CursorLockMode.Locked) { inputX = inputY = 0f; return; }

        float targetYaw = yaw + inputX;
        float targetPitch = Mathf.Clamp(pitch + inputY, minPitch, maxPitch);

        inputX = 0f;
        inputY = 0f;

        if (smooth)
        {
            float dt = Mathf.Max(0.0001f, Time.deltaTime);
            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref velYaw, smoothTime, Mathf.Infinity, dt);
            pitch = Mathf.SmoothDampAngle(pitch, targetPitch, ref velPitch, smoothTime, Mathf.Infinity, dt);
        }
        else
        {
            yaw = targetYaw;
            pitch = targetPitch;
        }

        // Apply yaw to body (preserve its X/Z)
        if (playerBody != null)
        {
            Vector3 e = playerBody.localEulerAngles;
            float ex = Normalize180(e.x);
            float ez = Normalize180(e.z);
            playerBody.localRotation = Quaternion.Euler(ex, yaw, ez);
        }

        // Apply pitch to camera, PRESERVING Z roll from mixers/effects
        if (playerCamera != null)
        {
            Transform ct = playerCamera.transform;
            float currentZ = Normalize180(ct.localEulerAngles.z);
            Quaternion pitchRot = Quaternion.Euler(pitch, 0f, 0f);
            Quaternion keepRoll = Quaternion.AngleAxis(currentZ, Vector3.forward);
            ct.localRotation = pitchRot * keepRoll;
        }
    }

    /// <summary>
    /// Instantly make the player look at a world-space point.
    /// Used by the memory minigame after the 3-2-1-GO intro.
    /// This also updates internal yaw/pitch so there is no snap back.
    /// </summary>
    public void ForceLookAt(Vector3 worldTarget)
    {
        if (playerCamera == null && playerBody == null) return;

        Vector3 eyePos;
        if (playerCamera != null)
            eyePos = playerCamera.transform.position;
        else
            eyePos = playerBody.position;

        Vector3 dir = worldTarget - eyePos;
        if (dir.sqrMagnitude < 0.0001f) return;

        dir.Normalize();

        // Yaw from X/Z plane
        float newYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        // Pitch from vertical vs horizontal magnitude
        float flatLen = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
        float newPitch = 0f;
        if (flatLen > 0.0001f)
            newPitch = Mathf.Atan2(dir.y, flatLen) * Mathf.Rad2Deg;

        newPitch = Mathf.Clamp(newPitch, minPitch, maxPitch);

        // Update internal state so controller is in sync
        yaw = newYaw;
        pitch = newPitch;
        velYaw = 0f;
        velPitch = 0f;
        inputX = 0f;
        inputY = 0f;

        // Apply immediately (same logic as LateUpdate)
        if (playerBody != null)
        {
            Vector3 e = playerBody.localEulerAngles;
            float ex = Normalize180(e.x);
            float ez = Normalize180(e.z);
            playerBody.localRotation = Quaternion.Euler(ex, yaw, ez);
        }

        if (playerCamera != null)
        {
            Transform ct = playerCamera.transform;
            float currentZ = Normalize180(ct.localEulerAngles.z);
            Quaternion pitchRot = Quaternion.Euler(pitch, 0f, 0f);
            Quaternion keepRoll = Quaternion.AngleAxis(currentZ, Vector3.forward);
            ct.localRotation = pitchRot * keepRoll;
        }
    }

    private static float Normalize180(float degrees)
    {
        if (degrees > 180f) degrees -= 360f;
        return degrees;
    }
}
