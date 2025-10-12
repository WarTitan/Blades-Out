using UnityEngine;
using Steamworks;

namespace BladesOut
{
    public class SteamInitializer : MonoBehaviour
    {
        private static bool initialized;

        public static bool Initialized
        {
            get { return initialized; }
        }

        private void Awake()
        {
            if (initialized)
                return;

            try
            {
                if (!SteamAPI.Init())
                {
                    Debug.LogError("[SteamInitializer] SteamAPI.Init() failed. Steam must be running.");
                    return;
                }

                initialized = true;
                DontDestroyOnLoad(gameObject);

                string personaName = SteamFriends.GetPersonaName();
                ulong steamId = SteamUser.GetSteamID().m_SteamID;
                Debug.Log("[SteamInitializer] Steam initialized for " + personaName + " (" + steamId + ")");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SteamInitializer] Steam initialization failed: " + e.Message);
            }
        }

        private void Update()
        {
            if (initialized)
            {
                try
                {
                    SteamAPI.RunCallbacks();
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[SteamInitializer] SteamAPI.RunCallbacks() failed: " + e.Message);
                }
            }
        }

        private void OnDisable()
        {
            if (initialized)
            {
                try
                {
                    SteamAPI.Shutdown();
                    Debug.Log("[SteamInitializer] Steam shutdown cleanly.");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[SteamInitializer] SteamAPI.Shutdown() failed: " + e.Message);
                }

                initialized = false;
            }
        }
    }
}
