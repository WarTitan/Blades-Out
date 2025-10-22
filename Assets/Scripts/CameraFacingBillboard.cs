using UnityEngine;
using Mirror;

/// Rotates this object to face the active viewer camera.
/// - In lobby: faces LobbyStage.lobbyCamera
/// - In game: faces the local player's gameplay camera (best for readability)
[AddComponentMenu("UI/Camera Facing Billboard")]
public class CameraFacingBillboard : MonoBehaviour
{
    [Header("What to rotate (defaults to this transform)")]
    public Transform target;

    [Header("Rotation")]
    public bool yawOnly = true;             // true = rotate around Y only
    public bool instant = true;             // true = snap instantly, false = turnSpeed
    public float turnSpeed = 720f;          // deg/sec if instant==false

    void Awake()
    {
        if (!target) target = transform;
    }

    void LateUpdate()
    {
        Camera cam = GetViewerCamera();
        if (cam == null) return;

        Vector3 toCam = cam.transform.position - target.position;
        if (yawOnly) toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) return;

        Quaternion look = Quaternion.LookRotation(yawOnly ? toCam.normalized : -(-toCam).normalized, Vector3.up);
        // ^ same as LookAt(cam) but protected for yawOnly

        if (instant)
            target.rotation = look;
        else
            target.rotation = Quaternion.RotateTowards(target.rotation, look, turnSpeed * Time.deltaTime);
    }

    Camera GetViewerCamera()
    {
        // 1) Lobby camera if lobby is active
        if (LobbyStage.Instance && LobbyStage.Instance.lobbyActive && LobbyStage.Instance.lobbyCamera)
            return LobbyStage.Instance.lobbyCamera;

        // 2) Local player's gameplay camera (each client faces their own view)
        if (NetworkClient.active && NetworkClient.localPlayer != null)
        {
            var lca = NetworkClient.localPlayer.GetComponent<LocalCameraActivator>();
            if (lca && lca.playerCamera && lca.playerCamera.enabled)
                return lca.playerCamera;
        }

        // 3) Fallback
        return Camera.main;
    }
}
