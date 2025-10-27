// FILE: CursorAndAudioManager.cs
// Locks cursor whenever the local player is forced into gameplay OR the lobby has ended.

using UnityEngine;
using Mirror;

[DefaultExecutionOrder(40000)]
public class CursorAndAudioManager : MonoBehaviour
{
    public bool verboseLogs = false;

    void LateUpdate()
    {
        bool lobbyActive = LobbyStage.Instance != null && LobbyStage.Instance.lobbyActive;

        var lca = FindLocalLca();
        bool forced = lca != null && lca.IsGameplayForced;
        bool localInGameplay = (lca != null && lca.isLocalPlayer) && (forced || !lobbyActive);

        if (localInGameplay)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
    }

    private static LocalCameraActivator FindLocalLca()
    {
#if UNITY_2023_1_OR_NEWER
        var lcas = Object.FindObjectsByType<LocalCameraActivator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        var lcas = Resources.FindObjectsOfTypeAll<LocalCameraActivator>();
#endif
        for (int i = 0; i < lcas.Length; i++)
        {
            var lca = lcas[i];
            if (lca == null) continue;
            var nb = lca as NetworkBehaviour;
            if (nb != null && nb.isLocalPlayer) return lca;
        }
        return null;
    }
}
