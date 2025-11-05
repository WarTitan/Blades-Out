// FILE: LocalCameraActivator.cs
// Exposes IsGameplayForced so other scripts can treat the local player as in-game
// even while the global lobby remains active.

using UnityEngine;
using UnityEngine.Rendering;
using Mirror;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Net/Local Camera Activator")]
[DefaultExecutionOrder(47000)]
public class LocalCameraActivator : NetworkBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public AudioListener playerAudioListener;

    [Header("Behavior")]
    public bool periodicallyEnforce = true;
    public float enforceDuration = 1.25f;
    public float enforceInterval = 0.15f;

    [Header("Debug")]
    public bool verboseLogs = false;

    [SerializeField] private bool forceGameplay = false;
    public bool IsGameplayForced { get { return forceGameplay; } }

    private Coroutine enforceRoutine;

    private void Awake()
    {
        AutoFindRefs();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isLocalPlayer)
        {
            SafeSetListener(false);
            SafeSetCamera(false);
        }
    }

    public override void OnStartLocalPlayer()
    {
        if (verboseLogs) Debug.Log("[LocalCameraActivator] OnStartLocalPlayer");
        ApplyState();
    }

    private void OnEnable()
    {
        AutoFindRefs();
        ApplyState();
    }

    private void OnDisable()
    {
        if (isLocalPlayer)
        {
            SafeSetListener(false);
            SafeSetCamera(false);
        }
    }

    // Called by TeleportHelper on the owner client after teleport
    public void ForceEnterGameplay()
    {
        if (!isLocalPlayer) return;

        forceGameplay = true; // IMPORTANT: stays true; we do NOT auto-clear this.

        ApplyState();
        StartEnforceWindow();
    }

    private void StartEnforceWindow()
    {
        if (!periodicallyEnforce) return;
        if (enforceRoutine != null) StopCoroutine(enforceRoutine);
        enforceRoutine = StartCoroutine(EnforceForSeconds(enforceDuration, enforceInterval));
    }

    private IEnumerator EnforceForSeconds(float duration, float interval)
    {
        float end = Time.unscaledTime + Mathf.Max(0.05f, duration);
        float step = Mathf.Max(0.02f, interval);

        while (Time.unscaledTime < end)
        {
            ApplyState();
            yield return new WaitForSecondsRealtime(step);
        }
        enforceRoutine = null;
    }

    private void ApplyState()
    {
        if (!isClient) return;

        bool lobbyActive = LobbyStage.Instance != null && LobbyStage.Instance.lobbyActive;
        bool shouldEnableLocalView = isLocalPlayer && (!lobbyActive || forceGameplay);

        if (shouldEnableLocalView)
        {
            if (playerCamera != null) ActivateAncestorsAndSelf(playerCamera.transform);
            if (playerAudioListener != null) ActivateAncestorsAndSelf(playerAudioListener.transform);

            SafeSetCamera(true);
            SafeSetListener(true);

            if (LobbyStage.Instance != null)
                LobbyStage.Instance.Client_DisableLobbyCameraLocal();

            NormalizeCameraOutput(playerCamera);
        }
        else
        {
            SafeSetListener(false);
            SafeSetCamera(false);
        }
    }

    private void SafeSetCamera(bool on)
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera == null) return;
        if (on) ActivateAncestorsAndSelf(playerCamera.transform);
        if (playerCamera.enabled != on) playerCamera.enabled = on;
    }

    private void SafeSetListener(bool on)
    {
        if (playerAudioListener == null) playerAudioListener = GetComponentInChildren<AudioListener>(true);
        if (playerAudioListener == null) return;
        if (on) ActivateAncestorsAndSelf(playerAudioListener.transform);
        if (playerAudioListener.enabled != on) playerAudioListener.enabled = on;
    }

    private void AutoFindRefs()
    {
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerAudioListener == null) playerAudioListener = GetComponentInChildren<AudioListener>(true);
    }

    private static void ActivateAncestorsAndSelf(Transform leaf)
    {
        if (leaf == null) return;
        List<Transform> chain = new List<Transform>();
        Transform t = leaf;
        while (t != null)
        {
            chain.Add(t);
            t = t.parent;
        }
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (!chain[i].gameObject.activeSelf)
                chain[i].gameObject.SetActive(true);
        }
    }

    private static void NormalizeCameraOutput(Camera cam)
    {
        if (cam == null) return;

        cam.targetDisplay = 0;
        cam.rect = new Rect(0f, 0f, 1f, 1f);

        // Only allowed in Built-in render pipeline; SRP (URP/HDRP) will throw
        if (GraphicsSettings.currentRenderPipeline == null)
        {
            cam.stereoTargetEye = StereoTargetEyeMask.None;
        }

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        var acd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (acd != null && acd.renderType != UnityEngine.Rendering.Universal.CameraRenderType.Base)
            acd.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Base;
#endif
    }
}
