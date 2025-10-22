using UnityEngine;
using Mirror;

[RequireComponent(typeof(LobbyCharacterSwitcher))]
[AddComponentMenu("Net/Lobby Input")]
public class LobbyInput : NetworkBehaviour
{
    public KeyCode prevKey = KeyCode.LeftArrow;
    public KeyCode nextKey = KeyCode.RightArrow;

    private LobbyCharacterSwitcher switcher;

    void Awake() { switcher = GetComponent<LobbyCharacterSwitcher>(); }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (LobbyStage.Instance == null || !LobbyStage.Instance.lobbyActive) return;

        if (Input.GetKeyDown(prevKey)) switcher.CmdCycle(-1);
        if (Input.GetKeyDown(nextKey)) switcher.CmdCycle(+1);
        // Ready toggling (Key 3) is handled by LobbyReady
    }
}
