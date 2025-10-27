// FILE: LobbyLocalDisabler.cs
// The enforcer that was flipping you back to lobby every 0.5s.
// Now it treats IsGameplayForced as gameplay, so it will KEEP your look script enabled.

using UnityEngine;
using Mirror;

[AddComponentMenu("Net/Lobby Local Disabler")]
public class LobbyLocalDisabler : NetworkBehaviour
{
    public bool verboseLogs = false;

    private LocalCameraController lookFP;
    private PlayerLook lookSimple;
    private LocalCameraActivator lca;

    private float nextEnforceTime;

    void Awake()
    {
        lookFP = GetComponent<LocalCameraController>();
        lookSimple = GetComponent<PlayerLook>();
        lca = GetComponent<LocalCameraActivator>();
    }

    void OnEnable()
    {
        LobbyStage.OnLobbyStateChanged += OnLobbyStateChanged;
        EnforceNow();
    }

    void OnDisable()
    {
        LobbyStage.OnLobbyStateChanged -= OnLobbyStateChanged;
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (Time.unscaledTime >= nextEnforceTime)
        {
            EnforceNow();
            nextEnforceTime = Time.unscaledTime + 0.5f; // this tick was the thing undoing your control
        }
    }

    private void OnLobbyStateChanged(bool lobbyActive)
    {
        if (!isLocalPlayer) return;
        EnforceNow();
    }

    private bool IsGameplayNow()
    {
        bool lobbyActive = LobbyStage.Instance != null && LobbyStage.Instance.lobbyActive;
        bool forced = (lca != null && lca.IsGameplayForced);
        // KEY RULE: if forced, treat as gameplay even if lobby is still active
        return forced || !lobbyActive;
    }

    private void EnforceNow()
    {
        if (!isLocalPlayer) return;

        bool gameplay = IsGameplayNow();

        if (gameplay)
        {
            if (lookFP != null && !lookFP.enabled) lookFP.enabled = true;
            if (lookSimple != null && lookSimple.enabled) lookSimple.enabled = false;
        }
        else
        {
            if (lookFP != null && lookFP.enabled) lookFP.enabled = false;
            if (lookSimple != null && lookSimple.enabled) lookSimple.enabled = false;
        }

        if (verboseLogs)
        {
            Debug.Log("[LobbyLocalDisabler] gameplay=" + gameplay
                + " forced=" + (lca != null && lca.IsGameplayForced)
                + " LCC=" + (lookFP != null && lookFP.enabled)
                + " Simple=" + (lookSimple != null && lookSimple.enabled));
        }
    }

    // Optional: called by TeleportHelper immediately after teleport
    public void ForceEnableGameplay()
    {
        if (!isLocalPlayer) return;
        if (lookFP != null) lookFP.enabled = true;
        if (lookSimple != null) lookSimple.enabled = false;
    }
}
