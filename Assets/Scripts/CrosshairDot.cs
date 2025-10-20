using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Crosshair X (Rounded)")]
public class CrosshairDot : MonoBehaviour
{
    [Header("Arms")]
    // 5x smaller than before: 180 -> 36, 12 -> 3, 90 -> 18
    public float lengthPx = 36f;
    public float thicknessPx = 3f;
    public float gapPx = 18f;

    [Header("Style")]
    public float alpha = 1f;
    public Color color = Color.black;
    public bool hideCursor = true;
    public bool roundOuterCaps = true;   // rounded only at the far end of each arm

    private Canvas _canvas;
    private Image[] _rects = new Image[4];
    private Image[] _caps = new Image[4];
    private static Sprite _circleSprite;

    void Start()
    {
        // Canvas
        GameObject go = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.layer = LayerMask.NameToLayer("UI");

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Create 4 arms (rect + cap)
        for (int i = 0; i < 4; i++)
        {
            // rectangle part
            var rectGO = new GameObject("ArmRect_" + i, typeof(RectTransform), typeof(Image));
            rectGO.transform.SetParent(go.transform, false);
            var rectImg = rectGO.GetComponent<Image>();
            rectImg.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
            rectImg.type = Image.Type.Simple;
            _rects[i] = rectImg;

            var rt = rectImg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f); // inner edge near the gap

            // cap part (circle)
            var capGO = new GameObject("ArmCap_" + i, typeof(RectTransform), typeof(Image));
            capGO.transform.SetParent(go.transform, false);
            var capImg = capGO.GetComponent<Image>();
            _caps[i] = capImg;

            var crt = capImg.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
        }

        ApplyStyle();
        LayoutArms();

        if (hideCursor) Cursor.visible = false;
    }

    // Optional runtime spread change
    public void SetSpread(float newGapPx)
    {
        gapPx = Mathf.Max(0f, newGapPx);
        LayoutArms();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_rects != null && _rects[0] != null)
        {
            ApplyStyle();
            LayoutArms();
        }
    }
#endif

    private void ApplyStyle()
    {
        var col = new Color(color.r, color.g, color.b, alpha);
        for (int i = 0; i < 4; i++)
        {
            if (_rects[i] != null) _rects[i].color = col;
            if (_caps[i] != null) _caps[i].color = col;
        }
    }

    private void LayoutArms()
    {
        if (_rects == null) return;

        float[] angles = { 45f, 135f, -135f, -45f };

        // ensure we have a circle sprite sized for caps
        EnsureCircleSprite(Mathf.Max(1, Mathf.RoundToInt(thicknessPx)));

        for (int i = 0; i < 4; i++)
        {
            float a = angles[i];
            Vector2 dir = DirFromAngle(a);

            // rectangle
            var r = _rects[i].rectTransform;
            r.sizeDelta = new Vector2(Mathf.Max(0.1f, lengthPx), Mathf.Max(0.1f, thicknessPx));
            r.localRotation = Quaternion.Euler(0f, 0f, a);
            r.anchoredPosition = dir * (gapPx * 0.5f);

            // cap
            var c = _caps[i];
            c.enabled = roundOuterCaps;
            if (roundOuterCaps)
            {
                c.sprite = _circleSprite;
                c.type = Image.Type.Simple;
                var crt = c.rectTransform;
                crt.sizeDelta = new Vector2(thicknessPx, thicknessPx);
                crt.localRotation = Quaternion.identity;
                crt.anchoredPosition = dir * (gapPx * 0.5f + lengthPx);
            }
        }
    }

    private static Vector2 DirFromAngle(float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;
    }

    // Creates a white circle sprite of given diameter (in pixels)
    private static void EnsureCircleSprite(int diameter)
    {
        // reuse if already created with this size
        if (_circleSprite != null && Mathf.Abs(_circleSprite.rect.width - diameter) < 0.5f) return;

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
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, diameter, diameter), new Vector2(0.5f, 0.5f), 1f);
    }
}
