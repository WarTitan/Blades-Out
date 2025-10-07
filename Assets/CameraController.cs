using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Settings")]
    public float lookSpeed = 100f;
    public float zoomSpeed = 2f;
    public float minZoom = 2f;
    public float maxZoom = 20f;

    [Header("References")]
    public Transform cameraTransform; // optional (for zoom only)

    private PlayerInputActions inputActions;
    private Vector2 lookInput;
    private float zoomInput;

    private float yaw;
    private float pitch;
    private float currentZoom = 10f;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();

        // input bindings
        inputActions.Player.look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.look.canceled += ctx => lookInput = Vector2.zero;

        inputActions.Player.zoom.performed += ctx => zoomInput = ctx.ReadValue<float>();
        inputActions.Player.zoom.canceled += ctx => zoomInput = 0f;

        // record scene rotation exactly as-is (no math conversions)
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        // hide + lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        inputActions.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        RotateCamera();
        HandleZoom();
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
        if (Mathf.Abs(zoomInput) < 0.01f)
            return;

        currentZoom -= zoomInput * zoomSpeed * Time.deltaTime;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

        if (cameraTransform != null)
            cameraTransform.localPosition = new Vector3(0, 0, -currentZoom);
        else
            transform.localPosition = new Vector3(0, 0, -currentZoom);
    }

    public void OnSwitchedTo()
    {
        // reset internal yaw/pitch to match current exact rotation
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        // ensure cursor stays locked when switching
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}

