// FILE: MemorySequenceUI.cs
// FULL FILE (ASCII only)
//
// Spawns memory board in WORLD at the correct seat.
// - BeginStatic(token, seconds, seatOverride): seatOverride comes from server RPC.
// - Uses MemoryBoardSeatSpawns to anchor; falls back to PlayerSpawn_<seat> then camera.
// - Wrong click -> immediate fail; success -> ends immediately.
// - Level curve per your spec; level persists per seat for this play session.
// - Intro: camera stays at its current position, smoothly rotates toward the SeatBoard_X
//   for this seat, shows a 3-2-1-GO countdown at board center, then the board appears.
//   No status text is shown during the game.

using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[AddComponentMenu("Gameplay/Memory/Memory Sequence UI")]
[DefaultExecutionOrder(50000)] // run AFTER LocalCameraController
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
    public float statusSize = 0.35f; // base size for countdown

    [Header("Intro Camera / Countdown")]
    public bool enableIntroSequence = true;
    public float introMoveDuration = 1.2f;     // how long the camera rotation tween takes
    public float countdownStepDuration = 0.75f;
    public float countdownFontScale = 4.0f;    // bigger countdown (3,2,1,GO)

    private Camera cam;
    private Transform boardRoot;
    private TextMeshPro countdownTMP;

    private Renderer[] padRenderers;
    private Collider[] padColliders;
    private readonly List<GameObject> pads = new List<GameObject>(64);
    private readonly HashSet<int> solution = new HashSet<int>();
    private readonly HashSet<int> selected = new HashSet<int>();

    private int gridN = 3;
    private int lightsToShow = 3;

    private bool running = false;
    private bool boardActive = false;
    private bool inputEnabled = false;
    private int token = 0;
    private float deadline = 0f;
    private float playSeconds = 0f;
    private int hoveredPad = -1;

    // Level state per seat (1..5). 0 means "unset" -> treated as level 1.
    private static int[] s_seatLevel = new int[6];
    private int currentLevel = 1;
    private int currentSeat = 1;

    // Optional seat override from server
    private int seatOverride = 0;

    // Intro camera override
    private bool introActive = false;
    private float introElapsed = 0f;
    private float introRotateDuration = 1.0f;
    private Vector3 introCamPos;
    private Quaternion introCamRotStart;
    private Quaternion introCamRotTarget;
    private LocalCameraController introLcc;

    // ---- Public entrypoint ----

    public static void BeginStatic(int token, float seconds, int seatOverrideIndex = 0)
    {
        if (Instance != null)
        {
            Object.Destroy(Instance.gameObject);
            Instance = null;
        }

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

#pragma warning disable CS0618
        return Object.FindObjectOfType<Camera>();
#pragma warning restore CS0618
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

        // Countdown text at center of board (0,0)
        var statusGO = new GameObject("CountdownTMP");
        statusGO.transform.SetParent(boardRoot, false);
        statusGO.transform.localPosition = Vector3.zero;
        countdownTMP = statusGO.AddComponent<TextMeshPro>();
        countdownTMP.text = "";
        countdownTMP.font = GetSafeTMPFont();
        countdownTMP.fontSize = statusSize;
        countdownTMP.alignment = TextAlignmentOptions.Center;
        countdownTMP.color = Color.white;
        countdownTMP.rectTransform.sizeDelta = new Vector2(3f, 0.6f);

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
            var cfg = MemoryBoardSeatSpawns.Instance;
            var seatAnchor = cfg.GetSeatAnchor(seat);
            if (seatAnchor != null)
            {
                anchor = seatAnchor;

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
        {
            if (pads[i] != null) Object.Destroy(pads[i]);
        }

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
            if (mat != null)
            {
                SetCol(mat, panelTint);
                pr.material = mat;
            }
        }

        var pcol = panel.GetComponent<Collider>();
        if (pcol) Object.Destroy(pcol);

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
                if (m != null)
                {
                    SetCol(m, colorOff);
                    r.material = m;
                }
                padRenderers[i] = r;
            }

            var c = pad.GetComponent<Collider>();
            if (c != null)
            {
                c.isTrigger = false;
                padColliders[i] = c;
            }

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

    // ---- Flow ----

    private void StartRun(int tkn, float seconds)
    {
        token = tkn;
        playSeconds = Mathf.Max(1f, seconds);
        currentSeat = GetLocalSeat();
        currentLevel = GetSeatLevel(currentSeat);

        running = true;
        boardActive = false;
        inputEnabled = false;
        hoveredPad = -1;
        introActive = false;
        introElapsed = 0f;
        introLcc = null;

        PlaceOnceBySeatAnchors();

        StopAllCoroutines();
        StartCoroutine(RunSequence(currentLevel));
    }

    private IEnumerator RunSequence(int level)
    {
        SetupIntroCameraLock();
        yield return PlayIntroCountdown();

        // Now start the actual timed minigame.
        deadline = Time.time + playSeconds;

        BuildBoardForLevel(level);
        boardActive = true;

        solution.Clear();
        selected.Clear();

        yield return RunSet(level);
    }

    private void SetupIntroCameraLock()
    {
        introActive = false;
        introElapsed = 0f;

        cam = cam == null ? ResolveCamera() : cam;
        if (!enableIntroSequence || cam == null || boardRoot == null)
        {
            introActive = false;
            return;
        }

        Transform ct = cam.transform;

        introCamPos = ct.position;
        introCamRotStart = ct.rotation;

        Vector3 boardPos = boardRoot.position;
        Vector3 dirToBoard = boardPos - introCamPos;
        if (dirToBoard.sqrMagnitude < 0.0001f)
        {
            dirToBoard = boardRoot.forward;
        }

        introCamRotTarget = Quaternion.LookRotation(dirToBoard.normalized, Vector3.up);
        introRotateDuration = Mathf.Max(0.01f, introMoveDuration);
        introActive = true;

        introLcc = null;
        var reporter = MemoryMinigameReporter.Local;
        if (reporter != null)
            introLcc = reporter.GetComponent<LocalCameraController>();
        if (introLcc == null)
            introLcc = ct.GetComponentInParent<LocalCameraController>();
    }

    private IEnumerator PlayIntroCountdown()
    {
        // Countdown at center (0,0) of the board.
        if (countdownTMP != null)
        {
            float originalSize = countdownTMP.fontSize;
            var originalAlign = countdownTMP.alignment;

            countdownTMP.fontSize = statusSize * countdownFontScale;
            countdownTMP.alignment = TextAlignmentOptions.Center;
            countdownTMP.alpha = 1f;

            for (int i = 3; i >= 1; i--)
            {
                countdownTMP.text = i.ToString();
                yield return new WaitForSeconds(countdownStepDuration);
            }

            countdownTMP.text = "GO";
            yield return new WaitForSeconds(countdownStepDuration * 0.5f);

            countdownTMP.fontSize = originalSize;
            countdownTMP.alignment = originalAlign;
            countdownTMP.text = string.Empty;
            countdownTMP.gameObject.SetActive(false);
        }
        else
        {
            float total = 3f * countdownStepDuration + countdownStepDuration * 0.5f;
            if (total < 0f) total = 0f;
            yield return new WaitForSeconds(total);
        }

        // When countdown finishes, snap the LocalCameraController's yaw/pitch
        // so that the camera looks directly at the board center again.
        if (introLcc != null && boardRoot != null)
        {
            introLcc.ForceLookAt(boardRoot.position);
        }

        introActive = false;
    }

    private IEnumerator RunSet(int level)
    {
        int total = gridN * gridN;
        if (lightsToShow > total - 1) lightsToShow = total - 1;

        while (solution.Count < lightsToShow)
            solution.Add(Random.Range(0, total));

        // Show pattern for memorization
        SetInputEnabled(false);
        yield return FlashSolutionOnce(0.8f, 0.25f);

        // Player selects tiles (no status text)
        SetAllPads(colorOff);
        SetInputEnabled(true);

        while (Time.time < deadline)
        {
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
        foreach (int idx in solution)
            SetPadColor(idx, colorOn);

        yield return new WaitForSeconds(onTime);

        foreach (int idx in solution)
            SetPadColor(idx, colorOff);

        yield return new WaitForSeconds(offTime);
    }

    private void SetAllPads(Color c)
    {
        int count = padRenderers != null ? padRenderers.Length : 0;
        for (int i = 0; i < count; i++)
            SetPadColor(i, c);
    }

    private void UpdateIntroCamera()
    {
        if (!introActive) return;

        cam = cam == null ? ResolveCamera() : cam;
        if (cam == null)
        {
            introActive = false;
            return;
        }

        introElapsed += Time.deltaTime;

        float t = 1f;
        if (introRotateDuration > 0f)
            t = Mathf.Clamp01(introElapsed / introRotateDuration);

        Transform ct = cam.transform;
        ct.position = introCamPos;
        ct.rotation = Quaternion.Slerp(introCamRotStart, introCamRotTarget, t);
    }

    private void LateUpdate()
    {
        if (introActive)
        {
            // Runs AFTER LocalCameraController (DefaultExecutionOrder 40000),
            // so mouse look cannot cancel our intro rotation.
            UpdateIntroCamera();
        }

        if (!running) return;
        if (!boardActive) return;

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
            if (hoveredPad >= 0 && !selected.Contains(hoveredPad))
                SetPadColor(hoveredPad, colorOff);

            hoveredPad = newHover;

            if (hoveredPad >= 0 && !selected.Contains(hoveredPad))
                SetPadColor(hoveredPad, colorHover);
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
    }

    private void Update()
    {
        if (!running) return;
        if (!boardActive) return;

        if (Time.time >= deadline)
            Finish(false);
    }

    private void Finish(bool success)
    {
        if (!running) return;

        running = false;
        boardActive = false;
        introActive = false;

        int nextLevel = success ? (currentLevel + 1) : currentLevel;
        SetSeatLevel(currentSeat, nextLevel);

        MemoryMinigameReporter.ReportResult(token, success ? 1 : 0);

        Object.Destroy(gameObject);
        Instance = null;
        seatOverride = 0;
    }

    private static bool SetsEqual(HashSet<int> a, HashSet<int> b)
    {
        if (a.Count != b.Count) return false;

        foreach (var x in a)
            if (!b.Contains(x)) return false;

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
