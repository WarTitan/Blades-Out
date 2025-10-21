using UnityEngine;
using Mirror;

/// Enforces crosshair visibility: OFF while in lobby, ON in gameplay.
/// Runs late every frame so it wins over anything that toggles it on join.
[DefaultExecutionOrder(30000)]
[AddComponentMenu("Net/Crosshair Visibility Enforcer")]
public class CrosshairVisibilityEnforcer : NetworkBehaviour
{
    [Header("Optional explicit refs (leave empty to auto-find)")]
    public MonoBehaviour crosshairBehaviour;   // e.g., CrosshairDot
    public GameObject crosshairRoot;           // or a GameObject to toggle

    [Header("Auto-find under the local player's camera")]
    public bool autoFindUnderPlayerCamera = true;
    public string crosshairObjectName = "Crosshair";

    LocalCameraActivator lca;
    float _nextResolveTime;

    void Awake()
    {
        lca = GetComponent<LocalCameraActivator>();
    }

    public override void OnStartLocalPlayer()
    {
        ResolveCrosshairIfNeeded(force: true);
    }

    void LateUpdate()
    {
        if (!isLocalPlayer) return;

        // Resolve again occasionally (handles late-spawned UI)
        if (Time.time >= _nextResolveTime)
        {
            ResolveCrosshairIfNeeded(force: false);
            _nextResolveTime = Time.time + 0.5f;
        }

        bool inLobby = LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : true;
        bool shouldBeActive = !inLobby;

        if (crosshairBehaviour) crosshairBehaviour.enabled = shouldBeActive;
        if (crosshairRoot) crosshairRoot.SetActive(shouldBeActive);
    }

    void ResolveCrosshairIfNeeded(bool force)
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
