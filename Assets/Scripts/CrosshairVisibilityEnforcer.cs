// FILE: CrosshairVisibilityEnforcer.cs
// Strong enforcement: hide ALL CrosshairDot during Darts, show in normal gameplay.
// Runs very late so it overrides any script that tries to show it again.

using UnityEngine;
using Mirror;
using System.Collections.Generic;

[DefaultExecutionOrder(60000)]
[AddComponentMenu("Net/Crosshair Visibility Enforcer")]
public class CrosshairVisibilityEnforcer : NetworkBehaviour
{
    [Header("Optional direct refs (auto-find works too)")]
    public MonoBehaviour crosshairBehaviour;   // e.g., CrosshairDot on a single object
    public GameObject crosshairRoot;

    [Header("Auto-find")]
    public bool autoFindUnderPlayerCamera = true;
    public string crosshairObjectName = "Crosshair";

    [Header("Rules")]
    public bool hideInLobby = true;
    public bool hideDuringDarts = true;

    [Header("Debug")]
    public bool debugLogs = false;

    private LocalCameraActivator lca;
    private Camera cam;
    private float nextResolve;
    private readonly List<CrosshairDot> foundDots = new List<CrosshairDot>();
    private bool lastHideState = false;

    public override void OnStartLocalPlayer()
    {
        lca = GetComponent<LocalCameraActivator>();
        cam = (lca && lca.playerCamera) ? lca.playerCamera : GetComponentInChildren<Camera>(true);
        ResolveTargets(true);
    }

    void LateUpdate()
    {
        if (!isLocalPlayer) return;

        // Re-resolve periodically in case camera/crosshair is recreated
        if (Time.time >= nextResolve)
        {
            ResolveTargets(false);
            nextResolve = Time.time + 0.5f;
        }

        bool inLobby = LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : false;
        bool inDarts = (TurnManagerNet.Instance != null &&
                        TurnManagerNet.Instance.phase == TurnManagerNet.Phase.Darts);

        bool shouldHide = (hideInLobby && inLobby) || (hideDuringDarts && inDarts);

        // Apply hard enforcement
        ApplyState(!shouldHide);

        if (debugLogs && lastHideState != shouldHide)
        {
            lastHideState = shouldHide;
            Debug.Log("[CrosshairVisibilityEnforcer] Crosshair " + (shouldHide ? "HIDDEN" : "SHOWN") +
                      " (lobby=" + inLobby + ", darts=" + inDarts + ")");
        }
    }

    private void ApplyState(bool active)
    {
        // Direct refs
        if (crosshairBehaviour != null) crosshairBehaviour.enabled = active;
        if (crosshairRoot != null) crosshairRoot.SetActive(active);

        // All CrosshairDot under the local camera
        for (int i = 0; i < foundDots.Count; i++)
        {
            var d = foundDots[i];
            if (d == null) continue;

            // Disable the behaviour and the GameObject so no script can flip it back this frame
            if (d.enabled != active) d.enabled = active;
            if (d.gameObject.activeSelf != active) d.gameObject.SetActive(active);
        }
    }

    private void ResolveTargets(bool force)
    {
        if (cam == null || (autoFindUnderPlayerCamera && cam != ((lca && lca.playerCamera) ? lca.playerCamera : cam)))
            cam = (lca && lca.playerCamera) ? lca.playerCamera : GetComponentInChildren<Camera>(true);

        if (!force && cam == null) return;

        // Rebuild list of all CrosshairDot under camera
        foundDots.Clear();
        if (cam != null)
        {
            var dots = cam.GetComponentsInChildren<CrosshairDot>(true);
            for (int i = 0; i < dots.Length; i++)
                if (dots[i] != null) foundDots.Add(dots[i]);
        }

        // Optional: try to find an object by name as a root holder
        if (crosshairRoot == null && cam != null && !string.IsNullOrEmpty(crosshairObjectName))
        {
            var t = FindDeepChild(cam.transform, crosshairObjectName);
            if (t) crosshairRoot = t.gameObject;
        }

        // Optional single-behaviour reference
        if (crosshairBehaviour == null && foundDots.Count > 0)
            crosshairBehaviour = foundDots[0];
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (c.name == name) return c;
            var r = FindDeepChild(c, name);
            if (r) return r;
        }
        return null;
    }
}
