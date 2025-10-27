// FILE: CameraSafetyNet.cs
using UnityEngine;
using Mirror;

[DefaultExecutionOrder(50000)]
[AddComponentMenu("Net/Camera Safety Net")]
public class CameraSafetyNet : MonoBehaviour
{
    [Header("Checks")]
    public float checkInterval = 0.15f;
    public bool enforceWhenNoCamera = true;
    public bool preferLocalPlayerCamera = true;

    float nextCheck;

    void Update()
    {
        if (!enforceWhenNoCamera) return;
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + checkInterval;

        // 1) If at least one enabled camera is rendering, optionally un-conflict lobby vs player
        Camera[] cams = GetAllCameras();
        bool anyEnabled = false;
        for (int i = 0; i < cams.Length; i++)
        {
            var c = cams[i];
            if (c != null && c.enabled && c.gameObject.activeInHierarchy)
            {
                anyEnabled = true;
                break;
            }
        }

        // If both lobby camera and a local player camera are enabled, prefer the local player camera
        if (anyEnabled && preferLocalPlayerCamera)
        {
            var localCam = FindLocalPlayerCamera();
            if (localCam != null && localCam.enabled)
            {
                // Turn the lobby camera off locally so it cannot fight
                if (LobbyStage.Instance != null && LobbyStage.Instance.lobbyCamera != null)
                    LobbyStage.Instance.lobbyCamera.enabled = false;
                return;
            }
        }

        // 2) If NO camera is rendering, enable the local player's camera, or fall back to lobby camera
        if (!anyEnabled)
        {
            // Prefer the local player's camera
            var localCam = FindLocalPlayerCamera();
            if (localCam != null)
            {
                SafeEnable(localCam);
                // Make sure lobby camera is off so it cannot flicker back on
                if (LobbyStage.Instance != null && LobbyStage.Instance.lobbyCamera != null)
                    LobbyStage.Instance.lobbyCamera.enabled = false;
                return;
            }

            // Fallback to lobby camera so we never black screen
            if (LobbyStage.Instance != null && LobbyStage.Instance.lobbyCamera != null)
                LobbyStage.Instance.lobbyCamera.enabled = true;
        }
    }

    Camera[] GetAllCameras()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        return Resources.FindObjectsOfTypeAll<Camera>();
#endif
    }

    Camera FindLocalPlayerCamera()
    {
        if (NetworkClient.localPlayer == null) return null;
        var root = NetworkClient.localPlayer.gameObject;
        if (root == null) return null;

        var cam = root.GetComponentInChildren<Camera>(true);
        return cam;
    }

    void SafeEnable(Camera cam)
    {
        if (cam == null) return;
        if (!cam.enabled) cam.enabled = true;

        var al = cam.GetComponent<AudioListener>();
        if (al != null && !al.enabled) al.enabled = true;

        // Disable other AudioListeners to avoid warnings
#if UNITY_2023_1_OR_NEWER
        var allAL = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var allAL = Resources.FindObjectsOfTypeAll<AudioListener>();
#endif
        for (int i = 0; i < allAL.Length; i++)
        {
            var other = allAL[i];
            if (other == null) continue;
            if (!other.gameObject.scene.IsValid()) continue; // ignore prefabs/assets
            if (other == al) continue;
            other.enabled = false;
        }
    }
}
