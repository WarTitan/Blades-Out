using UnityEngine;
using Mirror;
using System.Collections.Generic;

[AddComponentMenu("Cards/Draft Draw Net (Mirror)")]
[RequireComponent(typeof(PlayerState))]
public class DraftDrawNet : NetworkBehaviour
{
    // server-side stash of current choices per player
    private int[] serverChoices = null;

    private PlayerState ps;

    void Awake()
    {
        ps = GetComponent<PlayerState>();
    }

    // Call from server when a draw would happen
    [Server]
    public void Server_StartDraft(int draws = 1)
    {
        // We only support "pick 1" for now
        if (draws <= 0 || ps == null) return;

        // Build 3 unique choices from the database
        var db = ps.database != null ? ps.database : CardDatabase.Active;
        if (db == null) return;

        HashSet<int> uniq = new HashSet<int>();
        for (int i = 0; i < 32 && uniq.Count < 3; i++)
        {
            int id = db.GetRandomId();
            if (id >= 0) uniq.Add(id);
        }
        if (uniq.Count == 0) return;

        serverChoices = new int[Mathf.Min(3, uniq.Count)];
        int k = 0;
        foreach (var id in uniq)
        {
            if (k >= serverChoices.Length) break;
            serverChoices[k++] = id;
        }

        // Tell the owning client to show the 3 choices
        Target_BeginDraft(connectionToClient, serverChoices);
    }

    // Client: show the 3 floating choices
    [TargetRpc]
    private void Target_BeginDraft(NetworkConnection target, int[] choiceIds)
    {
        DraftDrawPicker.ShowChoices(this, choiceIds);
    }

    // Client -> Server: player clicked one of the choices
    [Command]
    public void Cmd_ChooseDraftCard(int chosenId)
    {
        if (ps == null) return;
        if (serverChoices == null || serverChoices.Length == 0) return;

        bool ok = false;
        for (int i = 0; i < serverChoices.Length; i++)
            if (serverChoices[i] == chosenId) { ok = true; break; }

        if (!ok) return;

        // Add chosen to hand at level 1 (adjust if you use other levels)
        ps.Server_AddToHand(chosenId, 1);

        // clear server stash
        serverChoices = null;

        // Tell client to hide the visual choices
        Target_EndDraft(connectionToClient, chosenId);
    }

    [TargetRpc]
    private void Target_EndDraft(NetworkConnection target, int chosenId)
    {
        DraftDrawPicker.HideChoices();
    }
}
