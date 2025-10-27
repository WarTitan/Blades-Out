// FILE: LocalCameraController.cs
// First-person style look. Locks the cursor when enabled for the local player.
// Seeds yaw/pitch from current transforms so it never snaps back to an axis.

using UnityEngine;
using Mirror;

[AddComponentMenu("Net/Local Camera Controller")]
public class LocalCameraController : NetworkBehaviour
{
    [Header("Rig References")]
    public Transform playerBody; // yaw pivot
    public Camera playerCamera;  // pitch pivot (local)

    [Header("Look Settings")]
    public float sensitivityX = 140f;
    public float sensitivityY = 140f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public bool invertY = false;

    [Header("Debug")]
    public bool verboseLogs = false;

    private float yaw;
    private float pitch;

    void Awake()
    {
        if (playerBody == null) playerBody = transform;
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        SeedFromCurrentPose();
        ForceLockCursor(true);
    }

    void OnEnable()
    {
        if (isLocalPlayer)
        {
            SeedFromCurrentPose();
            ForceLockCursor(true);
        }
    }

    void OnDisable()
    {
        if (isLocalPlayer)
        {
            // Intentionally do not unlock here to avoid flicker if toggled rapidly.
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        float mx = Input.GetAxisRaw("Mouse X");
        float my = Input.GetAxisRaw("Mouse Y");

        yaw += mx * sensitivityX * Time.unscaledDeltaTime;
        float dy = (invertY ? my : -my) * sensitivityY * Time.unscaledDeltaTime;
        pitch = Mathf.Clamp(pitch + dy, minPitch, maxPitch);

        ApplyPose();
    }

    // PUBLIC so other scripts (TeleportHelper) can call it.
    public void SeedFromCurrentPose()
    {
        if (playerBody != null)
        {
            yaw = playerBody.eulerAngles.y;
        }

        if (playerCamera != null)
        {
            Vector3 e = playerCamera.transform.localEulerAngles;
            float px = e.x;
            if (px > 180f) px -= 360f; // map to [-180, 180]
            pitch = Mathf.Clamp(px, minPitch, maxPitch);
        }

        ApplyPose();

        if (verboseLogs)
        {
            Debug.Log("[LocalCameraController] Seed yaw=" + yaw + " pitch=" + pitch);
        }
    }

    private void ApplyPose()
    {
        if (playerBody != null)
        {
            playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private static void ForceLockCursor(bool locked)
    {
        if (locked)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
    }
}
