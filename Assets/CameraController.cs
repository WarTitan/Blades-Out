// Multi-display camera controller: whichever camera is clicked or activated becomes active.
// Works with the new Input System and shared mouse input across displays.

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
    private Camera thisCamera;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
        thisCamera = GetComponent<Camera>();
        if (thisCamera == null)
        {
            Debug.LogError($"{name} requires a Camera component!");
            enabled = false;
        }
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

        if (ActiveCamera == null && thisCamera.targetDisplay == 0)
            OnSwitchedTo(); // Default to Display 1 camera
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
        // Handle mouse click switching
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = thisCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Optional: check tag/layer for click switching
            }

            // Switch active camera if user clicks in this display's viewport
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Rect pixelRect = thisCamera.pixelRect;
            if (pixelRect.Contains(mousePos))
                OnSwitchedTo();
        }

        if (ActiveCamera != this)
            return;

        RotateCamera();
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

    public void OnSwitchedTo()
    {
        ActiveCamera = this;
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Optionally: ensure only one AudioListener active
        foreach (var cam in FindObjectsOfType<Camera>())
        {
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = (cam == thisCamera);
        }

        Debug.Log($"{name} (Display {thisCamera.targetDisplay}) is now active.");
    }

    private void OnMouseDown()
    {
        OnSwitchedTo();
    }
}
