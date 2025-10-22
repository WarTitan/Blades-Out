using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

// Builds a stack and a world-space TMP label showing the count.
// Label can be laid flat (X rotation), faced to parent +Z or world +Z, mirrored left-right,
// and offset in local XYZ relative to the computed top position.
public class CurrencyStack : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject elementPrefab;                // OPTIONAL (fallback used if null)
    public Vector3 scaleMultiplier = Vector3.one;

    [Header("Layout")]
    public int maxVisibleElements = 20;             // visual cap; label shows actual count
    public bool autoStepFromPrefabBounds = true;
    public float manualVerticalStep = 0.018f;       // used if autoStepFromPrefabBounds=false
    public float labelYOffset = 0.02f;              // extra local Y above stack top
    public string labelPrefix = "";                 // " gold", " chips", or "" for just number
    public bool putOnIgnoreRaycast = false;         // set ON only if your camera renders that layer

    [Header("Randomization (natural stack)")]
    public float yawJitterDegrees = 8f;             // per piece random Y rotation
    public float xzJitter = 0.0035f;                // per piece +/- offset on X/Z
    public float yJitter = 0.0015f;                 // per piece vertical jitter

    [Header("Label Style")]
    public TMP_FontAsset labelFontAsset;            // optional TMP font
    public float labelFontSize = 2.4f;              // world-space TMP size
    public bool labelAutoSize = false;              // TMP autosize
    public Color labelColor = Color.white;
    public Vector3 labelLocalScale = Vector3.one;

    [Header("Label Orientation")]
    public float labelXRotation = -90f;             // -90 = lie flat on top
    public bool labelFaceParentZ = true;            // face parent's +Z, else world +Z
    public float labelYawOffset = 0f;               // add 180 if text is backwards
    public bool labelMirrorLeftRight = false;       // TRUE => mirror horizontally (left-right)
    public Vector3 labelLocalOffset = Vector3.zero; // extra LOCAL XYZ offset for label

    // Runtime
    private Transform elementsRoot;
    private TextMeshPro labelTmp;

    private int realCount = 0;
    private int shownCount = -1;

    private float verticalStep = 0.018f;
    private int ignoreLayer = 2; // Ignore Raycast
    private System.Random prng;

    private struct Elem
    {
        public GameObject go;
        public float yawDeg;
        public Vector2 xz;
        public float y;
        public float boundsY;
    }
    private readonly List<Elem> pool = new List<Elem>(64);

    public void SetSeed(int seed) { prng = new System.Random(seed); }

    public void Build()
    {
        if (prng == null) prng = new System.Random(transform.GetInstanceID());

        if (putOnIgnoreRaycast)
        {
            int maybe = LayerMask.NameToLayer("Ignore Raycast");
            if (maybe >= 0) ignoreLayer = maybe;
            SetLayerRecursively(gameObject, ignoreLayer);
        }

        // Root for stacked elements
        var er = new GameObject("ElementsRoot");
        er.transform.SetParent(transform, false);
        elementsRoot = er.transform;

        // Pre-create pool (uses prefab or fallback primitive)
        EnsurePoolSize(maxVisibleElements);

        // Label object (world-space TMP)
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        if (putOnIgnoreRaycast) labelGo.layer = ignoreLayer;

        labelTmp = labelGo.AddComponent<TextMeshPro>();
        labelTmp.text = "0" + labelPrefix;
        labelTmp.alignment = TextAlignmentOptions.Center;
        ApplyLabelStyle(); // font, size, color, base scale (mirror applied separately)

        shownCount = -1; // force refresh
        UpdateVisuals(); // place pieces + label once
    }

    public void ApplyCount(int count)
    {
        realCount = Mathf.Max(0, count);
        UpdateVisuals();
    }

    public void SetWorld(Vector3 pos, Quaternion rot)
    {
        transform.position = pos;
        transform.rotation = rot;
    }

    void LateUpdate()
    {
        // Keep label orientation consistent each frame (in case parents move)
        OrientLabel();
    }

    private void UpdateVisuals()
    {
        if (elementsRoot == null) return;

        int visible = Mathf.Min(realCount, maxVisibleElements);
        EnsurePoolSize(visible);

        // Activate needed, deactivate the rest
        for (int i = 0; i < pool.Count; i++)
        {
            bool on = i < visible;
            var e = pool[i];
            if (e.go != null && e.go.activeSelf != on)
                e.go.SetActive(on);
        }

        // Compute vertical step (auto or manual)
        if (autoStepFromPrefabBounds)
        {
            float step = manualVerticalStep;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].go != null && pool[i].go.activeSelf)
                {
                    step = Mathf.Max(0.001f, pool[i].boundsY * 0.9f);
                    break;
                }
            }
            verticalStep = step;
        }
        else
        {
            verticalStep = Mathf.Max(0.0001f, manualVerticalStep);
        }

        // Position each visible piece
        float topY = 0f;
        for (int i = 0; i < visible; i++)
        {
            var e = pool[i];
            if (e.go == null) continue;

            float baseY = i * verticalStep;
            float y = baseY + e.y;

            e.go.transform.localPosition = new Vector3(e.xz.x, y, e.xz.y);
            e.go.transform.localRotation = Quaternion.Euler(0f, e.yawDeg, 0f);
            e.go.transform.localScale = scaleMultiplier;

            topY = y + e.boundsY * 0.5f; // approx top
        }

        // Label text + placement
        if (labelTmp != null)
        {
            labelTmp.text = realCount.ToString() + labelPrefix;

            // Base local position at top + offsets, then convert to world
            Vector3 localPos = new Vector3(0f, topY + labelYOffset, 0f) + labelLocalOffset;
            Vector3 worldPos = transform.TransformPoint(localPos);
            labelTmp.transform.position = worldPos;

            // Re-apply style in case Inspector changed before play
            ApplyLabelStyle();

            // Final orientation & mirroring
            OrientLabel();
            ApplyMirrorIfNeeded();
        }

        shownCount = visible;
    }

    private void OrientLabel()
    {
        if (labelTmp == null) return;

        // Compute yaw: face parent's +Z (anchor forward) or world +Z.
        float worldYaw;
        if (labelFaceParentZ)
        {
            Vector3 fwd = transform.forward; // world forward of this stack (child of anchor)
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.000001f) worldYaw = 0f;
            else worldYaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }
        else
        {
            worldYaw = 0f; // face world +Z
        }
        worldYaw += labelYawOffset;

        // X rotation you control (use -90 to lie flat on the stack)
        float worldX = labelXRotation;

        // Apply WORLD rotation so it is independent of parent quirks
        labelTmp.transform.rotation = Quaternion.Euler(worldX, worldYaw, 0f);
    }

    private void ApplyMirrorIfNeeded()
    {
        if (labelTmp == null) return;

        // Make sure TMP material is double-sided when mirroring (avoid culling issues)
        var rend = labelTmp.GetComponent<Renderer>();
        if (rend != null && rend.material != null)
        {
            var mat = rend.material; // instance
            if (mat.HasProperty("_CullMode")) mat.SetInt("_CullMode", (int)CullMode.Off);
            else if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)CullMode.Off);
        }

        // Base scale from inspector
        Vector3 baseScale = labelLocalScale;
        if (baseScale.x == 0f) baseScale.x = 0.001f;

        // Mirror horizontally by flipping X scale sign
        if (labelMirrorLeftRight)
            labelTmp.rectTransform.localScale = new Vector3(-Mathf.Abs(baseScale.x), baseScale.y, baseScale.z);
        else
            labelTmp.rectTransform.localScale = new Vector3(Mathf.Abs(baseScale.x), baseScale.y, baseScale.z);
    }

    private void ApplyLabelStyle()
    {
        if (labelTmp == null) return;
        labelTmp.enableAutoSizing = labelAutoSize;
        labelTmp.fontSize = labelFontSize;
        if (labelFontAsset != null && labelTmp.font != labelFontAsset) labelTmp.font = labelFontAsset;
        labelTmp.color = labelColor;
        // scale is applied in ApplyMirrorIfNeeded so mirroring works reliably
    }

    private void EnsurePoolSize(int count)
    {
        while (pool.Count < count)
        {
            var go = CreateElementInstance();
            go.name = "Element_" + pool.Count;
            if (putOnIgnoreRaycast) SetLayerRecursively(go, ignoreLayer);

            // Remove colliders so stacks are not interactable
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < cols.Length; c++) cols[c].enabled = false;

            // Random per-element offsets (stable once)
            float yaw = RandomRangeSigned(prng, yawJitterDegrees);
            float jx = RandomRangeSigned(prng, xzJitter);
            float jz = RandomRangeSigned(prng, xzJitter);
            float jy = RandomRangeSigned(prng, yJitter);

            float h = ComputeApproxLocalHeight(go);
            if (h <= 0.0001f) h = manualVerticalStep;

            var elem = new Elem
            {
                go = go,
                yawDeg = yaw,
                xz = new Vector2(jx, jz),
                y = jy,
                boundsY = h
            };
            pool.Add(elem);

            go.SetActive(false);
        }
    }

    private GameObject CreateElementInstance()
    {
        if (elementPrefab != null)
            return Instantiate(elementPrefab, elementsRoot, false);

        // Fallback primitive: cylinder
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.transform.SetParent(elementsRoot, false);

        var mr = cyl.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
            var mat = new Material(sh);
            mat.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            mr.sharedMaterial = mat;
        }
        return cyl;
    }

    private static float RandomRangeSigned(System.Random r, float mag)
    {
        if (mag <= 0f) return 0f;
        return (float)((r.NextDouble() * 2.0 - 1.0) * mag);
    }

    private float ComputeApproxLocalHeight(GameObject root)
    {
        var rends = root.GetComponentsInChildren<MeshRenderer>(true);
        if (rends == null || rends.Length == 0) return Mathf.Max(0.0001f, manualVerticalStep);

        Bounds b = new Bounds(root.transform.InverseTransformPoint(rends[0].bounds.center), Vector3.zero);
        for (int i = 0; i < rends.Length; i++)
        {
            b.Encapsulate(root.transform.InverseTransformPoint(rends[i].bounds.min));
            b.Encapsulate(root.transform.InverseTransformPoint(rends[i].bounds.max));
        }
        return Mathf.Abs(b.size.y) * Mathf.Max(scaleMultiplier.y, 0.0001f);
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
    }
}
