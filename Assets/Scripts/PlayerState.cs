// FILE: PlayerState.cs
using UnityEngine;
using Mirror;

public class PlayerState : NetworkBehaviour
{
    // Seat index used by spawn/teleport logic.
    [SyncVar] public int seatIndex = -1;

    // Stub left in place so any old UI calling it won't break.
    [Command]
    public void CmdRequestStartGame()
    {
        // No-op in the no-turns build.
    }
}
