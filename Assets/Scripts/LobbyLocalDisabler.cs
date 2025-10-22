using UnityEngine;
using Mirror;
using System.Collections;

[AddComponentMenu("Net/Lobby Local Disabler")]
public class LobbyLocalDisabler : NetworkBehaviour
{
    [Header("Look drivers")]
    public PlayerLook playerLook;                  // legacy look (optional)
    public LocalCameraController cameraController; // preferred look

    [Header("Which look driver to use in gameplay")]
    public bool preferLocalCameraController = true;

    [Header("Robustness")]
    public bool periodicallyEnforce = true;
    public float enforceInterval = 0.25f;

    private bool forceGameplay = false;

    void Awake()
    {
        if (!playerLook) playerLook = GetComponentInChildren<PlayerLook>(true);
        if (!cameraController) cameraController = GetComponentInChildren<LocalCameraController>(true);
    }

    public override void OnStartLocalPlayer()
    {
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
        return inst ? inst.lobbyActive : true;
    }

    IEnumerator EnforceLoop()
    {
        while (isLocalPlayer)
        {
            Apply(IsLobby());
            yield return new WaitForSeconds(enforceInterval);
        }
    }

    void Apply(bool lobbyActive)
    {
        if (!isLocalPlayer) return;

        bool enableGameplay = !lobbyActive;

        // Exactly one look driver in gameplay, none in lobby
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
    }

    public void ForceEnableGameplay()
    {
        forceGameplay = true;
        Apply(false);
    }

    public void ForceLobby()
    {
        if (forceGameplay) return;
        Apply(true);
    }
}
