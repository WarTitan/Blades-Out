using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Netcode.Transports; // ✅ your correct namespace

public class SteamNetworkBootstrap : MonoBehaviour
{
#pragma warning disable 0414
    private bool isHost;
    [SerializeField] private bool autoHostIfOwner = true;
#pragma warning restore 0414

    private static bool steamInitialized = false;

    private void Start()
    {
        // ✅ Initialize Steam only once
        if (!steamInitialized)
        {
            try
            {
                if (!SteamManager.Initialized)
                {
                    Debug.LogError("❌ SteamManager not initialized — make sure SteamManager prefab is in your scene!");
                    return;
                }

                steamInitialized = true;
                Debug.Log($"[Steamworks.NET] Steam initialized as {SteamFriends.GetPersonaName()}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Steam init failed: {e}");
                return;
            }
        }

#if UNITY_EDITOR
        // ✅ ParrelSync auto logic (clone = client, main = host)
        if (ParrelSync.ClonesManager.IsClone())
        {
            Debug.Log("🔵 Clone detected — joining host...");
            JoinGame();
        }
        else
        {
            Debug.Log("🟢 Main instance — starting host...");
            HostGame();
        }
#else
        // ✅ In standalone builds, use autoHostIfOwner
        if (autoHostIfOwner)
        {
            HostGame();
        }
        else
        {
            JoinGame();
        }
#endif
    }

    public void HostGame()
    {
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as SteamNetworkingSocketsTransport;
        if (transport == null)
        {
            Debug.LogError("❌ SteamNetworkingSocketsTransport not found! Please assign it in the NetworkManager.");
            return;
        }

        Debug.Log("🟢 Starting as Host...");
        isHost = true;
        NetworkManager.Singleton.StartHost();
    }

    public void JoinGame()
    {
        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as SteamNetworkingSocketsTransport;
        if (transport == null)
        {
            Debug.LogError("❌ SteamNetworkingSocketsTransport not found! Please assign it in the NetworkManager.");
            return;
        }

        // ✅ Use your own Steam ID for ParrelSync testing
        var hostSteamId = SteamUser.GetSteamID();
        transport.ConnectToSteamID = hostSteamId.m_SteamID;

        Debug.Log($"🔵 Joining host {hostSteamId}...");
        isHost = false;
        NetworkManager.Singleton.StartClient();
    }
}
