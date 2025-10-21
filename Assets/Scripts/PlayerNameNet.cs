using UnityEngine;
using Mirror;
using Steamworks;

[AddComponentMenu("Net/Player Name (Steam)")]
public class PlayerNameNet : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNameChanged))] public string displayName;

    public override void OnStartLocalPlayer()
    {
        string n = null;
        try
        {
            if (BladesOut.SteamInitializer.Initialized)
                n = SteamFriends.GetPersonaName();
        }
        catch { /* Steamworks not ready or not present */ }

        if (string.IsNullOrEmpty(n))
            n = System.Environment.UserName;

        if (string.IsNullOrEmpty(n))
            n = "Player " + netId;

        CmdSetName(n);
    }

    [Command(requiresAuthority = true)]
    void CmdSetName(string n)
    {
        displayName = string.IsNullOrWhiteSpace(n) ? ("Player " + netId) : n.Trim();
    }

    void OnNameChanged(string oldV, string newV)
    {
        // nameplates read this every frame; no extra work needed here
    }
}
