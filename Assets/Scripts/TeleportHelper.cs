// FILE: TeleportHelper.cs
// Owner-side snap + gameplay handoff after teleport.
// Darts HUD uses a Unity-6000 safe built-in font (LegacyRuntime.ttf) with fallbacks.

using UnityEngine;
using UnityEngine.UI;
using Mirror;
using System.Collections;

[AddComponentMenu("Net/Teleport Helper")]
public class TeleportHelper : NetworkBehaviour
{
    [Header("Cursor lock enforcement after teleport (seconds)")]
    public float cursorEnforceSeconds = 1.0f;

    [Header("Cursor lock polling interval (seconds)")]
    public float cursorEnforceInterval = 0.05f;

    // --- Darts HUD (client-only, ephemeral) ---
    private static GameObject s_dartsHud;

    // -------- Font helper (handles Unity 6000+) --------
    private static Font GetDefaultUIFont()
    {
        // Newer Unity: Arial is no longer a built-in resource; use LegacyRuntime.ttf.
        Font f = null;

        // Try LegacyRuntime.ttf first (Unity 6000+)
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }

        // Fallback for older versions (if still present)
        if (f == null)
        {
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }

        // OS font fallback (works across versions)
        if (f == null)
        {
            try { f = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
        }

        // Still null? Last resort: create any dynamic (may still be null on some platforms)
        if (f == null)
        {
            Debug.LogWarning("[TeleportHelper] Could not load a built-in UI font. Using OS dynamic font fallback if available.");
        }

        return f;
    }

    [TargetRpc]
    public void Target_DisableLobbyCameraLocal(NetworkConnectionToClient conn)
    {
        var ls = LobbyStage.Instance;
        if (ls != null && ls.lobbyCamera != null)
        {
            var cam = ls.lobbyCamera.GetComponent<Camera>();
            if (cam != null) cam.enabled = false;
            var al = ls.lobbyCamera.GetComponent<AudioListener>();
            if (al != null) al.enabled = false;
        }
    }

    [TargetRpc]
    public void Target_ShowDartsHud(NetworkConnectionToClient conn, string label)
    {
        if (s_dartsHud != null) return;

        var root = new GameObject("DartsHUD");
        s_dartsHud = root;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        var bgGO = new GameObject("BG");
        bgGO.transform.SetParent(root.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.25f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(root.transform, false);
        var txt = textGO.AddComponent<Text>();
        txt.alignment = TextAnchor.UpperCenter;
        txt.font = GetDefaultUIFont();
        txt.fontSize = 36;
        txt.text = string.IsNullOrEmpty(label) ? "PLAYING DARTS" : label;
        var rt = txt.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(800f, 60f);
    }

    [TargetRpc]
    public void Target_HideDartsHud(NetworkConnectionToClient conn)
    {
        if (s_dartsHud != null)
        {
            GameObject.Destroy(s_dartsHud);
            s_dartsHud = null;
        }
    }

    [TargetRpc]
    public void TargetSnapAndEnterGameplay(NetworkConnectionToClient conn, Vector3 position, Quaternion rotation)
    {
        // 1) Snap transform safely
        var cc = GetComponent<CharacterController>();
        bool hadCC = (cc != null && cc.enabled);
        if (hadCC) cc.enabled = false;

        ActivateAncestorsAndSelf(transform);
        transform.SetPositionAndRotation(position, rotation);

        if (hadCC) cc.enabled = true;

        // 2) Make sure lobby camera is off
        Target_DisableLobbyCameraLocal(conn);

        // 3) Ensure local gameplay camera + look are ON
        var lca = GetComponent<LocalCameraActivator>();
        if (lca != null)
        {
            lca.SendMessage("ForceEnterGameplay", SendMessageOptions.DontRequireReceiver);
            if (lca.playerCamera == null)
                lca.playerCamera = lca.GetComponentInChildren<Camera>(true);
            if (lca.playerCamera != null)
                lca.playerCamera.enabled = true;
        }

        var lld = GetComponent<LobbyLocalDisabler>();
        if (lld != null)
            lld.SendMessage("ForceEnableGameplay", SendMessageOptions.DontRequireReceiver);

        var lcc = GetComponent<LocalCameraController>();
        if (lcc == null) lcc = GetComponentInChildren<LocalCameraController>(true);
        if (lcc != null)
        {
            lcc.enabled = true;
            if (lcc.playerCamera != null)
                lcc.playerCamera.enabled = true;
        }

        // 4) Lock cursor for mouse look briefly
        StartCoroutine(EnforceLockedCursor(cursorEnforceSeconds, cursorEnforceInterval));

        // 5) Optional: re-apply gameplay policies (cursor/audio)
        var mgr = GameObject.FindObjectOfType<CursorAndAudioManager>();
        if (mgr != null)
            mgr.gameObject.SendMessage("ApplyGameplayPolicy", SendMessageOptions.DontRequireReceiver);
    }

    private IEnumerator EnforceLockedCursor(float seconds, float interval)
    {
        float until = Time.unscaledTime + Mathf.Max(0.25f, seconds);
        float step = Mathf.Max(0.02f, interval);

        while (Time.unscaledTime < until)
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
            yield return new WaitForSecondsRealtime(step);
        }
    }

    private static void ActivateAncestorsAndSelf(Transform leaf)
    {
        if (leaf == null) return;
        System.Collections.Generic.List<Transform> chain = new System.Collections.Generic.List<Transform>();
        Transform t = leaf;
        while (t != null) { chain.Add(t); t = t.parent; }
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            if (!chain[i].gameObject.activeSelf)
                chain[i].gameObject.SetActive(true);
        }
    }
}
