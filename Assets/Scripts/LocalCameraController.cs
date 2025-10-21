using UnityEngine;
using Mirror;

public class LocalCameraController : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("Yaw pivot. If null, will use transform.parent or this transform.")]
    public Transform playerBody;

    [Header("Look (no smoothing)")]
    [Tooltip("Mouse delta multiplier. Try 2.0–6.0 depending on your DPI.")]
    public float sensitivity = 3.0f;
    public bool invertY = false;
    public float pitchMin = -85f;
    public float pitchMax = 85f;

    // runtime
    private float yaw;
    private float pitch;

    void Awake()
    {
        if (playerBody == null)
            playerBody = transform.parent != null ? transform.parent : transform;
    }

    public override void OnStartLocalPlayer()
    {
        // initialize from current transforms
        if (playerBody) yaw = playerBody.localEulerAngles.y;

        pitch = transform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f; // unwrap to [-180, 180]
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        // Only rotate when the cursor is locked (gameplay)
        if (Cursor.lockState != CursorLockMode.Locked) return;

        float mx = Input.GetAxisRaw("Mouse X"); // raw mouse delta (no smoothing)
        float my = Input.GetAxisRaw("Mouse Y");

        yaw += mx * sensitivity;
        pitch += (invertY ? my : -my) * sensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        if (playerBody) playerBody.localRotation = Quaternion.Euler(0f, yaw, 0f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
