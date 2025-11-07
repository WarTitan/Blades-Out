// FILE: TurnInput.cs
// DROP-IN REPLACEMENT (ASCII)
// Press "1" to end YOUR current crafting window (or skip where applicable).

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
