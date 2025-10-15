using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Crosshair Dot (Auto)")]
public class CrosshairDot : MonoBehaviour
{
    [Header("Dot")]
    public float sizePx = 6f;
    public float alpha = 0.9f;
    public Color color = Color.white;

    [Header("Optional")]
    public bool hideCursor = true;

    Canvas canvas;
    Image dot;

    void Start()
    {
        // Canvas
        GameObject go = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.layer = LayerMask.NameToLayer("UI");

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Dot
        var dotGO = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dotGO.transform.SetParent(go.transform, false);
        dot = dotGO.GetComponent<Image>();
        dot.color = new Color(color.r, color.g, color.b, alpha);

        var rt = dot.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(sizePx, sizePx);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Round shape
        dot.sprite = UnityEngine.Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        dot.type = Image.Type.Simple;
        dot.pixelsPerUnitMultiplier = 1f;

        if (hideCursor) Cursor.visible = false;
    }
}
