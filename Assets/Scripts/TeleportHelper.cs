// FILE: TeleportHelper.cs
// Tells LocalCameraActivator to force gameplay (flag stays true) and keeps look enabled.

using UnityEngine;
using Mirror;
using System.Collections;

[AddComponentMenu("Net/Teleport Helper")]
public class TeleportHelper : NetworkBehaviour
{
    public float cursorEnforceSeconds = 1.0f;
    public float cursorEnforceInterval = 0.05f;

    [TargetRpc]
    public void TargetSnapAndEnterGameplay(NetworkConnectionToClient conn, Vector3 position, Quaternion rotation)
    {
        // Flag the local player as "in gameplay" even if global lobby remains active
        var lca = GetComponent<LocalCameraActivator>();
        if (lca != null) lca.ForceEnterGameplay();

        // Make sure the look driver stays ON
        var lld = GetComponent<LobbyLocalDisabler>();
        if (lld != null) lld.ForceEnableGameplay();

        var lcc = GetComponent<LocalCameraController>();
        var legacy = GetComponent<PlayerLook>();

        if (lcc != null)
        {
            if (!lcc.enabled) lcc.enabled = true;
            if (legacy != null && legacy.enabled) legacy.enabled = false;
            lcc.SeedFromCurrentPose();
        }
        else if (legacy != null)
        {
            legacy.enabled = true;
        }

        // Keep cursor locked briefly so input flows immediately
        StartCoroutine(EnforceLockedCursor(cursorEnforceSeconds, cursorEnforceInterval));

        // Turn off lobby cam locally as safety
        if (LobbyStage.Instance != null)
            LobbyStage.Instance.Client_DisableLobbyCameraLocal();
    }

    private IEnumerator EnforceLockedCursor(float seconds, float interval)
    {
        float until = Time.unscaledTime + Mathf.Max(0.25f, seconds);
        float step = Mathf.Max(0.02f, interval);

        while (Time.unscaledTime < until)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
            yield return new WaitForSecondsRealtime(step);
        }
    }
}
