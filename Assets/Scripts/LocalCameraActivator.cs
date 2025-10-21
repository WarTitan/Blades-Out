using UnityEngine;
using Mirror;
using System.Collections;

public class LocalCameraActivator : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener playerAudioListener;

    [Header("Robustness")]
    public bool periodicallyEnforce = true;
    public float enforceInterval = 0.25f;

    bool forceGameplay = false;

    void Awake()
    {
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>(true);
        if (!playerAudioListener && playerCamera) playerAudioListener = playerCamera.GetComponent<AudioListener>();
    }

    void OnEnable() { LobbyStage.OnLobbyStateChanged += OnLobbyStateChanged; }
    void OnDisable() { LobbyStage.OnLobbyStateChanged -= OnLobbyStateChanged; }

    public override void OnStartLocalPlayer()
    {
        ApplyState(IsLobby());
        if (periodicallyEnforce) StartCoroutine(EnforceLoop());
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer) ApplyState(true);
    }

    void OnLobbyStateChanged(bool lobbyActive)
    {
        if (isLocalPlayer && !forceGameplay) ApplyState(lobbyActive);
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
            ApplyState(IsLobby());
            yield return new WaitForSeconds(enforceInterval);
        }
    }

    void ApplyState(bool lobbyActive)
    {
        if (!isLocalPlayer) return;

        bool enablePlayerCam = !lobbyActive;

        if (playerCamera) playerCamera.enabled = enablePlayerCam;
        if (playerAudioListener) playerAudioListener.enabled = enablePlayerCam;

        // Cursor policy per state
        if (enablePlayerCam)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Safety: shut off lobby camera when entering gameplay
        if (!lobbyActive && LobbyStage.Instance && LobbyStage.Instance.lobbyCamera)
            LobbyStage.Instance.lobbyCamera.enabled = false;
    }

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
