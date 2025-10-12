using UnityEngine;
using Steamworks;

public class SteamManager : MonoBehaviour
{
    private static SteamManager _instance;
    private bool initialized = false;

    public static bool Initialized => _instance != null && _instance.initialized;

    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            if (!Packsize.Test())
                Debug.LogError("[Steamworks.NET] Packsize Test failed!");
            if (!DllCheck.Test())
                Debug.LogError("[Steamworks.NET] DllCheck Test failed!");

            SteamAPI.Init();
            Debug.Log($"[Steamworks.NET] Steam initialized as {SteamFriends.GetPersonaName()}");
            initialized = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Steamworks.NET] Steam initialization failed: " + e.Message);
        }
    }

    public static void RunCallbacks() => SteamAPI.RunCallbacks();

    private void OnEnable() => SteamAPI.RunCallbacks();

    private void OnDestroy()
    {
        if (_instance == this && initialized)
        {
            SteamAPI.Shutdown();
            _instance = null;
        }
    }
}
