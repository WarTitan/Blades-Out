// FILE: PsychoactiveHUD.cs
// FULL REPLACEMENT
// Keeps first row fixed at TOP-RIGHT, appends new rows UNDER it,
// and prevents parent layouts from shifting the panel.
// Compatible with PsychoactiveHUDRow binder (supports Text or TMP).

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror;
using TMPro;

[AddComponentMenu("Gameplay/Psychoactive HUD")]
public class PsychoactiveHUD : NetworkBehaviour
{
    [Header("Existing UI Hookup")]
    public RectTransform listRoot;   // Your panel (should be under a Canvas)
    public GameObject rowPrefab;     // Prefab containing PsychoactiveHUDRow

    [Header("Top-Right Overlay Settings")]
    public bool forceTopRightAnchor = true;
    public Vector2 topRightOffset = new Vector2(-12f, -12f);

    [Header("Protect From Parent Layouts")]
    [Tooltip("If true, reparent listRoot directly under the root Canvas to avoid parent layout shifting.")]
    public bool detachToRootCanvas = true;

    [Tooltip("Always add LayoutElement(ignoreLayout=true) so parents cannot reposition this panel.")]
    public bool addIgnoreLayout = true;

    [Header("Internal Layout On listRoot")]
    [Tooltip("If listRoot has VerticalLayoutGroup, enforce top->down and right alignment.")]
    public bool enforceVerticalLayout = true;

    [Tooltip("If listRoot has GridLayoutGroup, enforce UpperRight + Vertical fill.")]
    public bool enforceGridLayout = true;

    [Header("Behavior")]
    [Tooltip("Append new rows at the bottom so row #1 never moves.")]
    public bool appendNewAtBottom = true;

    [Header("Update")]
    public float refreshInterval = 0.1f;

    [Header("Fallback Overlay (used only if listRoot or rowPrefab is missing)")]
    public Vector2 fallbackTopRightOffset = new Vector2(-12f, -12f);
    public int fallbackFontSize = 16;

    private PsychoactiveEffectsManager mgr;

    private class Row
    {
        public PsychoactiveEffectBase effect;
        public float endTime;
        public float duration;
        public GameObject go;
        public PsychoactiveHUDRow binder;
    }

    private readonly Dictionary<PsychoactiveEffectBase, Row> rows = new Dictionary<PsychoactiveEffectBase, Row>();

    // Fallback UI
    private Canvas fallbackCanvas;
    private RectTransform fallbackPanel;
    private GameObject fallbackRowTemplate; // created once if needed

