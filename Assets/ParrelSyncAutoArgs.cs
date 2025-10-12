#if UNITY_EDITOR
using UnityEditor;
using ParrelSync;
using Steamworks;
using System.IO;
using UnityEngine;

[InitializeOnLoad]
public static class ParrelSyncAutoArgs
{
    private static bool argumentSet = false;

    static ParrelSyncAutoArgs()
    {
        // Keep checking periodically until Steam is ready
        EditorApplication.update += CheckSteamInitialization;
    }

    private static void CheckSteamInitialization()
    {
        if (argumentSet)
            return;

        if (ClonesManager.IsClone())
        {
            EditorApplication.update -= CheckSteamInitialization;
            return;
        }

        if (!SteamManager.Initialized)
            return;

        // Steam is initialized, safe to write argument
        string hostSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
        UpdateCloneArguments(hostSteamId);
        Debug.Log("[ParrelSync] Argument file updated for clones: " + hostSteamId);

        argumentSet = true;
        EditorApplication.update -= CheckSteamInitialization;
    }

    private static void UpdateCloneArguments(string argument)
    {
        // Find clone folders next to main project
        string projectParent = Directory.GetParent(Application.dataPath).FullName;
        string projectRootName = Path.GetFileName(projectParent);
        string parentFolder = Path.GetDirectoryName(projectParent);
        if (string.IsNullOrEmpty(parentFolder))
        {
            Debug.LogWarning("[ParrelSync] Could not determine parent folder of project.");
            return;
        }

        // Look for sibling folders named "<ProjectName>_clone*"
        string[] siblingDirs = Directory.GetDirectories(parentFolder, projectRootName + "_clone*");
        if (siblingDirs.Length == 0)
        {
            Debug.LogWarning("[ParrelSync] No clone folders found near: " + parentFolder);
            return;
        }

        foreach (string folder in siblingDirs)
        {
            string argFile = Path.Combine(folder, "args.txt");
            try
            {
                File.WriteAllText(argFile, argument);
                Debug.Log("[ParrelSync] Wrote argument to: " + argFile);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[ParrelSync] Failed to write argument: " + e.Message);
            }
        }
    }
}
#endif
