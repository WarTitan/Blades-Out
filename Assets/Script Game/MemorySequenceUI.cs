// FILE: MemorySequenceUI.cs
// FULL FILE (ASCII only)
//
// Spawns memory board in WORLD at the correct seat.
// - BeginStatic(token, seconds, seatOverride): seatOverride comes from server RPC.
// - Uses MemoryBoardSeatSpawns to anchor; falls back to PlayerSpawn_<seat> then camera.
// - Wrong click -> immediate fail; success -> ends immediately.
// - Level curve per your spec; level persists per seat for this play session.

using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Memory/Memory Sequence UI")]
public class MemorySequenceUI : MonoBehaviour
{
    public static MemorySequenceUI Instance;

    [Header("Fallback offset if only a camera is available")]
    public Vector3 cameraFallbackOffset = new Vector3(0f, 1.2f, 2.0f);

    [Header("Board Layout")]
    public float padSize = 0.24f;
    public float padSpacing = 0.06f;
    public float boardThickness = 0.02f;

    [Header("Panel Margin (extra around pads)")]
    public float panelMarginX = 0.12f;
    public float panelMarginY = 0.22f;

    [Header("Raycast")]
    public LayerMask rayMask = ~0;
    public float rayDistance = 100f;

    [Header("Colors")]
    public Color colorOff = new Color(0.25f, 0.25f, 0.25f, 1f);
    public Color colorOn = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color colorHover = new Color(0.65f, 0.65f, 0.65f, 1f);
    public Color panelTint = new Color(0f, 0f, 0f, 0.35f);
    public Color colorSelected = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color colorWrong = new Color(1f, 0.3f, 0.3f, 1f);

    [Header("Text")]
    public float statusSize = 0.25f;

    private Camera cam;
    private Transform boardRoot;
    private Renderer[] padRenderers;
    private Collider[] padColliders;
    private TextMeshPro statusTMP;

    private int gridN = 3;
    private int lightsToShow = 3;
    private readonly List<GameObject> pads = new List<GameObject>(64);
    private readonly HashSet<int> solution = new HashSet<int>();
    private readonly HashSet<int> selected = new HashSet<int>();

    private bool running = false;
    private bool inputEnabled = false;
    private int token = 0;
    private float deadline = 0f;
    private int hoveredPad = -1;

    // Level state per seat (1..5). 0 means "unset" -> treated as level 1.
    private static int[] s_seatLevel = new int[6];
    private int currentLevel = 1;
    private int currentSeat = 1;

    // Optional seat override from server
    private int seatOverride = 0;

    // ---- Public entrypoint ----

    public static void BeginStatic(int token, float seconds, int seatOverrideIndex = 0)
    {
        if (Instance != null) { Object.Destroy(Instance.gameObject); Instance = null; }
        EnsureExistence();
        if (Instance.running) return;
        Instance.seatOverride = seatOverrideIndex;
        Instance.StartRun(token, seconds);
    }

    private static void EnsureExistence()
    {
        if (Instance != null) return;
        var go = new GameObject("MemorySequenceWorld");
        Object.DontDestroyOnLoad(go);
        Instance = go.AddComponent<MemorySequenceUI>();
        Instance.BuildShell();
    }

    private Camera ResolveCamera()
    {
        var reporter = MemoryMinigameReporter.Local;
        if (reporter != null)
        {
            var lcc = reporter.GetComponent<LocalCameraController>();
            if (lcc != null && lcc.playerCamera != null) return lcc.playerCamera;
            var cChild = reporter.GetComponentInChildren<Camera>(true);
            if (cChild != null) return cChild;
        }
        if (Camera.main != null) return Camera.main;
        return Object.FindObjectOfType<Camera>();
    }

    private int GetSeatLevel(int seat)
    {
        if (seat < 1 || seat > 5) seat = 1;
        int lv = s_seatLevel[seat];
        if (lv < 1) lv = 1;
        return lv;
    }

    private void SetSeatLevel(int seat, int level)
    {
        if (seat < 1 || seat > 5) seat = 1;
        if (level < 1) level = 1;
        s_seatLevel[seat] = level;
    }

