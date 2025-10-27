// FILE: LocalCameraActivator.cs
using UnityEngine;
using Mirror;
using System.Collections;

[AddComponentMenu("Net/Local Camera Activator")]
public class LocalCameraActivator : NetworkBehaviour
{
    [Header("Refs")]
    public Camera playerCamera;
    public AudioListener playerAudioListener;

    [Header("Robustness")]
    public bool periodicallyEnforce = true;
    public float enforceInterval = 0.25f;

    private bool forceGameplay;

    void Awake()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerAudioListener == null && playerCamera != null) playerAudioListener = playerCamera.GetComponent<AudioListener>();
    }

    void OnEnable()
    {
        LobbyStage.OnLobbyStateChanged += OnLobbyStateChanged; // Action<bool>
    }

    void OnDisable()
    {
        LobbyStage.OnLobbyStateChanged -= OnLobbyStateChanged;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer) SafeSet(false); // remote players never render locally
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ApplyState(IsLobby());
        if (periodicallyEnforce) StartCoroutine(EnforceLoop());
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        if (isLocalPlayer) ApplyState(true);
    }

    private void OnLobbyStateChanged(bool lobbyActive)
    {
        if (isLocalPlayer && !forceGameplay)
            ApplyState(lobbyActive);
    }

    private bool IsLobby()
    {
        if (forceGameplay) return false;
        var inst = LobbyStage.Instance;
        return inst ? inst.lobbyActive : true;
    }

    private IEnumerator EnforceLoop()
    {
        var wait = new WaitForSeconds(enforceInterval);
        while (isLocalPlayer)
        {
            ApplyState(IsLobby());
            yield return wait;
        }
    }

    private void ApplyState(bool lobbyActive)
    {
        if (!isLocalPlayer) return;

        bool enablePlayerCam = !lobbyActive || forceGameplay;
        SafeSet(enablePlayerCam);

        // Safety: when entering gameplay, locally turn off lobby camera
        if (!lobbyActive && LobbyStage.Instance != null)
            LobbyStage.Instance.Client_DisableLobbyCameraLocal();
    }

    private void SafeSet(bool on)
    {
        if (playerCamera != null) playerCamera.enabled = on;
        if (playerAudioListener != null) playerAudioListener.enabled = on;
    }

    // Called by TeleportHelper after teleport
    public void ForceEnterGameplay()
    {
        forceGameplay = true;
        ApplyState(false);
    }

    public void ForceEnterLobby()
    {
        if (forceGameplay) return;
        ApplyState(true);
    }
}
