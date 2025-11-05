using UnityEngine;
namespace DistortionsPro_20X
{
    /// <summary>Simple first-person controller for Unity 2023.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float jumpForce = 5f;
        [SerializeField] float gravity = -9.81f;

        [Header("Mouse Look")]
        [SerializeField] float mouseSensitivity = 2f;
        [SerializeField] Transform cameraTransform;          // assign your main camera here

        float pitch;                                         // camera rotation around X
        CharacterController cc;
        Vector3 velocity;                                    // y-velocity for gravity

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (cameraTransform == null) cameraTransform = Camera.main.transform;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            HandleLook();
            HandleMovement();
        }

        void HandleLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // rotate player (yaw)
            transform.Rotate(Vector3.up * mouseX);

            // rotate camera (pitch)
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -80f, 80f);
            cameraTransform.localEulerAngles = Vector3.right * pitch;
        }

        void HandleMovement()
        {
            // WASD input
            float h = Input.GetAxis("Horizontal"); // A / D
            float v = Input.GetAxis("Vertical");   // W / S

            // local space → world space
            Vector3 move = transform.right * h + transform.forward * v;
            cc.Move(move * moveSpeed * Time.deltaTime);

            // jump
            if (cc.isGrounded)
            {
                if (velocity.y < 0) velocity.y = -2f;        // keep grounded
                if (Input.GetButtonDown("Jump")) velocity.y = jumpForce;
            }

            // gravity
            velocity.y += gravity * Time.deltaTime;
            cc.Move(velocity * Time.deltaTime);
        }
    }
}
