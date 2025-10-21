using UnityEngine;

/// Global, last-word enforcer for cursor + audio listeners.
/// Put this on the same GameObject as LobbyStage (scene singleton).
[DefaultExecutionOrder(40000)]
[AddComponentMenu("Net/Cursor & Audio Manager")]
public class CursorAndAudioManager : MonoBehaviour
{
    [Header("Cursor Policy")]
    // LOBBY: cursor should be visible + unlocked
    public CursorLockMode lobbyLock = CursorLockMode.None;
    public bool lobbyVisible = true;

    // GAME: cursor usually hidden + locked (change if you want it visible in game)
    public CursorLockMode gameplayLock = CursorLockMode.Locked;
    public bool gameplayVisible = false;

    [Header("Always enforce every frame")]
    public bool enforceCursorEveryFrame = true;
    public bool enforceAudioEveryFrame = true;

    void LateUpdate()
    {
        bool inLobby = LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : true;

        if (enforceCursorEveryFrame)
        {
            if (inLobby)
            {
                Cursor.lockState = lobbyLock;
                Cursor.visible = lobbyVisible;
            }
            else
            {
                Cursor.lockState = gameplayLock;
                Cursor.visible = gameplayVisible;
            }
        }

        if (enforceAudioEveryFrame)
            EnforceSingleAudioListener(inLobby);
    }

    void EnforceSingleAudioListener(bool inLobby)
    {
        // Decide the one camera whose AudioListener should be enabled
        Camera desiredCam = null;

        if (inLobby)
        {
            if (LobbyStage.Instance && LobbyStage.Instance.lobbyCamera)
                desiredCam = LobbyStage.Instance.lobbyCamera;
            if (!desiredCam) desiredCam = Camera.main;
        }
        else
        {
            // Prefer local player's gameplay camera
            if (Mirror.NetworkClient.active && Mirror.NetworkClient.localPlayer != null)
            {
                var lca = Mirror.NetworkClient.localPlayer.GetComponent<LocalCameraActivator>();
                if (lca && lca.playerCamera) desiredCam = lca.playerCamera;
            }
            if (!desiredCam) desiredCam = Camera.main;
        }

        AudioListener desiredListener = desiredCam ? desiredCam.GetComponent<AudioListener>() : null;

        // Find all scene AudioListeners (use non-obsolete API on 2023+, safe fallback otherwise)
#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
        var all = Resources.FindObjectsOfTypeAll<AudioListener>();
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var al = all[i];
            if (!al || !al.gameObject.scene.IsValid()) continue; // skip assets/prefabs
            al.enabled = (desiredListener != null && al == desiredListener);
        }
    }
}
