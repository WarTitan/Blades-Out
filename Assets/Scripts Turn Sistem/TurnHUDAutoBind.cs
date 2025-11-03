// FILE: TurnHUDAutoBind.cs
// NEW FILE (ASCII only)
// Helper to bind TurnHUD to your existing UI by name or tag.
// Put this on any always-present object in the scene (e.g., a UI bootstrap).

using UnityEngine;

[AddComponentMenu("Gameplay/Turn HUD AutoBind")]
public class TurnHUDAutoBind : MonoBehaviour
{
    public TurnHUD hud;

    [Header("Find by Name or Tag")]
    public string titleObjectName = "TurnTitle";
    public string timerObjectName = "TurnTimer";
    public string titleObjectTag = "";
    public string timerObjectTag = "";

    void Start()
    {
#pragma warning disable CS0618
        if (hud == null) {} hud = FindObjectOfType<TurnHUD>();
#pragma warning disable CS0618
        if (hud == null) return;

        if (hud.titleObject == null)
        {
            if (!string.IsNullOrEmpty(titleObjectTag))
            {
                var byTag = GameObject.FindWithTag(titleObjectTag);
                if (byTag != null) hud.titleObject = byTag;
            }
            if (hud.titleObject == null && !string.IsNullOrEmpty(titleObjectName))
            {
                var byName = GameObject.Find(titleObjectName);
                if (byName != null) hud.titleObject = byName;
            }
        }

        if (hud.timerObject == null)
        {
            if (!string.IsNullOrEmpty(timerObjectTag))
            {
                var byTag = GameObject.FindWithTag(timerObjectTag);
                if (byTag != null) hud.timerObject = byTag;
            }
            if (hud.timerObject == null && !string.IsNullOrEmpty(timerObjectName))
            {
                var byName = GameObject.Find(timerObjectName);
                if (byName != null) hud.timerObject = byName;
            }
        }
    }
}
