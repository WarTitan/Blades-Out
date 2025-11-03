// FILE: MemoryLevelTracker.cs
// FULL FILE (ASCII only)
// Server-authoritative memory levels per player (netId). Syncs to clients via Mirror.

using UnityEngine;
using Mirror;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Memory/Memory Level Tracker")]
public class MemoryLevelTracker : NetworkBehaviour
{
    public static MemoryLevelTracker Instance;

    // netId -> level (1..)
    public class LevelsDict : SyncDictionary<uint, ushort> { }
    public readonly LevelsDict levelByNetId = new LevelsDict();

    public override void OnStartServer()
    {
        if (Instance != null && Instance != this)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnStartClient()
    {
        if (Instance == null) Instance = this;
    }

    public static int GetLevelForNetId(uint netId)
    {
        if (netId == 0) return 1;
        if (Instance == null) return 1;
        ushort lv;
        if (Instance.levelByNetId.TryGetValue(netId, out lv))
        {
            if (lv < 1) return 1;
            return lv;
        }
        return 1;
    }

    [Server]
    public void Server_SetLevel(uint netId, int level)
    {
        if (netId == 0) return;
        if (level < 1) level = 1;
        levelByNetId[netId] = (ushort)level;
    }

    [Server]
    public void Server_OnMemoryResultUpdate(uint netId, bool success)
    {
        if (netId == 0) return;
        ushort cur = 1;
        if (levelByNetId.ContainsKey(netId))
            cur = levelByNetId[netId] < 1 ? (ushort)1 : levelByNetId[netId];

        if (success)
        {
            // advance level on success
            if (cur < ushort.MaxValue) cur++;
        }
        else
        {
            // fail: repeat same level -> do nothing
        }

        levelByNetId[netId] = cur;
    }

    [Server]
    public void Server_Clear(uint netId)
    {
        if (levelByNetId.ContainsKey(netId)) levelByNetId.Remove(netId);
    }

    [Server]
    public void Server_ClearAll()
    {
        levelByNetId.Clear();
    }
}