    private float nextRefresh;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Initialize();
    }

    private void OnEnable()
    {
        if (isLocalPlayer) Initialize();
    }

    private void OnDisable()
    {
        Unhook();
        ClearAllRows();
        DestroyFallbackIfAny();
    }

    private void Initialize()
    {
        if (!isLocalPlayer) return;

        if (mgr == null) mgr = GetComponent<PsychoactiveEffectsManager>();
        if (mgr == null)
        {
            Debug.LogWarning("[PsychoactiveHUD] PsychoactiveEffectsManager not found on player.");
            return;
        }

        if (listRoot == null || rowPrefab == null)
        {
            EnsureFallbackOverlay();
        }
        else
        {
            // Detach from layout parents to stop them from moving our panel
            if (detachToRootCanvas) DetachToRootCanvas(listRoot);

            // Make sure parent layouts ignore this panel
            if (addIgnoreLayout)
            {
                var le = listRoot.GetComponent<LayoutElement>();
                if (le == null) le = listRoot.gameObject.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
            }

            // Anchor/pivot to top-right so height growth goes downward
            if (forceTopRightAnchor)
            {
                listRoot.anchorMin = new Vector2(1f, 1f);
                listRoot.anchorMax = new Vector2(1f, 1f);
                listRoot.pivot = new Vector2(1f, 1f);
                listRoot.anchoredPosition = topRightOffset;
            }

            // Enforce the internal layout rules
            EnforceInternalLayout(listRoot);
        }

        Hook();

        // Populate already-active effects
        var snap = mgr.GetActiveEffectsSnapshot();
        for (int i = 0; i < snap.Count; i++)
        {
            var s = snap[i];
            CreateOrUpdateRow(s.effect, s.name, s.endTime, s.duration);
        }
    }

    private void Hook()
    {
        if (mgr == null) return;
        mgr.OnEffectStarted += OnEffectStarted;
        mgr.OnEffectEnded += OnEffectEnded;
    }

    private void Unhook()
    {
        if (mgr == null) return;
        mgr.OnEffectStarted -= OnEffectStarted;
        mgr.OnEffectEnded -= OnEffectEnded;
    }

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);

        if (rows.Count == 0) return;

        var toRemove = ListPool<PsychoactiveEffectBase>.Get();
        foreach (var kv in rows)
        {
            var r = kv.Value;
            float remaining = Mathf.Max(0f, r.endTime - Time.time);
            if (remaining <= 0f)
            {
                toRemove.Add(kv.Key);
                continue;
            }
            if (r.binder != null) r.binder.SetTime(FormatTime(remaining));
        }

        for (int i = 0; i < toRemove.Count; i++) RemoveRow(toRemove[i]);
        ListPool<PsychoactiveEffectBase>.Release(toRemove);
    }

    // Events from manager
    private void OnEffectStarted(string name, PsychoactiveEffectBase eff, float endTime, float duration)
    {
        CreateOrUpdateRow(eff, name, endTime, duration);
    }

    private void OnEffectEnded(PsychoactiveEffectBase eff)
    {
        if (eff == null)
        {
            ClearAllRows();
            return;
        }
        RemoveRow(eff);
    }

    // Row management
    private void CreateOrUpdateRow(PsychoactiveEffectBase eff, string name, float endTime, float duration)
    {
        if (eff == null) return;

        Row row;
        if (!rows.TryGetValue(eff, out row))
        {
            Transform parent = (listRoot != null) ? (Transform)listRoot : (Transform)fallbackPanel;
            if (parent == null)
            {
                Debug.LogWarning("[PsychoactiveHUD] No UI container available.");
                return;
            }

            // Ensure internal layout is correct each time
            EnforceInternalLayout((RectTransform)parent);

            GameObject prefab = (rowPrefab != null) ? rowPrefab : CreateFallbackRowPrefabOnce();
            var go = Instantiate(prefab, parent, false);

            // Append at bottom so row #1 stays fixed
            if (appendNewAtBottom)
            {
                go.transform.SetAsLastSibling();
            }
            else
            {
                go.transform.SetAsFirstSibling();
            }

            // Force immediate layout so positions update this frame
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)parent);

            row = new Row
            {
                effect = eff,
                endTime = endTime,
                duration = duration,
                go = go,
                binder = go.GetComponent<PsychoactiveHUDRow>()
            };

            if (row.binder == null)
            {
                row.binder = go.AddComponent<PsychoactiveHUDRow>();
                AutoBindTextsForBinder(row.binder, go);
            }

            rows.Add(eff, row);
        }
        else
        {
            row.endTime = endTime;
            row.duration = duration;
        }

        if (row.binder != null)
        {
            row.binder.SetName(name);
            float remainingNow = Mathf.Max(0f, endTime - Time.time);
            row.binder.SetTime(FormatTime(remainingNow));
        }
    }

    private void RemoveRow(PsychoactiveEffectBase eff)
    {
        Row row;
        if (!rows.TryGetValue(eff, out row)) return;

        if (row.go != null) Destroy(row.go);
        rows.Remove(eff);
    }

    private void ClearAllRows()
    {
        foreach (var kv in rows)
        {
            if (kv.Value.go != null) Destroy(kv.Value.go);
        }
        rows.Clear();
    }

    // --- Helpers ---

    private void DetachToRootCanvas(RectTransform rt)
    {
        if (rt == null) return;

        // If any parent has a LayoutGroup or ContentSizeFitter, they can shift our panel.
        if (!HasLayoutInParents(rt)) return;

        Canvas rootCanvas = null;
        var allCanvas = GameObject.FindObjectsOfType<Canvas>();
        for (int i = 0; i < allCanvas.Length; i++)
        {
            if (allCanvas[i] != null && allCanvas[i].isRootCanvas)
            {
                rootCanvas = allCanvas[i];
                break;
            }
        }
        if (rootCanvas == null) return;

        // Reparent to root canvas and preserve screen position
        Vector3 worldPos = rt.position;
        Vector2 sizeDelta = rt.sizeDelta;
        rt.SetParent(rootCanvas.transform, true); // keep world position
        rt.position = worldPos;
        rt.sizeDelta = sizeDelta;
    }

    private static bool HasLayoutInParents(RectTransform rt)
    {
        Transform t = rt.parent;
        while (t != null)
        {
            if (t.GetComponent<HorizontalLayoutGroup>() != null) return true;
            if (t.GetComponent<VerticalLayoutGroup>() != null) return true;
            if (t.GetComponent<GridLayoutGroup>() != null) return true;
            if (t.GetComponent<ContentSizeFitter>() != null) return true;
            t = t.parent;
        }
        return false;
    }

    private void EnforceInternalLayout(RectTransform container)
    {
        if (container == null) return;

        var vlg = container.GetComponent<VerticalLayoutGroup>();
        if (vlg != null && enforceVerticalLayout)
        {
            vlg.reverseArrangement = false;            // top -> down
            vlg.childAlignment = TextAnchor.UpperRight;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
        }

        var glg = container.GetComponent<GridLayoutGroup>();
        if (glg != null && enforceGridLayout)
        {
            glg.startCorner = GridLayoutGroup.Corner.UpperRight;
            glg.startAxis = GridLayoutGroup.Axis.Vertical; // fills downward
        }

        var fitter = container.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private static void AutoBindTextsForBinder(PsychoactiveHUDRow binder, GameObject root)
    {
        if (binder == null || root == null) return;

        if (binder.nameObject == null || binder.timeObject == null)
        {
            var name = root.transform.Find("NameText");
            var time = root.transform.Find("TimeText");

            if (binder.nameObject == null)
                binder.nameObject = name != null ? name.gameObject : FindFirstTextGO(root);

            if (binder.timeObject == null)
                binder.timeObject = time != null ? time.gameObject : FindSecondTextGO(root, binder.nameObject);
        }
    }

    private static GameObject FindFirstTextGO(GameObject root)
    {
        var t = root.GetComponentInChildren<TMP_Text>(true);
        if (t != null) return t.gameObject;
        var u = root.GetComponentInChildren<Text>(true);
        if (u != null) return u.gameObject;
        return root;
    }

    private static GameObject FindSecondTextGO(GameObject root, GameObject exclude)
    {
        var tmps = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
            if (tmps[i] != null && tmps[i].gameObject != exclude)
                return tmps[i].gameObject;

        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
            if (texts[i] != null && texts[i].gameObject != exclude)
                return texts[i].gameObject;

        return root;
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int s = Mathf.FloorToInt(seconds);
        int m = s / 60;
        s = s % 60;
        return m.ToString("00") + ":" + s.ToString("00");
    }

    // Fallback overlay UI (only used if listRoot or rowPrefab missing)
    private void EnsureFallbackOverlay()
    {
        if (fallbackCanvas != null && fallbackPanel != null) return;

        GameObject cgo = new GameObject("PsychoactiveHUD_FallbackCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cgo.layer = LayerMask.NameToLayer("UI");
        fallbackCanvas = cgo.GetComponent<Canvas>();
        fallbackCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0f;

        GameObject pgo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        pgo.transform.SetParent(cgo.transform, false);
        fallbackPanel = pgo.GetComponent<RectTransform>();
        var img = pgo.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.3f);

        var fitter = pgo.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = pgo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperRight;
        layout.reverseArrangement = false; // top -> down

        // Anchor top-right
        fallbackPanel.anchorMin = new Vector2(1f, 1f);
        fallbackPanel.anchorMax = new Vector2(1f, 1f);
        fallbackPanel.pivot = new Vector2(1f, 1f);
        fallbackPanel.anchoredPosition = fallbackTopRightOffset;
    }

    private void DestroyFallbackIfAny()
    {
        if (fallbackCanvas != null) Destroy(fallbackCanvas.gameObject);
        fallbackCanvas = null;
        fallbackPanel = null;
        fallbackRowTemplate = null;
    }

    private GameObject CreateFallbackRowPrefabOnce()
    {
        if (fallbackRowTemplate != null) return fallbackRowTemplate;

        GameObject r = new GameObject("FallbackRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var h = r.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = true;
        h.childForceExpandWidth = false;
        h.spacing = 8f;

        GameObject nameGO = new GameObject("NameText", typeof(RectTransform), typeof(Text));
        nameGO.transform.SetParent(r.transform, false);
        var nameT = nameGO.GetComponent<Text>();
        nameT.text = "Effect";
        nameT.fontSize = fallbackFontSize;
        nameT.fontStyle = FontStyle.Bold;
        nameT.color = new Color(1f, 1f, 1f, 0.95f);
        nameT.alignment = TextAnchor.MiddleRight;
        nameT.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameT.raycastTarget = false;

        GameObject timeGO = new GameObject("TimeText", typeof(RectTransform), typeof(Text));
        timeGO.transform.SetParent(r.transform, false);
        var timeT = timeGO.GetComponent<Text>();
        timeT.text = "00:00";
        timeT.fontSize = fallbackFontSize;
        timeT.color = new Color(1f, 0.9f, 0.6f, 0.95f);
        timeT.alignment = TextAnchor.MiddleRight;
        timeT.horizontalOverflow = HorizontalWrapMode.Overflow;
        timeT.raycastTarget = false;

        var bind = r.AddComponent<PsychoactiveHUDRow>();
        bind.nameObject = nameGO;
        bind.timeObject = timeGO;

        fallbackRowTemplate = r;
        return fallbackRowTemplate;
    }

    // Simple list pool
    private static class ListPool<T>
    {
        private static readonly Stack<List<T>> pool = new Stack<List<T>>();
        public static List<T> Get()
        {
            return pool.Count > 0 ? pool.Pop() : new List<T>(8);
        }
        public static void Release(List<T> list)
        {
            list.Clear();
            pool.Push(list);
        }
    }
}
