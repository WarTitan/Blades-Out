using UnityEngine;
using Mirror;
using System.Collections;

[AddComponentMenu("Net/Lobby Local Disabler")]
public class LobbyLocalDisabler : NetworkBehaviour
{
    [Header("Look drivers")]
    public PlayerLook playerLook;                      // OPTIONAL legacy look
    public LocalCameraController cameraController;     // Preferred driver

    [Header("Crosshair (optional)")]
    public MonoBehaviour crosshairBehaviour;
    public GameObject crosshairRoot;

    [Header("Auto-find")]
    public bool autoFindUnderPlayerCamera = true;
    public string crosshairObjectName = "Crosshair";

    [Header("Robustness")]
    public bool periodicallyEnforce = true;
    public float enforceInterval = 0.25f;

    [Header("Which look driver to use in gameplay")]
    public bool preferLocalCameraController = true;

    LocalCameraActivator lca;
    bool forceGameplay = false;

    void Awake()
    {
        if (!playerLook) playerLook = GetComponentInChildren<PlayerLook>(true);
        if (!cameraController) cameraController = GetComponentInChildren<LocalCameraController>(true);
        lca = GetComponent<LocalCameraActivator>();
    }

    public override void OnStartLocalPlayer()
    {
        ResolveCrosshairIfNeeded();
        Apply(IsLobby());
        if (periodicallyEnforce) StartCoroutine(EnforceLoop());
    }

    void OnEnable() { LobbyStage.OnLobbyStateChanged += OnLobbyChanged; }
    void OnDisable() { LobbyStage.OnLobbyStateChanged -= OnLobbyChanged; }

    void OnLobbyChanged(bool lobbyActive)
    {
        if (isLocalPlayer && !forceGameplay) Apply(lobbyActive);
    }

    bool IsLobby()
    {
        if (forceGameplay) return false;
        var inst = LobbyStage.Instance;
        return inst ? inst.lobbyActive : true; // default lobby until proven otherwise
    }

    IEnumerator EnforceLoop()
    {
        while (isLocalPlayer)
        {
            Apply(IsLobby());
            yield return new WaitForSeconds(enforceInterval);
        }
    }

    void ResolveCrosshairIfNeeded()
    {
        if (!isLocalPlayer || !autoFindUnderPlayerCamera) return;
        if (crosshairBehaviour || crosshairRoot) return;

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

    void Apply(bool lobbyActive)
    {
        bool enableGameplay = !lobbyActive;

        // Look/input: exactly ONE enabled in gameplay, NONE in lobby
        if (preferLocalCameraController)
        {
            if (cameraController) cameraController.enabled = enableGameplay;
            if (playerLook) playerLook.enabled = false;
        }
        else
        {
            if (playerLook) playerLook.enabled = enableGameplay;
            if (cameraController) cameraController.enabled = false;
        }

        // Crosshair visibility
        if (crosshairBehaviour) crosshairBehaviour.enabled = enableGameplay;
        if (crosshairRoot) crosshairRoot.SetActive(enableGameplay);
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

    // called by TeleportHelper after start
    public void ForceEnableGameplay()
    {
        forceGameplay = true;
        Apply(false);
    }

    // called every frame by LobbyStage while in lobby
    public void ForceLobby()
    {
        if (forceGameplay) return;
        ResolveCrosshairIfNeeded();
        Apply(true);
    }
}
