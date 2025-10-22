using UnityEngine;
using TMPro;
using Mirror;

[AddComponentMenu("UI/World Nameplate (TextMeshPro)")]
public class WorldNameplate : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;                                  // who we follow
    public Vector3 offset = new Vector3(0f, 2.2f, 0f);        // world offset above target

    [Header("Look")]
    public bool billboard = true;
    public float billboardLerp = 20f;

    [Header("Text Source")]
    public bool useCustomText = true;
    [TextArea] public string customText = "Click me to get a random level up";
    public bool uppercase = false;

    [Header("TextMeshPro")]
    public TMP_FontAsset fontAsset;
    public float fontSize = 1.2f;
    public Color color = Color.white;
    public bool useOutline = true;
    public Color outlineColor = new Color(0, 0, 0, 0.85f);
    [Range(0f, 1f)] public float outlineWidth = 0.28f;
    public TextWrappingModes wrapping = TextWrappingModes.NoWrap;

    // Internals
    private Transform holder;          // this is what we actually move/rotate
    private TextMeshPro tmp;
    private bool autoCreatedHolder = false;

    void Awake()
    {
        // Default target: parent (recommended to add this component on a child)
        if (target == null)
            target = transform.parent != null ? transform.parent : transform;

        // If the component is on the SAME object as the target, create a child "holder"
        // so we move the child only (and never move the dealer root).
        if (target == transform)
        {
            var child = new GameObject("NameplateHolder");
            child.transform.SetParent(transform, false);
            holder = child.transform;
            autoCreatedHolder = true;
        }
        else
        {
            // Component is on a child already: we can use self as holder
            holder = transform;
        }

        // Ensure TMP exists under the holder
        tmp = GetComponentInChildren<TextMeshPro>();
        if (tmp == null || (holder != null && tmp.transform.parent != holder))
        {
            // If TMP exists under wrong parent, move it. Otherwise create new under holder.
            if (tmp != null && tmp.transform != holder)
            {
                tmp.transform.SetParent(holder, false);
            }
            else
            {
                var go = new GameObject("NameTextTMP");
                go.transform.SetParent(holder, false);
                tmp = go.AddComponent<TextMeshPro>();
            }
        }

        // Setup TMP
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = wrapping;
        if (fontAsset) tmp.font = fontAsset;
        tmp.outlineColor = outlineColor;
        tmp.outlineWidth = useOutline ? outlineWidth : 0f;

        var mr = tmp.GetComponent<MeshRenderer>();
        if (mr)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        ApplyText(); // initialize once
    }

    void LateUpdate()
    {
        if (target == null || holder == null) return;

        // Position: ALWAYS compute from target (never from holder) so there is no drift.
        holder.position = target.position + offset;

        // Update text if needed
        ApplyText();

        // Billboard: rotate the HOLDER only
        if (billboard)
        {
            Camera cam = GetViewerCamera();
            if (cam != null)
            {
                Vector3 dir = (holder.position - cam.transform.position).normalized;
                Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
                holder.rotation = Quaternion.Slerp(
                    holder.rotation,
                    look,
                    1f - Mathf.Exp(-billboardLerp * Time.deltaTime)
                );
            }
        }
    }

    void ApplyText()
    {
        if (tmp == null) return;

        if (useCustomText)
        {
            string desired = uppercase ? customText.ToUpperInvariant() : customText;
            if (tmp.text != desired) tmp.text = desired;
        }
        else
        {
            var pn = target ? target.GetComponent<PlayerNameNet>() : null;
            if (pn != null && pn.displayName != null)
            {
                string desired = uppercase ? pn.displayName.ToUpperInvariant() : pn.displayName;
                if (tmp.text != desired) tmp.text = desired;
            }
        }

        // Apply style every frame so inspector tweaks during play stick
        if (fontAsset && tmp.font != fontAsset) tmp.font = fontAsset;
        if (!Mathf.Approximately(tmp.fontSize, fontSize)) tmp.fontSize = fontSize;
        if (tmp.color != color) tmp.color = color;
        tmp.textWrappingMode = wrapping;
        tmp.outlineColor = outlineColor;
        tmp.outlineWidth = useOutline ? outlineWidth : 0f;
    }

    Camera GetViewerCamera()
    {
        if (LobbyStage.Instance && LobbyStage.Instance.lobbyActive && LobbyStage.Instance.lobbyCamera)
            return LobbyStage.Instance.lobbyCamera;

        if (NetworkClient.active && NetworkClient.localPlayer != null)
        {
            var lca = NetworkClient.localPlayer.GetComponent<LocalCameraActivator>();
            if (lca && lca.playerCamera && lca.playerCamera.enabled)
                return lca.playerCamera;
        }

        return Camera.main;
    }

#if UNITY_EDITOR
    // Draw gizmo where the holder will be placed relative to the target
    void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(target.position + offset, 0.05f);
    }
#endif
}