    private int GetLocalSeat()
    {
        if (seatOverride > 0) return seatOverride;

        var reporter = MemoryMinigameReporter.Local;
        if (reporter != null)
        {
            var trays = reporter.GetComponent<PlayerItemTrays>();
            if (trays != null && trays.seatIndex1Based > 0) return trays.seatIndex1Based;
        }
        return 1;
    }

    // ---- Build & layout ----

    private void BuildShell()
    {
        cam = ResolveCamera();

        var rootGO = new GameObject("BoardRoot");
        rootGO.transform.SetParent(transform, false);
        boardRoot = rootGO.transform;

        float globalScale = 1.0f;
        if (MemoryBoardSeatSpawns.Instance != null)
            globalScale = Mathf.Max(0.01f, MemoryBoardSeatSpawns.Instance.boardScale);
        boardRoot.localScale = Vector3.one * globalScale;

        var statusGO = new GameObject("StatusTMP");
        statusGO.transform.SetParent(boardRoot, false);
        statusTMP = statusGO.AddComponent<TextMeshPro>();
        statusTMP.text = "Memory Test";
        statusTMP.font = GetSafeTMPFont();
        statusTMP.fontSize = statusSize;
        statusTMP.alignment = TextAlignmentOptions.Center;
        statusTMP.color = Color.white;
        statusTMP.rectTransform.sizeDelta = new Vector2(3f, 0.6f);

        PlaceOnceBySeatAnchors();
    }

    private void PlaceOnceBySeatAnchors()
    {
        Transform anchor = null;
        Vector3 worldPos = Vector3.zero;
        Quaternion rotation = Quaternion.identity;

        int seat = GetLocalSeat();

        if (MemoryBoardSeatSpawns.Instance != null)
        {
            var seatAnchor = MemoryBoardSeatSpawns.Instance.GetSeatAnchor(seat);
            if (seatAnchor != null)
            {
                anchor = seatAnchor;
                var cfg = MemoryBoardSeatSpawns.Instance;
                if (cfg.offsetMode == MemoryBoardSeatSpawns.OffsetMode.None)
                    worldPos = seatAnchor.position;
                else if (cfg.offsetMode == MemoryBoardSeatSpawns.OffsetMode.World)
                    worldPos = seatAnchor.position + cfg.offset;
                else
                    worldPos = seatAnchor.TransformPoint(cfg.offset);

                rotation = cfg.alignWithSeatAnchorForward ? seatAnchor.rotation : Quaternion.identity;
            }
        }

        if (anchor == null)
        {
            var go = GameObject.Find("PlayerSpawn_" + seat);
            if (go != null)
            {
                anchor = go.transform;
                worldPos = anchor.TransformPoint(new Vector3(0f, 1.2f, 2.0f));
                rotation = anchor.rotation;
            }
        }

        cam = cam == null ? ResolveCamera() : cam;
        if (anchor == null && cam != null)
        {
            boardRoot.position = cam.transform.position + cam.transform.forward * cameraFallbackOffset.z + Vector3.up * cameraFallbackOffset.y;
            boardRoot.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
            return;
        }

        boardRoot.position = worldPos;
        boardRoot.rotation = rotation;
    }

    private TMP_FontAsset GetSafeTMPFont()
    {
        TMP_FontAsset f = TMP_Settings.defaultFontAsset;
        if (f == null) f = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return f;
    }

    private void ClearPads()
    {
        for (int i = 0; i < pads.Count; i++)
            if (pads[i] != null) Object.Destroy(pads[i]);
        pads.Clear();
        padRenderers = null;
        padColliders = null;

        var oldPanel = boardRoot.Find("Panel");
        if (oldPanel != null) Object.Destroy(oldPanel.gameObject);
    }

