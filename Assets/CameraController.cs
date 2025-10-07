// 10/7/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Display Settings")]
    public int targetDisplay = 1; // Set this in the Inspector for each camera (1 for Display 1, 2 for Display 2, etc.)

    [Header("Camera Look Settings")]
    public float lookSpeed = 100f; // Speed of camera rotation
    public Transform target; // The target object the camera should look at (e.g., CENTER Q)
    public bool alwaysLookAtTarget = false; // Toggle to control whether the camera always looks at the target
    public bool smoothLookOnSwitch = true; // Smoothly rotate toward target when switching displays

    private Vector2 lookInput; // Stores input for looking around
    private PlayerInputActions inputActions;
    private float yaw; // Tracks horizontal rotation
    private float pitch; // Tracks vertical rotation
    private bool justSwitchedDisplay = false; // Tracks if the display was just switched

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.look.performed += OnLook;
        inputActions.Player.look.canceled += OnLook;
        inputActions.Enable();

        // Hide and lock the cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        inputActions.Player.look.performed -= OnLook;
        inputActions.Player.look.canceled -= OnLook;
        inputActions.Disable();

        // Restore the cursor state
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    void Start()
    {
        // Ensure the camera is rendering to the correct display
        if (Display.displays.Length >= targetDisplay)
        {
            Display.displays[targetDisplay - 1].Activate();
            GetComponent<Camera>().targetDisplay = targetDisplay - 1;
        }

        // Initialize yaw and pitch based on the camera's current rotation
        Vector3 eulerAngles = transform.eulerAngles;
        yaw = eulerAngles.y;
        pitch = eulerAngles.x;

        // Ensure the camera points to the target initially
        if (target != null)
        {
            LookAtTarget();
        }
    }

    void Update()
    {
        // Rotate the camera based on input
        if (lookInput != Vector2.zero)
        {
            RotateCamera();
            justSwitchedDisplay = false;
        }
        else if (justSwitchedDisplay && target != null)
        {
            // If we just switched displays, ensure the camera looks at the target
            LookAtTarget();
            justSwitchedDisplay = false;
        }
        else if (alwaysLookAtTarget && target != null)
        {
            // Smoothly rotate to look at the target when the toggle is enabled
            Vector3 directionToTarget = target.position - transform.position;
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookSpeed);
        }
    }

    private void RotateCamera()
    {
        yaw += lookInput.x * lookSpeed * Time.deltaTime;
        pitch -= lookInput.y * lookSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -90f, 90f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void LookAtTarget()
    {
        if (target == null) return;

        Vector3 directionToTarget = target.position - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        transform.rotation = targetRotation;

        // Update yaw and pitch to match the new rotation
        Vector3 eulerAngles = transform.eulerAngles;
        yaw = eulerAngles.y;
        pitch = eulerAngles.x;
    }

    private IEnumerator SmoothLookAtTarget()
    {
        if (target == null) yield break;

        Quaternion startRot = transform.rotation;
        Quaternion endRot = Quaternion.LookRotation(target.position - transform.position);
        float duration = 0.5f; // how long the smooth turn takes
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        // Sync yaw and pitch with new orientation
        Vector3 eulerAngles = transform.eulerAngles;
        yaw = eulerAngles.y;
        pitch = eulerAngles.x;
    }

    public void SwitchDisplay()
    {
        justSwitchedDisplay = true;
        Debug.Log("SwitchDisplay called, camera reorienting to target.");

        if (target != null)
        {
            if (smoothLookOnSwitch)
                StartCoroutine(SmoothLookAtTarget());
            else
                LookAtTarget();
        }
    }
}
