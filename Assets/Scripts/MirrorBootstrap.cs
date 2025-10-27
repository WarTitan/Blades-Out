using UnityEngine;
using Mirror;

[DefaultExecutionOrder(-10000)] // run very early so transport is set before any HUD/UI calls
[AddComponentMenu("Networking/Mirror Bootstrap (Editor=Telepathy, Build=Fizzy)")]
public class MirrorBootstrap : MonoBehaviour
{
    public enum TransportMode { Telepathy, FizzySteamworks }

    [Header("Network Manager")]
    public NetworkManager networkManager;              // auto-resolved if null

    [Header("Transports (assign the components you use)")]
    public TelepathyTransport telepathy;               // local TCP for editor tests
    public Transport fizzy;                            // assign FizzySteamworks component here

    [Header("Defaults")]
    public TransportMode defaultMode = TransportMode.FizzySteamworks;  // builds use this
    public bool preferTelepathyInEditor = true;        // editor forces Telepathy

    [Header("Autostart (optional)")]
    public bool autoStartOnPlay = false;               // auto-start when scene loads
    public bool startAsHost = true;                    // host or client if autostart

    [Header("Direct IP (Telepathy)")]
    public string clientAddress = "127.0.0.1";
    public ushort telepathyPort = 7777;

    private TransportMode currentMode;
    private string currentTransportName = "?";

    void Awake()
    {
        if (networkManager == null) networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("[Bootstrap] No NetworkManager in scene.");
            return;
        }

        // Resolve transports if not assigned
        if (telepathy == null) telepathy = FindObjectOfType<TelepathyTransport>();
        if (fizzy == null)
        {
            var allTransports = FindObjectsOfType<Transport>(true);
            for (int i = 0; i < allTransports.Length; i++)
                if (allTransports[i] != null && allTransports[i].GetType().Name.Contains("FizzySteamworks"))
                { fizzy = allTransports[i]; break; }
        }

        // 1) Command line override > 2) Editor preference > 3) defaultMode
        TransportMode mode = GetModeFromCmdline();
        if (mode == (TransportMode)(-1))
        {
#if UNITY_EDITOR
            mode = preferTelepathyInEditor ? TransportMode.Telepathy : defaultMode;
#else
            mode = defaultMode;
#endif
        }

        ApplyTransport(mode);
    }

    void Start()
    {
        if (autoStartOnPlay)
        {
            if (startAsHost) StartHostActive();
            else StartClientActive();
        }
    }

    // ---------------------- Public helpers ----------------------

    public void UseTelepathy() { ApplyTransport(TransportMode.Telepathy); }
    public void UseFizzySteamworks() { ApplyTransport(TransportMode.FizzySteamworks); }

    public void StartHostActive()
    {
        if (!EnsureConfigured()) return;
        networkManager.StartHost();
        Debug.Log("[Bootstrap] StartHost with " + currentTransportName);
    }

    public void StartClientActive()
    {
        if (!EnsureConfigured()) return;
        if (currentMode == TransportMode.Telepathy && telepathy != null)
            networkManager.networkAddress = clientAddress;
        networkManager.StartClient();
        Debug.Log("[Bootstrap] StartClient with " + currentTransportName + " addr=" + networkManager.networkAddress);
    }

    public void StopAllNetworking()
    {
        if (NetworkServer.active) networkManager.StopHost();
        else if (NetworkClient.isConnected) networkManager.StopClient();
        Debug.Log("[Bootstrap] Stopped networking.");
    }

    // ---------------------- Internals ----------------------

    void ApplyTransport(TransportMode mode)
    {
        if (networkManager == null) return;

        Transport chosen = null;

        switch (mode)
        {
            case TransportMode.Telepathy:
                if (telepathy == null) { Debug.LogError("[Bootstrap] TelepathyTransport missing."); return; }
                telepathy.port = telepathyPort;
                chosen = telepathy;
                break;

            case TransportMode.FizzySteamworks:
                if (fizzy == null) { Debug.LogError("[Bootstrap] FizzySteamworks missing."); return; }
                chosen = fizzy;
                break;
        }

        if (chosen == null) { Debug.LogError("[Bootstrap] No transport chosen."); return; }

        networkManager.transport = chosen;
        currentMode = mode;
        currentTransportName = chosen.GetType().Name;

        EnableIfPresent(telepathy, mode == TransportMode.Telepathy);
        EnableIfPresent(fizzy as Behaviour, mode == TransportMode.FizzySteamworks);

#if UNITY_EDITOR
        Debug.Log("[Bootstrap] Active transport -> " + currentTransportName + " (Editor)");
#else
        Debug.Log("[Bootstrap] Active transport -> " + currentTransportName + " (Build)");
#endif
    }

    static void EnableIfPresent(Behaviour b, bool on)
    {
        if (b == null) return;
        if (b.enabled != on) b.enabled = on;
    }

    bool EnsureConfigured()
    {
        if (networkManager == null || networkManager.transport == null)
        {
            Debug.LogError("[Bootstrap] NetworkManager or its 'transport' is null. Call ApplyTransport(...) first.");
            return false;
        }
        var bh = networkManager.transport as Behaviour;
        if (bh == null) { Debug.LogError("[Bootstrap] NetworkManager.transport is invalid (destroyed?)."); return false; }
        if (!bh.enabled) bh.enabled = true;
        return true;
    }

    TransportMode GetModeFromCmdline()
    {
        try
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-transport", System.StringComparison.OrdinalIgnoreCase))
                {
                    string val = null;
                    if (args[i].Contains("="))
                    {
                        var split = args[i].Split('=');
                        if (split.Length == 2) val = split[1];
                    }
                    else if (i + 1 < args.Length) val = args[i + 1];

                    if (!string.IsNullOrEmpty(val))
                    {
                        val = val.Trim().ToLowerInvariant();
                        if (val == "telepathy") return TransportMode.Telepathy;
                        if (val == "fizzy") return TransportMode.FizzySteamworks;
                    }
                }
            }
        }
        catch { }
        return (TransportMode)(-1);
    }
}
