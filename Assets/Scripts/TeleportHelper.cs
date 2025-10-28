// FILE: TeleportHelper.cs
// FULL REPLACEMENT (ASCII only)
// Owner-side snap + camera/controls handoff.
// Ensures the client moves to the table and enters gameplay view.

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
        // 1) Owner-side snap
        var cc = GetComponent<CharacterController>();
        bool hadCC = (cc != null && cc.enabled);
        if (hadCC) cc.enabled = false;

        ActivateAncestorsAndSelf(transform);
        transform.SetPositionAndRotation(position, rotation);

        if (hadCC) cc.enabled = true;

        // 2) Force gameplay camera/controls
        var lca = GetComponent<LocalCameraActivator>();
        if (lca != null) lca.ForceEnterGameplay();

        var lld = GetComponent<LobbyLocalDisabler>();
        if (lld != null) lld.ForceEnableGameplay();

        var lcc = GetComponent<LocalCameraController>();
        if (lcc != null)
        {
            lcc.enabled = true;
        }

        // 3) Briefly enforce locked cursor so look gets input immediately
        StartCoroutine(EnforceLockedCursor(cursorEnforceSeconds, cursorEnforceInterval));

        // 4) Turn off lobby camera locally as extra safety
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

    private static void ActivateAncestorsAndSelf(Transform leaf)
    {
        Transform t = leaf;
        System.Collections.Generic.List<Transform> chain = new System.Collections.Generic.List<Transform>();
        while (t != null) { chain.Add(t); t = t.parent; }
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (!chain[i].gameObject.activeSelf)
                chain[i].gameObject.SetActive(true);
        }
    }
}
