// FILE: TurnInput.cs
// NEW FILE (ASCII)
// Put this on the player prefab. Lets the current-turn player press "1" to end their turn.

using UnityEngine;
using Mirror;

[AddComponentMenu("Gameplay/Turn Input (Local)")]
public class TurnInput : NetworkBehaviour
{
    void Update()
    {
        if (!isLocalPlayer) return;
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Cmd_EndTurn();
        }
    }

    [Command]
    private void Cmd_EndTurn()
    {
        var id = GetComponent<NetworkIdentity>();
        if (TurnManagerNet.Instance != null && id != null)
        {
            TurnManagerNet.Instance.Server_EndCurrentTurnBy(id.netId);
        }
    }
}
