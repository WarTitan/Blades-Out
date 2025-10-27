// FILE: CrosshairDot.cs
using UnityEngine;
using UnityEngine.UI;
using Mirror;

[AddComponentMenu("UI/Crosshair X (Rounded)")]
public class CrosshairDot : MonoBehaviour
{
    [Header("Arms")]
    public float lengthPx = 36f;
    public float thicknessPx = 3f;
    public float gapPx = 18f;

    [Header("Style")]
    public float alpha = 1f;
    public Color color = Color.black;
    public bool roundOuterCaps = true;

    [Header("Visibility Rules")]
    public bool hideInLobby = true;
    public bool onlyForLocalPlayer = true;

    private Canvas canvasRef;
    private Image[] rects = new Image[4];
    private Image[] caps = new Image[4];
    private static Sprite circleSprite;

    private NetworkIdentity ownerNI;
    private bool isLocalOwner = true;

    void Awake()
    {
        ownerNI = GetComponentInParent<NetworkIdentity>();
        isLocalOwner = (ownerNI == null) ? true : ownerNI.isLocalPlayer;
    }

    void Start()
    {
        BuildCanvasAndArms();
        ApplyStyle();
        LayoutArms();
        UpdateVisibility();
    }

    void OnEnable()
    {
        LobbyStage.OnLobbyStateChanged += OnLobbyStateChanged; // matches Action<bool>
        UpdateVisibility();
    }

    void OnDisable()
    {
        LobbyStage.OnLobbyStateChanged -= OnLobbyStateChanged;
        SetCanvasActive(false);
    }

    void LateUpdate()
    {
        UpdateVisibility();
    }

    // ==== Event handler: Action<bool>
    private void OnLobbyStateChanged(bool lobbyActive)
    {
        UpdateVisibility();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (rects != null && rects[0] != null)
        {
            ApplyStyle();
            LayoutArms();
        }
        UpdateVisibility();
    }
#endif

    private void BuildCanvasAndArms()
    {
        var canvasGO = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvasRef = canvasGO.GetComponent<Canvas>();
        canvasRef.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.layer = LayerMask.NameToLayer("UI");

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        for (int i = 0; i < 4; i++)
        {
            var rectGO = new GameObject("ArmRect_" + i, typeof(RectTransform), typeof(Image));
            rectGO.transform.SetParent(canvasGO.transform, false);
            var rectImg = rectGO.GetComponent<Image>();
            rectImg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            rectImg.type = Image.Type.Simple;
            rects[i] = rectImg;

            var rt = rectImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);

            var capGO = new GameObject("ArmCap_" + i, typeof(RectTransform), typeof(Image));
            capGO.transform.SetParent(canvasGO.transform, false);
            caps[i] = capGO.GetComponent<Image>();
            var crt = caps[i].rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
        }
    }

    public void SetSpread(float newGapPx)
    {
        gapPx = Mathf.Max(0f, newGapPx);
        LayoutArms();
    }

    private void ApplyStyle()
    {
        var col = new Color(color.r, color.g, color.b, alpha);
        for (int i = 0; i < 4; i++)
        {
            if (rects[i] != null) rects[i].color = col;
            if (caps[i] != null) caps[i].color = col;
        }
    }

    private void LayoutArms()
    {
        if (rects == null) return;

        float[] angles = { 45f, 135f, -135f, -45f };
        EnsureCircleSprite(Mathf.Max(1, Mathf.RoundToInt(thicknessPx)));

        for (int i = 0; i < 4; i++)
        {
            float a = angles[i];
            Vector2 dir = DirFromAngle(a);

            var r = rects[i].rectTransform;
            r.sizeDelta = new Vector2(Mathf.Max(0.1f, lengthPx), Mathf.Max(0.1f, thicknessPx));
            r.localRotation = Quaternion.Euler(0f, 0f, a);
            r.anchoredPosition = dir * (gapPx * 0.5f);

            var c = caps[i];
            if (c != null)
            {
                c.enabled = roundOuterCaps;
                if (roundOuterCaps)
                {
                    c.sprite = circleSprite;
                    c.type = Image.Type.Simple;
                    var crt = c.rectTransform;
                    crt.sizeDelta = new Vector2(thicknessPx, thicknessPx);
                    crt.localRotation = Quaternion.identity;
                    crt.anchoredPosition = dir * (gapPx * 0.5f + lengthPx);
                }
            }
        }
    }

    private static Vector2 DirFromAngle(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    private static void EnsureCircleSprite(int diameter)
    {
        if (circleSprite != null && Mathf.Abs(circleSprite.rect.width - diameter) < 0.5f) return;

        var tex = new Texture2D(diameter, diameter, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float r = (diameter - 1) * 0.5f;
        float cx = r, cy = r;

        for (int y = 0; y < diameter; y++)
        {
            for (int x = 0; x < diameter; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d2 = dx * dx + dy * dy;
                float inside = d2 <= r * r ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside));
            }
        }
        tex.Apply(false, false);
        circleSprite = Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 1f);
    }

    private void UpdateVisibility()
    {
        if (onlyForLocalPlayer)
        {
            if (ownerNI == null) ownerNI = GetComponentInParent<NetworkIdentity>();
            isLocalOwner = (ownerNI == null) ? true : ownerNI.isLocalPlayer;
        }

        if (!isLocalOwner)
        {
            SetCanvasActive(false);
            return;
        }

        bool inLobby = (LobbyStage.Instance != null) ? LobbyStage.Instance.lobbyActive : false;
        bool shouldShow = !(hideInLobby && inLobby);
        SetCanvasActive(shouldShow);
    }

    private void SetCanvasActive(bool active)
    {
        if (canvasRef != null && canvasRef.gameObject.activeSelf != active)
            canvasRef.gameObject.SetActive(active);
    }
}
