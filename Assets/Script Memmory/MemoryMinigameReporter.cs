// FILE: MemoryMinigameReporter.cs
// FULL FILE (ASCII only)

using UnityEngine;
using Mirror;

[AddComponentMenu("Gameplay/Memory/Memory Minigame Reporter")]
public class MemoryMinigameReporter : NetworkBehaviour
{
    public static MemoryMinigameReporter Local { get; private set; }

    public override void OnStartLocalPlayer()
    {
        Local = this;
    }

    public override void OnStopClient()
    {
        if (Local == this) Local = null;
    }

    // Called by the minigame. best = 1 (success) or 0 (fail).
    public static void ReportResult(int token, int best)
    {
        if (Local != null) Local.Cmd_ReportResult(token, best);
        else Debug.LogWarning("[MemoryMinigameReporter] Local is null; cannot report.");
    }

    [Command]
    private void Cmd_ReportResult(int token, int best)
    {
        var tm = TurnManagerNet.Instance;
        if (tm != null)
        {
            tm.Server_OnMemoryResult(netId, token, best);
        }
        else
        {
            Debug.LogWarning("[MemoryMinigameReporter] TurnManagerNet.Instance is null on server.");
        }
    }
}
