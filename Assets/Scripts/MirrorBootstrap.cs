using UnityEngine;
using Mirror;
using Steamworks;
using System.Collections;

#if UNITY_EDITOR
using ParrelSync;
#endif

public class MirrorBootstrap : MonoBehaviour
{
    // Editor:
    // false = use Telepathy (localhost) so host+clone can connect with one Steam account.
    // true  = use Steam (Fizzy) - requires two different Steam accounts.
    [SerializeField] private bool useSteamInEditor = false;

    [SerializeField] private ushort telepathyPort = 7777;

    void Start()
    {
#if UNITY_EDITOR
        if (useSteamInEditor)
        {
            if (!BladesOut.SteamInitializer.Initialized)
            {
                Debug.LogError("[MirrorBootstrap] Steam not initialized but useSteamInEditor = true.");
                return;
            }

            if (ClonesManager.IsClone())
            {
                Debug.Log("[MirrorBootstrap] Editor+Steam: clone will join via Steam.");
                StartCoroutine(JoinAfterDelaySteam(0.3f));
            }
            else
            {
                Debug.Log("[MirrorBootstrap] Editor+Steam: host starting via Steam.");
                StartHostSteam();
            }
        }
        else
        {
            // Editor localhost path (no Steam). We make sure Fizzy cannot hijack.
            if (ClonesManager.IsClone())
            {
                Debug.Log("[MirrorBootstrap] Editor+Telepathy: clone connecting to 127.0.0.1:" + telepathyPort);
                StartClientTelepathy("127.0.0.1", telepathyPort);
            }
            else
            {
                Debug.Log("[MirrorBootstrap] Editor+Telepathy: host listening on 0.0.0.0:" + telepathyPort);
                StartHostTelepathy(telepathyPort);
            }
        }
#else
        // In builds: always use Steam/Fizzy
        if (!BladesOut.SteamInitializer.Initialized)
        {
            Debug.LogError("[MirrorBootstrap] Steam not initialized in build.");
            return;
        }
        StartHostSteam();
#endif
    }

    // ---------- Steam (Fizzy) ----------
    void StartHostSteam()
    {
        var fizzy = GetOnManager<Mirror.FizzySteam.FizzySteamworks>();
        var telepathy = GetOnManager<TelepathyTransport>();
        if (fizzy == null)
        {
            LogMissingTransport("FizzySteamworks");
            return;
        }

        ActivateTransport(fizzy, telepathy);

        NetworkManager.singleton.StartHost();
        var myId = SteamUser.GetSteamID().m_SteamID;
        Debug.Log("[MirrorBootstrap] Host started via Steam. My SteamID: " + myId);
    }

#if UNITY_EDITOR
    IEnumerator JoinAfterDelaySteam(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);

        var fizzy = GetOnManager<Mirror.FizzySteam.FizzySteamworks>();
        var telepathy = GetOnManager<TelepathyTransport>();
        if (fizzy == null)
        {
            LogMissingTransport("FizzySteamworks");
            yield break;
        }

        ActivateTransport(fizzy, telepathy);

        string hostId = ClonesManager.GetArgument();
        if (string.IsNullOrWhiteSpace(hostId))
        {
            // same-account will time out; fallback only
            hostId = SteamUser.GetSteamID().m_SteamID.ToString();
            Debug.LogWarning("[MirrorBootstrap] No host SteamID in ParrelSync Argument; fallback to self: " + hostId);
        }

        NetworkManager.singleton.networkAddress = hostId;
        Debug.Log("[MirrorBootstrap] Editor+Steam: connecting to host SteamID: " + hostId);
        NetworkManager.singleton.StartClient();
    }