    private void BuildBoardForLevel(int level)
    {
        ComputeLayoutForLevel(level, out gridN, out lightsToShow);

        ClearPads();

        float totalSize = gridN * padSize + (gridN - 1) * padSpacing;

        var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        panel.name = "Panel";
        panel.transform.SetParent(boardRoot, false);
        panel.transform.localScale = new Vector3(totalSize + panelMarginX, totalSize + panelMarginY, 1f);
        var pr = panel.GetComponent<Renderer>();
        if (pr != null)
        {
            var mat = SafeClone(pr);
            if (mat != null) { SetCol(mat, panelTint); pr.material = mat; }
        }
        var pcol = panel.GetComponent<Collider>();
        if (pcol) Object.Destroy(pcol);

        statusTMP.transform.localPosition = new Vector3(0f, (totalSize * 0.5f) + 0.28f, 0f);

        int count = gridN * gridN;
        padRenderers = new Renderer[count];
        padColliders = new Collider[count];

        for (int i = 0; i < count; i++)
        {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad" + i;
            pad.transform.SetParent(boardRoot, false);

            int row = i / gridN;
            int col = i % gridN;
            float start = -totalSize * 0.5f + padSize * 0.5f;
            float x = start + col * (padSize + padSpacing);
            float y = -(start + row * (padSize + padSpacing));
            pad.transform.localPosition = new Vector3(x, y, 0f);
            pad.transform.localScale = new Vector3(padSize, padSize, boardThickness);

            var r = pad.GetComponent<Renderer>();
            if (r != null)
            {
                var m = SafeClone(r);
                if (m != null) { SetCol(m, colorOff); r.material = m; }
                padRenderers[i] = r;
            }

            var c = pad.GetComponent<Collider>();
            if (c != null) { c.isTrigger = false; padColliders[i] = c; }

            var idx = pad.AddComponent<MemoryPadIndex>();
            idx.index = i;

            pads.Add(pad);
        }
    }

    private static Material SafeClone(Renderer r)
    {
        if (r == null) return null;
        if (r.sharedMaterial != null) return new Material(r.sharedMaterial);
        if (r.material != null) return new Material(r.material);
        return null;
    }

    private static void SetCol(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        else m.color = c;
    }

    private void StartRun(int tkn, float seconds)
    {
        token = tkn;
        deadline = Time.time + Mathf.Max(1f, seconds);
        running = true;

        currentSeat = GetLocalSeat();
        currentLevel = GetSeatLevel(currentSeat);

        BuildBoardForLevel(currentLevel);

        StopAllCoroutines();
        StartCoroutine(RunSet(currentLevel));
    }

    private IEnumerator RunSet(int level)
    {
        solution.Clear();
        selected.Clear();

        int total = gridN * gridN;
        if (lightsToShow > total - 1) lightsToShow = total - 1;
        while (solution.Count < lightsToShow) solution.Add(Random.Range(0, total));

        statusTMP.text = BuildStatus("Memorize the tiles");
        SetInputEnabled(false);
        yield return FlashSolutionOnce(0.8f, 0.25f);

        statusTMP.text = BuildStatus("Select the same tiles");
        SetAllPads(colorOff);
        SetInputEnabled(true);

        while (Time.time < deadline)
        {
            statusTMP.text = BuildStatus("Select the same tiles");
            if (selected.Count == lightsToShow)
            {
                bool success = SetsEqual(selected, solution);
                Finish(success);
                yield break;
            }
            yield return null;
        }

        Finish(false);
    }

    private IEnumerator FlashSolutionOnce(float onTime, float offTime)
    {
        foreach (int idx in solution) SetPadColor(idx, colorOn);
        yield return new WaitForSeconds(onTime);
        foreach (int idx in solution) SetPadColor(idx, colorOff);
        yield return new WaitForSeconds(offTime);
    }

    private void SetAllPads(Color c)
    {
        int count = padRenderers != null ? padRenderers.Length : 0;
        for (int i = 0; i < count; i++) SetPadColor(i, c);
    }

    private void LateUpdate()
    {
        if (!running) return;
        UpdateHoverAndClick();
    }

