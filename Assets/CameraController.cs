// 10/9/2025 AI-Tag
// Each camera (Camera1–Camera6) can be selected with keys 1–6.
// Only the active camera responds to mouse input.
// Fixed zoom: reads scroll.y from Vector2 instead of float.

using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Settings")]
    public float lookSpeed = 100f;
    public float zoomSpeed = 5f;
    public float minZoom = 2f;
    public float maxZoom = 20f;
    public float zoomSmoothness = 10f;

    [Header("References")]
    public Transform cameraTransform; // Optional (for zoom only)

    private PlayerInputActions inputActions;
    private Vector2 lookInput;
    private float zoomInput;

    private float yaw;
    private float pitch;
    private float currentZoom = 10f;
    private float targetZoom = 10f;

    public static CameraController ActiveCamera { get; private set; }

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();

        inputActions.Player.look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.look.canceled += ctx => lookInput = Vector2.zero;

        // ✅ Fixed scroll: read Vector2 and use its Y component
        inputActions.Player.zoom.performed += ctx =>
        {
            Vector2 scroll = ctx.ReadValue<Vector2>();
            zoomInput = scroll.y;
        };
        inputActions.Player.zoom.canceled += ctx => zoomInput = 0f;

        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Make Camera1 active by default
        if (ActiveCamera == null && name == "Camera1")
            OnSwitchedTo();
    }

    private void OnDisable()
    {
        inputActions.Disable();
        if (ActiveCamera == this)
            ActiveCamera = null;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        HandleCameraSwitching();

        if (ActiveCamera != this)
            return;

        RotateCamera();
        HandleZoom();
    }

    private void HandleCameraSwitching()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame) ActivateCameraByName("Camera1");
        if (Keyboard.current.digit2Key.wasPressedThisFrame) ActivateCameraByName("Camera2");
        if (Keyboard.current.digit3Key.wasPressedThisFrame) ActivateCameraByName("Camera3");
        if (Keyboard.current.digit4Key.wasPressedThisFrame) ActivateCameraByName("Camera4");
        if (Keyboard.current.digit5Key.wasPressedThisFrame) ActivateCameraByName("Camera5");
        if (Keyboard.current.digit6Key.wasPressedThisFrame) ActivateCameraByName("Camera6");
    }

    private void ActivateCameraByName(string cameraName)
    {
        CameraController target = FindCameraByName(cameraName);
        if (target != null)
            target.OnSwitchedTo();
    }

    private CameraController FindCameraByName(string name)
    {
        foreach (var cam in FindObjectsOfType<CameraController>())
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

    private void HandleZoom()
    {
        if (Mathf.Abs(zoomInput) > 0.01f)
        {
            targetZoom -= zoomInput * zoomSpeed * Time.deltaTime;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        currentZoom = Mathf.Lerp(currentZoom, targetZoom, Time.deltaTime * zoomSmoothness);

        if (cameraTransform != null)
            cameraTransform.localPosition = new Vector3(0, 0, -currentZoom);
        else
            transform.localPosition = new Vector3(0, 0, -currentZoom);
    }

    public void OnSwitchedTo()
    {
        ActiveCamera = this;
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log($"{name} is now the active camera.");
    }

    private void OnMouseDown()
    {
        OnSwitchedTo();
    }

    public void ResetCamera()
    {
        yaw = 0f;
        pitch = 0f;
        currentZoom = (minZoom + maxZoom) / 2f;
        targetZoom = currentZoom;

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        if (cameraTransform != null)
            cameraTransform.localPosition = new Vector3(0, 0, -currentZoom);
        else
            transform.localPosition = new Vector3(0, 0, -currentZoom);

        Debug.Log($"{name} camera reset to default position and zoom.");
    }
}