#endif

    // ---------- Telepathy (localhost) ----------
    void StartHostTelepathy(ushort port)
    {
        var telepathy = GetOnManager<TelepathyTransport>();
        var fizzy = GetOnManager<Mirror.FizzySteam.FizzySteamworks>();
        if (telepathy == null)
        {
            LogMissingTransport("TelepathyTransport");
            return;
        }

        // make sure Fizzy cannot be selected by Mirror internally
        HardDisableFizzy(fizzy);

        ActivateTransport(telepathy); // no "toDisable" needed; Fizzy is hard-disabled
        telepathy.port = port;

        NetworkManager.singleton.networkAddress = "0.0.0.0";
        NetworkManager.singleton.StartHost();
        Debug.Log("[MirrorBootstrap] Host started via Telepathy on port " + port);
    }

    void StartClientTelepathy(string address, ushort port)
    {
        var telepathy = GetOnManager<TelepathyTransport>();
        var fizzy = GetOnManager<Mirror.FizzySteam.FizzySteamworks>();
        if (telepathy == null)
        {
            LogMissingTransport("TelepathyTransport");
            return;
        }

        // make sure Fizzy cannot be selected by Mirror internally
        HardDisableFizzy(fizzy);

        ActivateTransport(telepathy); // no "toDisable" needed; Fizzy is hard-disabled
        telepathy.port = port;

        NetworkManager.singleton.networkAddress = address;
        NetworkManager.singleton.StartClient();
        Debug.Log("[MirrorBootstrap] Client connecting via Telepathy to " + address + ":" + port);
    }

    // ---------- helpers ----------
    // Enable the chosen transport and (optionally) disable others so Mirror uses the correct one.
    void ActivateTransport(Transport toEnable, params Transport[] toDisable)
    {
        if (toDisable != null)
        {
            foreach (var t in toDisable)
                if (t != null && t.enabled) t.enabled = false;
        }

        if (toEnable != null && !toEnable.enabled) toEnable.enabled = true;

        // reflect on NetworkManager so inspector matches runtime
        NetworkManager.singleton.transport = toEnable;

        Debug.Log("[MirrorBootstrap] Transport set to: " +
            (toEnable != null ? toEnable.GetType().Name : "null"));
    }

    // In the Editor localhost path, make sure Fizzy cannot hijack active transport.
    void HardDisableFizzy(Mirror.FizzySteam.FizzySteamworks fizzy)
    {
        if (fizzy == null) return;

        // disable the component
        if (fizzy.enabled) fizzy.enabled = false;

#if UNITY_EDITOR
        // destroy the component so it can't be re-enabled during this session
        DestroyImmediate(fizzy, true);
        Debug.Log("[MirrorBootstrap] FizzySteamworks component removed for Editor localhost test.");
#endif
    }

    T GetOnManager<T>() where T : Component
    {
        // Prefer the NetworkManager's own GameObject first
        if (NetworkManager.singleton != null)
        {
            var onManager = NetworkManager.singleton.GetComponent<T>();
            if (onManager != null)
                return onManager;
        }

        // Unity 2023.1+ uses the new API
#if UNITY_2023_1_OR_NEWER
    var first = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    if (first != null)
        return first;

    var any = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    if (any != null)
        return any;
#else
        // Older Unity: mimic "include inactive" search
        var all = Resources.FindObjectsOfTypeAll<T>();
        if (all != null && all.Length > 0)
            return all[0];
#endif

        return null;
    }


    void LogMissingTransport(string transportName)
    {
        string where = (NetworkManager.singleton != null)
            ? " on object: " + NetworkManager.singleton.name
            : " (NetworkManager.singleton is null)";
        Debug.LogError("[MirrorBootstrap] " + transportName + " missing. Add it to the same GameObject as PlayerSpawnManager" + where + ".");
        if (NetworkManager.singleton != null)
        {
            var ts = NetworkManager.singleton.GetComponents<Transport>();
            if (ts != null && ts.Length > 0)
            {
                string list = "";
                foreach (var t in ts) list += t.GetType().Name + ", ";
                Debug.Log("[MirrorBootstrap] Found transports on manager: " + list);
            }
            else
            {
                Debug.Log("[MirrorBootstrap] No transports found on manager.");
            }
        }
    }
}
