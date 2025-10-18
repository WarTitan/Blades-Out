using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class CardCanvasStabilizer : MonoBehaviour
{
    [Tooltip("Meters to push the canvas forward from the card face to avoid z-fighting.")]
    public float zOffset = 0.002f;

    [Tooltip("Disable layout components at runtime to prevent reflow when cards move.")]
    public bool disableLayoutAtRuntime = true;

    RectTransform rt;
    Vector3 baseLocalPos;
    Quaternion baseLocalRot;
    Vector3 baseLocalScale;

    void Awake()
    {
        rt = transform as RectTransform;
        baseLocalPos = rt.localPosition;
        baseLocalRot = rt.localRotation;
        baseLocalScale = rt.localScale;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        if (canvas.worldCamera == null && Camera.main != null)
            canvas.worldCamera = Camera.main;

        // Optional but recommended: put all card UI on a dedicated sorting layer
        // (Create "CardsUI" in Project Settings > Tags and Layers if it doesn't exist.)
        try { canvas.sortingLayerName = "CardsUI"; } catch { /* layer might not exist */ }
        canvas.sortingOrder = 100;

        if (disableLayoutAtRuntime)
        {
            foreach (var lg in GetComponentsInChildren<LayoutGroup>(true)) lg.enabled = false;
            foreach (var csf in GetComponentsInChildren<ContentSizeFitter>(true)) csf.enabled = false;
        }
    }

    void LateUpdate()
    {
        // Re-apply stable local transform every frame so parent motion/anim doesn't skew UI
        rt.localPosition = baseLocalPos + new Vector3(0f, 0f, -zOffset); // flip sign if your card’s forward is opposite
        rt.localRotation = baseLocalRot;
        rt.localScale = baseLocalScale;
    }
}
