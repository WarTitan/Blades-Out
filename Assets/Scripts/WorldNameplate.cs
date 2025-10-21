using UnityEngine;
using TMPro;
using Mirror;

[AddComponentMenu("UI/World Nameplate (TextMeshPro)")]
public class WorldNameplate : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 2.2f, 0f);

    [Header("Look")]
    public bool billboard = true;
    public float billboardLerp = 20f;

    [Header("TextMeshPro")]
    public TMP_FontAsset fontAsset;        // drag your TMP font asset here
    public float fontSize = 1.2f;          // world-space size
    public Color color = Color.white;
    public bool useOutline = true;
    public Color outlineColor = new Color(0, 0, 0, 0.85f);
    public float outlineWidth = 0.28f;     // 0..1 (SDF outline)
    public TextWrappingModes wrapping = TextWrappingModes.NoWrap;

    private TextMeshPro tmp;

    void Awake()
    {
        if (!target) target = transform.parent != null ? transform.parent : transform;

        tmp = GetComponentInChildren<TextMeshPro>();
        if (!tmp)
        {
            var go = new GameObject("NameTextTMP");
            go.transform.SetParent(transform, false);
            tmp = go.AddComponent<TextMeshPro>();
        }

        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = wrapping;

        if (fontAsset) tmp.font = fontAsset;

        tmp.outlineColor = outlineColor;
        tmp.outlineWidth = useOutline ? outlineWidth : 0f;

        var mr = tmp.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    void LateUpdate()
    {
        if (!target) return;

        // position above head
        transform.position = target.position + offset;

        // text from PlayerNameNet
        var pn = target.GetComponent<PlayerNameNet>();
        if (pn != null && pn.displayName != null && tmp.text != pn.displayName)
            tmp.text = pn.displayName;

        if (billboard)
        {
            Camera cam = GetViewerCamera();
            if (cam != null)
            {
                Vector3 dir = (transform.position - cam.transform.position).normalized;
                Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-billboardLerp * Time.deltaTime));
            }
        }
    }

    Camera GetViewerCamera()
    {
        // 1) Lobby camera while lobby is active
        if (LobbyStage.Instance && LobbyStage.Instance.lobbyActive && LobbyStage.Instance.lobbyCamera)
            return LobbyStage.Instance.lobbyCamera;

        // 2) Local player's gameplay camera (best choice in game)
        if (NetworkClient.active && NetworkClient.localPlayer != null)
        {
            var lca = NetworkClient.localPlayer.GetComponent<LocalCameraActivator>();
            if (lca && lca.playerCamera && lca.playerCamera.enabled)
                return lca.playerCamera;
        }

        // 3) Fallback
        return Camera.main;
    }
}
