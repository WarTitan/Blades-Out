using UnityEngine;
using Mirror;

/// Enforces crosshair visibility: OFF in lobby, ON in gameplay.
/// Attach to player prefab root (local player).
[DefaultExecutionOrder(30000)]
[AddComponentMenu("Net/Crosshair Visibility Enforcer")]
public class CrosshairVisibilityEnforcer : NetworkBehaviour
{
    [Header("Assign either component or root (auto-find if empty)")]
    public MonoBehaviour crosshairBehaviour; // e.g., CrosshairDot
    public GameObject crosshairRoot;
    public bool autoFindUnderPlayerCamera = true;
    public string crosshairObjectName = "Crosshair";

    LocalCameraActivator lca;
    float nextResolve;

    void Awake()
    {
        lca = GetComponent<LocalCameraActivator>();
    }

    public override void OnStartLocalPlayer()
    {
        ResolveCrosshair(true);
    }

    void LateUpdate()
    {
        if (!isLocalPlayer) return;

        if (Time.time >= nextResolve)
        {
            ResolveCrosshair(false);
            nextResolve = Time.time + 0.5f;
        }

        bool inLobby = LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : true;
        bool shouldBeActive = !inLobby;

        if (crosshairBehaviour) crosshairBehaviour.enabled = shouldBeActive;
        if (crosshairRoot) crosshairRoot.SetActive(shouldBeActive);
    }

    void ResolveCrosshair(bool force)
    {
        if (!autoFindUnderPlayerCamera) return;
        if (!force && (crosshairBehaviour || crosshairRoot)) return;

        Camera cam = (lca && lca.playerCamera) ? lca.playerCamera : GetComponentInChildren<Camera>(true);
        if (!cam) return;

        if (!crosshairBehaviour)
        {
            var ch = cam.GetComponentInChildren<CrosshairDot>(true);
            if (ch) crosshairBehaviour = ch;
        }
        if (!crosshairBehaviour && !crosshairRoot && !string.IsNullOrEmpty(crosshairObjectName))
        {
            var t = FindDeepChild(cam.transform, crosshairObjectName);
            if (t) crosshairRoot = t.gameObject;
        }
    }

    static Transform FindDeepChild(Transform parent, string name)
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
