using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target & Look")]
    public Transform target;                      // center of the table
    public float lookSpeed = 100f;

    [Header("Robust alignment")]
    public int alignFrames = 4;                   // how many consecutive frames we force the alignment
    public float ignoreInputSecs = 0.25f;         // how long we ignore player input after switching
    public bool debugLogs = false;

    // input system
    private PlayerInputActions inputActions;
    private Vector2 lookInput;
    private float yaw;
    private float pitch;

    // runtime
    private float ignoreInputUntil = 0f;
    private Coroutine alignCoroutine;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Player.look.performed += OnLook;
        inputActions.Player.look.canceled += OnLook;
        inputActions.Enable();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Always try to align when enabled (robust multi-frame)
        StartAligning();
    }

    private void OnDisable()
    {
        inputActions.Player.look.performed -= OnLook;
        inputActions.Player.look.canceled -= OnLook;
        inputActions.Disable();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (alignCoroutine != null)
        {
            StopCoroutine(alignCoroutine);
            alignCoroutine = null;
        }
    }

    private void OnLook(InputAction.CallbackContext ctx)
    {
        // ignore look input while we're ignoring it
        if (Time.unscaledTime < ignoreInputUntil) return;
        lookInput = ctx.ReadValue<Vector2>();
    }

    private void Update()
    {
        // ignore rotating while being forced to align
        if (Time.unscaledTime < ignoreInputUntil) return;

        if (lookInput != Vector2.zero)
            RotateCamera();
    }

    private void RotateCamera()
    {
        yaw += lookInput.x * lookSpeed * Time.deltaTime;
        pitch -= lookInput.y * lookSpeed * Time.deltaTime;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // Public call used by the switcher
    public void SwitchDisplay()
    {
        // clear any leftover raw input
        lookInput = Vector2.zero;

        // ignore input for a short time to avoid immediate override
        ignoreInputUntil = Time.unscaledTime + ignoreInputSecs;

        // restart align coroutine
        StartAligning();

        if (debugLogs) Debug.Log($"{name}: SwitchDisplay() called, ignoring input until {ignoreInputUntil:F3}");
    }

    private void StartAligning()
    {
        if (alignCoroutine != null) StopCoroutine(alignCoroutine);
        alignCoroutine = StartCoroutine(AlignForFramesCoroutine(alignFrames));
    }

    // This coroutine forces the camera to look at the target for N frames.
    // That overrides other scripts/animators that might modify rotation after OnEnable.
    private IEnumerator AlignForFramesCoroutine(int frames)
    {
        if (target == null) yield break;

        // make sure we ignore input while aligning
        ignoreInputUntil = Mathf.Max(ignoreInputUntil, Time.unscaledTime + ignoreInputSecs);

        int i = 0;
        while (i < frames)
        {
            // Wait until end of frame so LateUpdate modifications are included in next iteration.
            yield return new WaitForEndOfFrame();

            Vector3 dir = (target.position - transform.position);
            if (dir.sqrMagnitude < 0.0001f) break;

            // Compute a world-space rotation looking at target with world up
            Quaternion worldLook = Quaternion.LookRotation(dir.normalized, Vector3.up);

            // Apply world rotation (this sets localRotation appropriately under any parent)
            transform.rotation = worldLook;

            // Clear any roll that might be left and sync yaw/pitch for later manual look
            Vector3 euler = transform.rotation.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            if (debugLogs)
            {
                Debug.Log($"{name}: Align frame {i+1}/{frames} -> rot={transform.rotation.eulerAngles} yaw={yaw:F2} pitch={pitch:F2}");
            }

            i++;
        }

        // small extra frame to ensure we stay stable
        yield return new WaitForEndOfFrame();

        alignCoroutine = null;
        if (debugLogs) Debug.Log($"{name}: Finished aligning for {frames} frames.");
    }
}
