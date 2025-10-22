using UnityEngine;
using Mirror;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerState))]
public class PlayerChestUpgrades : NetworkBehaviour
{
    private PlayerState ps;

    void Awake()
    {
        ps = GetComponent<PlayerState>();
    }

    // Client -> Server: request a chest while it is not your turn.
    [Command(requiresAuthority = true)]
    public void CmdRequestChestUpgrade()
    {
        if (!isServer || ps == null) return;

        if (ChestUpgradeNet.Instance != null)
        {
            ChestUpgradeNet.Instance.StartChestFor(ps);
        }
        else
        {
            Debug.LogWarning("ChestUpgradeNet not present in scene. Add it to a server-side GameObject.");
        }
    }

    // Client -> Server: choose one of the offered hand indices (sent via the local player's authority).
    [Command(requiresAuthority = true)]
    public void CmdChooseChestOption(int chosenHandIndex)
    {
        if (!isServer || ps == null) return;

        if (ChestUpgradeNet.Instance != null)
        {
            ChestUpgradeNet.Instance.Server_ChooseFor(ps, chosenHandIndex);
        }
        else
        {
            Debug.LogWarning("ChestUpgradeNet not present in scene. Add it to a server-side GameObject.");
        }
    }
}
