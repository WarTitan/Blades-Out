using UnityEngine;
using Unity.Netcode;
using Steamworks;

public class SteamNetworkBootstrap : MonoBehaviour
{
    private NetworkManager networkManager;

    private void Start()
    {
        networkManager = FindFirstObjectByType<NetworkManager>();

        if (networkManager == null)
        {
            Debug.LogError("❌ No NetworkManager found in scene!");
            return;
        }

        if (!SteamManager.Initialized)
        {
            Debug.LogError("❌ Steam not initialized!");
            return;
        }

        Debug.Log($"✅ Steam initialized as {SteamFriends.GetPersonaName()}");
    }

    public void HostGame()
    {
        Debug.Log("🟢 Host Game button pressed — starting as Host.");
        NetworkManager.Singleton.StartHost();
    }

    public void JoinGame()
    {
        Debug.Log("🔵 Join Game button pressed — starting as Client.");
        NetworkManager.Singleton.StartClient();
    }

}
