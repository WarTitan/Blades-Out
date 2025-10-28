// FILE: LobbyLocalDisabler.cs
// FULL REPLACEMENT (ASCII only)
// Keeps only LocalCameraController; no references to PlayerLook.

using UnityEngine;
using Mirror;

[AddComponentMenu("Net/Lobby Local Disabler")]
public class LobbyLocalDisabler : NetworkBehaviour
{
    public bool verboseLogs = false;

    private LocalCameraController lookFP;
    private LocalCameraActivator lca;

    private float nextEnforceTime;

    void Awake()
    {
        lookFP = GetComponent<LocalCameraController>();
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
            nextEnforceTime = Time.unscaledTime + 0.5f;
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
        // Treat forced gameplay as gameplay even if lobby is still active
        return forced || !lobbyActive;
    }

    private void EnforceNow()
    {
        if (!isLocalPlayer) return;

        bool gameplay = IsGameplayNow();

        if (lookFP != null)
        {
            lookFP.enabled = gameplay;
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[LobbyLocalDisabler] gameplay=" + gameplay +
                " forced=" + (lca != null && lca.IsGameplayForced) +
                " LCC=" + (lookFP != null && lookFP.enabled)
            );
        }
    }

    // Optional: can be called by TeleportHelper immediately after teleport
    public void ForceEnableGameplay()
    {
        if (!isLocalPlayer) return;
        if (lookFP != null) lookFP.enabled = true;
    }
}
