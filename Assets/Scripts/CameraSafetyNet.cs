// FILE: CameraSafetyNet.cs
// PURPOSE: Last-resort guard. If Display 1 has no rendering camera in a frame,
// it will pick the local player's camera, activate its entire parent chain,
// normalize settings, enable it, and disable other cameras and stray AudioListeners.
// Put this on any always-present scene object. Execution order is very late.

using UnityEngine;
using Mirror;

[DefaultExecutionOrder(70000)]
public class CameraSafetyNet : MonoBehaviour
{
    public bool verboseLogs = false;

    void LateUpdate()
    {
        if (HasAnyRenderingCameraForDisplay(0)) return;

        // Find local player's LocalCameraActivator first (best signal)
        LocalCameraActivator lca = FindLocalLca();
        Camera playerCam = null;

        if (lca != null)
        {
            if (lca.playerCamera == null)
                lca.playerCamera = lca.GetComponentInChildren<Camera>(true);
            playerCam = lca.playerCamera;
        }

        if (playerCam == null)
        {
            // Fallback: any camera under a NetworkBehaviour that isLocalPlayer
#if UNITY_2023_1_OR_NEWER
            var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var cams = Resources.FindObjectsOfTypeAll<Camera>();
#endif
            for (int i = 0; i < cams.Length; i++)
            {
                var cam = cams[i];
                if (cam == null) continue;
                if (!cam.gameObject.scene.IsValid()) continue;
                var nb = cam.GetComponentInParent<NetworkBehaviour>();
                if (nb != null && nb.isLocalPlayer)
                {
                    playerCam = cam;
                    break;
                }
            }
        }

        if (playerCam == null)
        {
            if (verboseLogs) Debug.LogWarning("[CameraSafetyNet] No local player camera found.");
            return;
        }

        // Activate full parent chain and normalize
        ActivateAncestorsAndSelf(playerCam.transform);
        NormalizeCameraOutput(playerCam);
        playerCam.enabled = true;

        // Disable all other cameras (scene-only)
#if UNITY_2023_1_OR_NEWER
        var allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var allCams = Resources.FindObjectsOfTypeAll<Camera>();
#endif
        for (int i = 0; i < allCams.Length; i++)
        {
            var cam = allCams[i];
            if (cam == null) continue;
            if (!cam.gameObject.scene.IsValid()) continue;
            if (cam == playerCam) continue;

            // If it is a known lobby camera, turn it off
            if (LobbyStage.Instance != null && LobbyStage.Instance.lobbyCamera == cam)
            {
                cam.enabled = false;
                continue;
            }

            // Disable all other cameras to guarantee one clear winner
            cam.enabled = false;
        }

        // Single AudioListener policy
        var playerAL = playerCam.GetComponent<AudioListener>();
#if UNITY_2023_1_OR_NEWER
        var allAL = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var allAL = Resources.FindObjectsOfTypeAll<AudioListener>();
#endif
        for (int i = 0; i < allAL.Length; i++)
        {
            var al = allAL[i];
            if (al == null) continue;
            if (!al.gameObject.scene.IsValid()) continue;
            al.enabled = (al == playerAL);
        }

        if (verboseLogs) Debug.Log("[CameraSafetyNet] Recovered player camera: " + playerCam.name);
    }

    private static bool HasAnyRenderingCameraForDisplay(int display)
    {
#if UNITY_2023_1_OR_NEWER
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var cams = GameObject.FindObjectsOfType<Camera>();
#endif
        for (int i = 0; i < cams.Length; i++)
        {
            var cam = cams[i];
            if (cam == null) continue;
            if (cam.enabled && cam.gameObject.activeInHierarchy && cam.targetDisplay == display)
                return true;
        }
        return false;
    }

    private static LocalCameraActivator FindLocalLca()
    {
#if UNITY_2023_1_OR_NEWER
        var lcas = Object.FindObjectsByType<LocalCameraActivator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var lcas = Resources.FindObjectsOfTypeAll<LocalCameraActivator>();
#endif
        for (int i = 0; i < lcas.Length; i++)
        {
            var lca = lcas[i];
            if (lca == null) continue;
            var nb = lca as NetworkBehaviour;
            if (nb != null && nb.isLocalPlayer)
                return lca;
        }
        return null;
    }

    private static void ActivateAncestorsAndSelf(Transform leaf)
    {
        if (leaf == null) return;

        System.Collections.Generic.List<Transform> chain = new System.Collections.Generic.List<Transform>();
        Transform t = leaf;
        while (t != null)
        {
            chain.Add(t);
            t = t.parent;
        }
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            Transform cur = chain[i];
            if (!cur.gameObject.activeSelf)
                cur.gameObject.SetActive(true);
        }
    }

    private static void NormalizeCameraOutput(Camera cam)
    {
        if (cam == null) return;
        cam.targetDisplay = 0; // Display 1
        cam.rect = new Rect(0f, 0f, 1f, 1f);
        cam.stereoTargetEye = StereoTargetEyeMask.None;

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        var acd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (acd != null && acd.renderType != UnityEngine.Rendering.Universal.CameraRenderType.Base)
            acd.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
#endif
    }
}
