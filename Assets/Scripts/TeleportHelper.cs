using UnityEngine;
using Mirror;
using System.Collections;

[AddComponentMenu("Net/Teleport Helper")]
public class TeleportHelper : NetworkBehaviour
{
    [TargetRpc]
    public void TargetSnapAndEnterGameplay(NetworkConnectionToClient conn, Vector3 position, Quaternion rotation)
    {
        // Safe local snap
        var cc = GetComponent<CharacterController>();
        bool hadCC = (cc != null && cc.enabled);
        if (hadCC) cc.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        if (hadCC) cc.enabled = true;

        // Tell local systems we're in gameplay
        var lca = GetComponent<LocalCameraActivator>();
        if (lca != null) lca.ForceEnterGameplay();

        var lld = GetComponent<LobbyLocalDisabler>();
        if (lld != null) lld.ForceEnableGameplay();

        // Enforce that our player camera wins (prevents flicker/black screen)
        StartCoroutine(EnforceLocalViewForSeconds(1.0f));

        // Gameplay cursor policy
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator EnforceLocalViewForSeconds(float seconds)
    {
        float end = Time.unscaledTime + Mathf.Max(0.25f, seconds);
        while (Time.unscaledTime < end)
        {
            EnsureLocalPlayerViewWins();
            yield return null;
        }
        EnsureLocalPlayerViewWins();
    }

    private void EnsureLocalPlayerViewWins()
    {
        // Find the camera on this local player
        Camera playerCam = GetComponentInChildren<Camera>(true);

        if (playerCam == null)
        {
            // No player camera? avoid black screen by keeping lobby camera on
            if (LobbyStage.Instance != null && LobbyStage.Instance.lobbyCamera != null)
                LobbyStage.Instance.lobbyCamera.enabled = true;
            return;
        }

        // Enable this player's camera and AudioListener
        if (!playerCam.enabled) playerCam.enabled = true;
        var playerAL = playerCam.GetComponent<AudioListener>();
        if (playerAL != null && !playerAL.enabled) playerAL.enabled = true;

        // Disable every other camera locally (including lobby camera)
#if UNITY_2023_1_OR_NEWER
        var allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var allCams = Resources.FindObjectsOfTypeAll<Camera>();
#endif
        for (int i = 0; i < allCams.Length; i++)
        {
            var cam = allCams[i];
            if (cam == null) continue;
            if (!cam.gameObject.scene.IsValid()) continue; // ignore assets/prefabs
            if (cam == playerCam) continue;
            cam.enabled = false;
        }

        // Ensure only this player's AudioListener is active
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

        // Extra safety: explicitly keep lobby camera off on this client
        if (LobbyStage.Instance != null && LobbyStage.Instance.lobbyCamera != null)
            LobbyStage.Instance.lobbyCamera.enabled = false;
    }
}