    private void UpdateHoverAndClick()
    {
        int newHover = -1;

        if (inputEnabled)
        {
            cam = cam == null ? ResolveCamera() : cam;
            if (cam != null)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, rayDistance, rayMask, QueryTriggerInteraction.Ignore))
                {
                    var idx = hit.collider.GetComponent<MemoryPadIndex>();
                    if (idx != null) newHover = idx.index;
                }
            }
        }

        if (newHover != hoveredPad)
        {
            if (hoveredPad >= 0 && !selected.Contains(hoveredPad)) SetPadColor(hoveredPad, colorOff);
            hoveredPad = newHover;
            if (hoveredPad >= 0 && !selected.Contains(hoveredPad)) SetPadColor(hoveredPad, colorHover);
        }

        if (inputEnabled && Input.GetMouseButtonDown(0))
        {
            if (hoveredPad >= 0) OnPadClicked(hoveredPad);
        }
    }

    private void OnPadClicked(int idx)
    {
        if (!solution.Contains(idx))
        {
            SetPadColor(idx, colorWrong);
            Finish(false);
            return;
        }

        if (selected.Contains(idx))
        {
            selected.Remove(idx);
            if (hoveredPad == idx) SetPadColor(idx, colorHover);
            else SetPadColor(idx, colorOff);
        }
        else
        {
            if (selected.Count >= lightsToShow) return;
            selected.Add(idx);
            SetPadColor(idx, colorSelected);

            if (selected.Count == lightsToShow)
            {
                bool success = SetsEqual(selected, solution);
                Finish(success);
            }
        }
    }

    private void SetPadColor(int idx, Color c)
    {
        if (idx < 0 || padRenderers == null || idx >= padRenderers.Length) return;
        var r = padRenderers[idx];
        if (r != null && r.material != null)
        {
            if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", c);
            else if (r.material.HasProperty("_Color")) r.material.SetColor("_Color", c);
            else r.material.color = c;
        }
    }

    private void SetInputEnabled(bool en)
    {
        inputEnabled = en;
        if (statusTMP != null) statusTMP.alpha = en ? 1f : 0.85f;
    }

    private void Update()
    {
        if (!running) return;
        statusTMP.text = BuildStatus(inputEnabled ? "Select the same tiles" : "Memorize the tiles");
        if (Time.time >= deadline) Finish(false);
    }

    private void Finish(bool success)
    {
        if (!running) return;
        running = false;

        int nextLevel = success ? (currentLevel + 1) : currentLevel;
        SetSeatLevel(currentSeat, nextLevel);

        MemoryMinigameReporter.ReportResult(token, success ? 1 : 0);

        Object.Destroy(gameObject);
        Instance = null;
        seatOverride = 0;
    }

    private string BuildStatus(string phase)
    {
        float remain = deadline - Time.time;
        if (remain < 0f) remain = 0f;
        int s = Mathf.FloorToInt(remain);
        int m = s / 60;
        s = s % 60;
        return phase + "  " + m.ToString("00") + ":" + s.ToString("00");
    }

    private static bool SetsEqual(HashSet<int> a, HashSet<int> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var x in a) if (!b.Contains(x)) return false;
        return true;
    }

    private static void ComputeLayoutForLevel(int level, out int n, out int k)
    {
        if (level <= 0) level = 1;

        switch (level)
        {
            case 1: n = 3; k = 3; break;
            case 2: n = 3; k = 4; break;
            case 3: n = 4; k = 5; break;
            case 4: n = 4; k = 6; break;
            case 5: n = 4; k = 7; break;
            case 6: n = 5; k = 8; break;
            case 7: n = 5; k = 9; break;
            case 8: n = 5; k = 10; break;
            case 9: n = 5; k = 11; break;
            case 10: n = 5; k = 12; break;
            case 11: n = 6; k = 13; break;
            case 12: n = 6; k = 14; break;
            case 13: n = 6; k = 15; break;
            case 14: n = 6; k = 16; break;
            case 15: n = 6; k = 17; break;
            case 16: n = 7; k = 18; break;
            case 17: n = 7; k = 19; break;
            case 18: n = 7; k = 20; break;
            case 19: n = 7; k = 21; break;
            case 20: n = 7; k = 22; break;
            default:
                n = 7;
                k = 22 + (level - 20);
                break;
        }

        int maxK = n * n - 1;
        if (k > maxK) k = maxK;
        if (k < 1) k = 1;
    }
}
