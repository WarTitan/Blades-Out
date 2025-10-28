// FILE: PsychoactiveHUDAutoBind.cs
// New helper to bind the HUD to your scene UI at runtime.
// Add this to the player prefab (same object as PsychoactiveHUD).

using UnityEngine;
using Mirror;

[AddComponentMenu("Gameplay/Psychoactive HUD AutoBind")]
public class PsychoactiveHUDAutoBind : NetworkBehaviour
{
    public string listRootObjectName = "EffectsPanel"; // fallback by name
    public string listRootTag = ""; // optional: set to your EffectsPanel tag, e.g. "UIEffectsPanel"
    public PsychoactiveHUD hud;
    public GameObject rowPrefab;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Bind();
    }

    void OnEnable()
    {
        if (isLocalPlayer) Bind();
    }

    private void Bind()
    {
        if (hud == null) hud = GetComponent<PsychoactiveHUD>();
        if (hud == null) return;

        if (hud.listRoot == null)
        {
            RectTransform target = null;

            if (!string.IsNullOrEmpty(listRootTag))
            {
                var byTag = GameObject.FindWithTag(listRootTag);
                if (byTag != null) target = byTag.GetComponent<RectTransform>();
            }

            if (target == null && !string.IsNullOrEmpty(listRootObjectName))
            {
                var byName = GameObject.Find(listRootObjectName);
                if (byName != null) target = byName.GetComponent<RectTransform>();
            }

            if (target != null)
                hud.listRoot = target;
        }

        if (hud.rowPrefab == null && rowPrefab != null)
            hud.rowPrefab = rowPrefab;
    }
}
