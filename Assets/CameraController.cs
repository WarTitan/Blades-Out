using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float lookSpeed = 100f;

    private PlayerInputActions inputActions;
    private Vector2 lookInput;
    private float yaw;
    private float pitch;

    private void Awake()
    {
        inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.look.canceled += ctx => lookInput = Vector2.zero;
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }

    private void Update()
    {
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
}
