using UnityEngine;
using Mirror;

/// Attach this to the LOBBY camera GameObject.
/// While the lobby is active:
///   - Cursor is visible & unlocked
///   - Local player's crosshair is forced OFF
/// When the lobby ends:
///   - Crosshair is forced ON (game controls can still change it later)
[DefaultExecutionOrder(48000)]
[AddComponentMenu("Net/Lobby Camera Crosshair Disabler")]
public class LobbyCameraCrosshairDisabler : MonoBehaviour
{
    [Header("Crosshair auto-find (under local player's camera)")]
    public string crosshairObjectName = "Crosshair"; // fallback search name

    Camera _localCam;
    Behaviour _crosshairBehaviour; // e.g., CrosshairDot
    GameObject _crosshairRoot;

    void LateUpdate()
    {
        bool inLobby = LobbyStage.Instance ? LobbyStage.Instance.lobbyActive : true;

        // Always enforce cursor for lobby: visible + unlocked
        if (inLobby)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        EnsureLocalCameraAndCrosshairRefs();

        // Crosshair state: OFF in lobby, ON in game
        bool shouldBeActive = !inLobby;

        if (_crosshairBehaviour) _crosshairBehaviour.enabled = shouldBeActive;
        if (_crosshairRoot) _crosshairRoot.SetActive(shouldBeActive);
    }

    void EnsureLocalCameraAndCrosshairRefs()
    {
        if (_localCam == null)
        {
            if (NetworkClient.active && NetworkClient.localPlayer != null)
            {
                var lca = NetworkClient.localPlayer.GetComponent<LocalCameraActivator>();
                if (lca && lca.playerCamera) _localCam = lca.playerCamera;
            }
            if (_localCam == null) _localCam = Camera.main;
        }
        if (_localCam == null) return;

        if (_crosshairBehaviour == null)
        {
            // Try a known component type first (CrosshairDot)
            _crosshairBehaviour = _localCam.GetComponentInChildren<CrosshairDot>(true);
        }
        if (_crosshairBehaviour == null && _crosshairRoot == null && !string.IsNullOrEmpty(crosshairObjectName))
        {
            var t = FindDeepChild(_localCam.transform, crosshairObjectName);
            if (t) _crosshairRoot = t.gameObject;
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
