// FILE: DartsAimer.cs
// Centered spinning crosshair for darts.
// The crosshair sprite stays at exact screen center.
// The AIM POINT (where darts land) = center + centerOffsetPixels + polar(radius, angle).
// GetCurrentScreenPoint() returns that precise aim point every frame.

using UnityEngine;
using UnityEngine.UI;
using Mirror;

[DefaultExecutionOrder(-5000)]
[AddComponentMenu("Minigames/Darts Aimer UI")]
public class DartsAimer : NetworkBehaviour
{
    [Header("UI Assets")]
    public Sprite redXSprite;                 // assign your PNG

    [Header("Appearance")]
    public float sizePixels = 128f;           // sprite size in screen pixels
    public float spinDegreesPerSec = 180f;    // spin speed
    public float initialAngleDeg = 0f;        // starting rotation at t=0

    [Header("Aim Circle (pixels)")]
    public float aimRadiusPixels = 64f;       // radius of the path the aim point follows
    public float aimAngleOffsetDeg = 0f;      // rotate aim point relative to sprite if needed

    [Header("Aim Center Offset (pixels)")]
    // NOTE: This shifts ONLY where the dart lands, NOT the visual crosshair.
    public Vector2 centerOffsetPixels = Vector2.zero;

    [Header("Debug")]
    public bool debugLogs = false;

    private Canvas canvas;
    private Image cross;
    private RectTransform crossRT;
    private RectTransform aimRT;

    private float startTimeUnscaled;

    void Start()
    {
        BuildUI();
        startTimeUnscaled = Time.unscaledTime;
        SetVisible(false);
    }

    void Update()
    {
        if (!isLocalPlayer) { SetVisible(false); return; }

        bool dartsActive = (TurnManagerNet.Instance != null &&
                            TurnManagerNet.Instance.phase == TurnManagerNet.Phase.Darts);
        SetVisible(dartsActive);
        if (!dartsActive) return;

        // 1) Keep the crosshair IMAGE EXACTLY at true screen center (no offset here).
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 canvasCenter;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform, screenCenter, null, out canvasCenter);
        crossRT.anchoredPosition = canvasCenter;

        // 2) Absolute rotation for deterministic sync
        float angleDeg = ComputeAngleDegNow();
        crossRT.localEulerAngles = new Vector3(0f, 0f, angleDeg);

        // 3) Place the (invisible) AimPoint child relative to the CENTERED crosshair
        //    It gets the centerOffsetPixels + polar(radius, angle+offset)
        float aimDeg = angleDeg + aimAngleOffsetDeg;
        Vector2 aimLocal = centerOffsetPixels + PolarToXY(aimRadiusPixels, aimDeg);
        aimRT.anchoredPosition = aimLocal;
    }

    // PlayerDartsShooter reads this to create the ray
    public Vector2 GetCurrentScreenPoint()
    {
        // True screen center (no offset) + aim offset (centerOffset + polar)
        float angleDeg = ComputeAngleDegNow();
        float aimDeg = angleDeg + aimAngleOffsetDeg;

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 aimLocal = centerOffsetPixels + PolarToXY(aimRadiusPixels, aimDeg);
        return center + aimLocal;
    }

    // ---- internals ----
    private float ComputeAngleDegNow()
    {
        float t = Time.unscaledTime - startTimeUnscaled;
        return initialAngleDeg + spinDegreesPerSec * t;
    }

    private static Vector2 PolarToXY(float radius, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
    }

    private void BuildUI()
    {
        if (canvas == null)
        {
            var go = new GameObject("DartsAimerCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50000;

            var scaler = go.AddComponent<CanvasScaler>();
            // ConstantPixelSize so sizes/offsets are literal pixels on any resolution
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            go.AddComponent<GraphicRaycaster>();
        }

        if (cross == null)
        {
            var go = new GameObject("DartsCrosshair");
            go.transform.SetParent(canvas.transform, false);

            cross = go.AddComponent<Image>();
            cross.raycastTarget = false;
            cross.sprite = redXSprite;
            cross.color = Color.white;

            crossRT = cross.rectTransform;
            crossRT.anchorMin = crossRT.anchorMax = new Vector2(0.5f, 0.5f);
            crossRT.pivot = new Vector2(0.5f, 0.5f);
            crossRT.sizeDelta = new Vector2(sizePixels, sizePixels);

            // Invisible child used only to visualize/debug the aim offset path (no graphic)
            var aimGO = new GameObject("AimPoint");
            aimGO.transform.SetParent(crossRT, false);
            aimRT = aimGO.AddComponent<RectTransform>();
            aimRT.anchorMin = aimRT.anchorMax = new Vector2(0.5f, 0.5f);
            aimRT.pivot = new Vector2(0.5f, 0.5f);
            aimRT.sizeDelta = Vector2.zero;
        }
    }

    private void SetVisible(bool v)
    {
        if (cross != null) cross.enabled = v;
        if (canvas != null)
        {
            if (canvas.enabled != v) canvas.enabled = v;
            if (canvas.gameObject.activeSelf != v) canvas.gameObject.SetActive(v);
        }
    }
}
