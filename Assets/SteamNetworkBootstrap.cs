using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Netcode.Transports;

#if UNITY_EDITOR
using ParrelSync;
#endif

public class SteamNetworkBootstrap : MonoBehaviour
{
    private bool initialized = false;

    [Header("Options")]
    [Tooltip("If true, automatically host if this is the original editor; clones will auto-join.")]
    [SerializeField] private bool autoHostIfOwner = true;

    public bool IsHost { get; private set; } = false;

    void Start()
    {
        StartCoroutine(InitializeAfterDelay());
    }

    private System.Collections.IEnumerator InitializeAfterDelay()
    {
        yield return new WaitForSeconds(1.0f);

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[SteamNetworkBootstrap] Steam not initialized! Ensure SteamManager prefab loads before this script.");
            yield break;
        }

        initialized = true;
        Debug.Log("[Steamworks.NET] Steam initialized for " + SteamFriends.GetPersonaName() + " (" + SteamUser.GetSteamID() + ")");

#if UNITY_EDITOR
        if (ClonesManager.IsClone())
        {
            Debug.Log("[SteamNetworkBootstrap] Clone detected - joining host...");
            JoinGame();
        }
        else
        {
            Debug.Log("[SteamNetworkBootstrap] Original detected - starting as host...");
            HostGame();
        }
#else
        Debug.Log("[SteamNetworkBootstrap] Running outside editor - use UI buttons to host or join.");
#endif
    }

    public void HostGame()
    {
        if (!initialized)
        {
            Debug.LogError("[SteamNetworkBootstrap] Steam not initialized - cannot host.");
            return;
        }

        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as SteamNetworkingSocketsTransport;
        if (transport == null)
        {
            Debug.LogError("[SteamNetworkBootstrap] SteamNetworkingSocketsTransport not found! Assign it in the NetworkManager.");
            return;
        }

        Debug.Log("[SteamNetworkBootstrap] Starting as Host...");
        IsHost = true;

        transport.ConnectToSteamID = 0;
        NetworkManager.Singleton.StartHost();
    }

    public void JoinGame()
    {
        if (!initialized)
        {
            Debug.LogError("[SteamNetworkBootstrap] Steam not initialized - cannot join.");
            return;
        }

        var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as SteamNetworkingSocketsTransport;
        if (transport == null)
        {
            Debug.LogError("[SteamNetworkBootstrap] SteamNetworkingSocketsTransport not found! Assign it in the NetworkManager.");
            return;
        }

#if UNITY_EDITOR
        StartCoroutine(WaitForArgumentAndJoin(transport));
#else
        ulong selfSteamId = SteamUser.GetSteamID().m_SteamID;
        transport.ConnectToSteamID = selfSteamId;
        Debug.Log("[SteamNetworkBootstrap] Joining host (fallback self ID) " + selfSteamId);
        NetworkManager.Singleton.StartClient();
#endif
    }

#if UNITY_EDITOR
    private System.Collections.IEnumerator WaitForArgumentAndJoin(SteamNetworkingSocketsTransport transport)
    {
        Debug.Log("[SteamNetworkBootstrap] Starting WaitForArgumentAndJoin...");

        string argument = ClonesManager.GetArgument();
        if (!string.IsNullOrEmpty(argument))
            Debug.Log("[SteamNetworkBootstrap] ClonesManager.GetArgument() returned: " + argument);
        else
            Debug.Log("[SteamNetworkBootstrap] ClonesManager.GetArgument() was empty.");

        // Ignore placeholder arguments like "client" or "clone"
        if (argument == "client" || argument == "clone")
            argument = string.Empty;

        int tries = 0;
        while (string.IsNullOrEmpty(argument) && tries < 20)
        {
            string projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            string argFile = System.IO.Path.Combine(projectPath, "args.txt");

            Debug.Log("[SteamNetworkBootstrap] Checking for args.txt at: " + argFile);

            if (System.IO.File.Exists(argFile))
            {
                try
                {
                    argument = System.IO.File.ReadAllText(argFile).Trim();
                    if (!string.IsNullOrEmpty(argument))
                    {
                        Debug.Log("[ParrelSync] Found and read args.txt: " + argument);
                        break;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[ParrelSync] Failed to read args.txt: " + e.Message);
                }
            }

            yield return new WaitForSeconds(0.1f);
            tries++;
        }

        if (!string.IsNullOrEmpty(argument) && ulong.TryParse(argument, out ulong hostSteamId))
        {
            transport.ConnectToSteamID = hostSteamId;
            Debug.Log("[SteamNetworkBootstrap] Loaded host SteamID from args.txt: " + hostSteamId);
        }
        else
        {
            ulong selfSteamId = SteamUser.GetSteamID().m_SteamID;
            transport.ConnectToSteamID = selfSteamId;
            Debug.Log("[SteamNetworkBootstrap] Could not read host ID, joining fallback self ID " + selfSteamId);
        }

        IsHost = false;
        NetworkManager.Singleton.StartClient();

        NetworkManager.Singleton.OnClientConnectedCallback += (clientId) =>
        {
            Debug.Log("[SteamNetworkBootstrap] Client connected successfully! ID: " + clientId);
        };
    }
#endif

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[SteamNetworkBootstrap] Host shut down.");
        }
    }
}
