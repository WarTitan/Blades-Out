using UnityEngine;
using Mirror;

public class LocalCameraActivator : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener playerAudioListener;

    void Awake()
    {
        // Auto-find if you forget to assign in Inspector
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
        if (playerAudioListener == null && playerCamera != null)
            playerAudioListener = playerCamera.GetComponent<AudioListener>();
    }

    public override void OnStartLocalPlayer()
    {
        SetActive(true);
        Debug.Log("[LocalCameraActivator] Enabled camera for local player.");
    }

    void Start()
    {
        if (!isLocalPlayer)
            SetActive(false);
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer)
            SetActive(false);
    }

    private void SetActive(bool value)
    {
        if (playerCamera != null) playerCamera.enabled = value;
        if (playerAudioListener != null) playerAudioListener.enabled = value;
    }
}
