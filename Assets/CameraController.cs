using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Settings")]
    public float lookSpeed = 100f;

    private PlayerInputActions inputActions;
    private Vector2 lookInput;

    private float yaw;
    private float pitch;

    public static CameraController ActiveCamera { get; private set; }

    private Camera cam;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        cam = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.look.canceled += ctx => lookInput = Vector2.zero;

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Default to Camera1 at start
        if (ActiveCamera == null && name == "Camera1")
            OnSwitchedTo();
    }

    private void OnDisable()
    {
        inputActions.Disable();
        if (ActiveCamera == this)
            ActiveCamera = null;
    }

    private void Update()
    {
        HandleCameraSwitching();

        if (ActiveCamera != this)
            return;

        RotateCamera();
    }

    private void HandleCameraSwitching()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) ActivateCameraByName("Camera1", 0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame) ActivateCameraByName("Camera2", 1);
        if (Keyboard.current.digit3Key.wasPressedThisFrame) ActivateCameraByName("Camera3", 2);
        if (Keyboard.current.digit4Key.wasPressedThisFrame) ActivateCameraByName("Camera4", 3);
        if (Keyboard.current.digit5Key.wasPressedThisFrame) ActivateCameraByName("Camera5", 4);
        if (Keyboard.current.digit6Key.wasPressedThisFrame) ActivateCameraByName("Camera6", 5);
    }

    private void ActivateCameraByName(string cameraName, int displayIndex)
    {
        CameraController target = FindCameraByName(cameraName);
        if (target != null)
            target.OnSwitchedTo(displayIndex);
    }

    private CameraController FindCameraByName(string name)
    {
        var cameras = FindObjectsByType<CameraController>(FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            if (cam.name == name)
                return cam;
        }
        return null;
    }

    private void RotateCamera()
    {
        if (lookInput.sqrMagnitude < 0.0001f)
            return;

        yaw += lookInput.x * lookSpeed * Time.deltaTime;
        pitch -= lookInput.y * lookSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -90f, 90f);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void OnSwitchedTo(int displayIndex = 0)
    {
        ActiveCamera = this;
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        // Disable all other cameras
        var allCams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in allCams)
            c.enabled = false;

        // Enable this one
        cam.enabled = true;

        // Assign to correct display
        if (Display.displays.Length > displayIndex)
        {
            cam.targetDisplay = displayIndex;
            Display.displays[displayIndex].Activate();
            Debug.Log($"{name} activated on Display {displayIndex + 1}");
        }
        else
        {
            Debug.LogWarning($"Display {displayIndex + 1} not available!");
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
