using UnityEngine;
using Mirror;
using Steamworks;
#if UNITY_EDITOR
using ParrelSync;
#endif

public class MirrorBootstrap : MonoBehaviour
{
    [SerializeField] bool autoStartInEditor = true;

    void Start()
    {
        if (!BladesOut.SteamInitializer.Initialized)
        {
            Debug.LogError("[MirrorBootstrap] Steam not initialized. Put SteamInitializer in the first scene.");
            return;
        }

#if UNITY_EDITOR
        if (!autoStartInEditor) return;

        if (ClonesManager.IsClone())
        {
            Debug.Log("[MirrorBootstrap] Clone detected - joining host...");
            JoinAsClient();
        }
        else
        {
            Debug.Log("[MirrorBootstrap] Original detected - starting as host...");
            StartAsHost();
        }
#else
        StartAsHost();
#endif
    }

    void StartAsHost()
    {
        var transport = NetworkManager.singleton.transport as Mirror.FizzySteam.FizzySteamworks;
        if (transport == null)
        {
            Debug.LogError("[MirrorBootstrap] FizzySteamworks transport not found on NetworkManager.");
            return;
        }

        NetworkManager.singleton.StartHost();
        Debug.Log("[MirrorBootstrap] Host started. My SteamID: " + SteamUser.GetSteamID().m_SteamID);
    }

    void JoinAsClient()
    {
        var transport = NetworkManager.singleton.transport as Mirror.FizzySteam.FizzySteamworks;
        if (transport == null)
        {
            Debug.LogError("[MirrorBootstrap] FizzySteamworks transport not found on NetworkManager.");
            return;
        }

        string hostSteamId = null;

#if UNITY_EDITOR
        string arg = ClonesManager.GetArgument(); // set this to host's SteamID in ParrelSync
        if (!string.IsNullOrWhiteSpace(arg))
            hostSteamId = arg.Trim();
#endif

        if (!IsAllDigits(hostSteamId))
        {
            Debug.LogWarning("[MirrorBootstrap] No valid host SteamID via ParrelSync Argument. Falling back to self.");
            hostSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
        }

        NetworkManager.singleton.networkAddress = hostSteamId;
        Debug.Log("[MirrorBootstrap] Connecting to host SteamID: " + hostSteamId);
        NetworkManager.singleton.StartClient();
    }

    static bool IsAllDigits(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < s.Length; i++)
            if (s[i] < '0' || s[i] > '9') return false;
        return true;
    }
}
