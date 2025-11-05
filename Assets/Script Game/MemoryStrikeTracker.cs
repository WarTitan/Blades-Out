// FILE: MemoryStrikeTracker.cs
// FULL FILE (ASCII only)

using UnityEngine;
using Mirror;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Memory/Memory Strike Tracker")]
public class MemoryStrikeTracker : NetworkBehaviour
{
    public static MemoryStrikeTracker Instance;
    public const int MaxStrikes = 3;

    // netId -> strikes (0..255)
    public class StrikesDict : SyncDictionary<uint, byte> { }
    public readonly StrikesDict strikesByNetId = new StrikesDict();


    public class Entry
    {
        public int strikes;
        public bool eliminated;
    }

    public override void OnStartServer()
    {
        if (Instance != null && Instance != this) { NetworkServer.Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnStartClient()
    {
        if (Instance == null) Instance = this;
    }

    [Server]
    public void Server_AddStrike(uint netId)
    {
        byte cur = 0;
        if (strikesByNetId.ContainsKey(netId)) cur = strikesByNetId[netId];
        if (cur < 255) cur++;
        strikesByNetId[netId] = cur;
    }

    [Server]
    public void Server_Clear(uint netId)
    {
        if (strikesByNetId.ContainsKey(netId)) strikesByNetId.Remove(netId);
    }

    [Server]
    public void Server_ClearAll()
    {
        strikesByNetId.Clear();
    }

    // Client-side lookup used by HUD
    public static Entry FindForNetId(uint netId)
    {
        var e = new Entry();
        if (Instance == null) { e.strikes = 0; e.eliminated = false; return e; }
        byte cur = 0;
        if (Instance.strikesByNetId.TryGetValue(netId, out cur))
            e.strikes = cur;
        e.eliminated = e.strikes >= MaxStrikes;
        return e;
    }
}
