// FILE: DartsBoardMaskScorer.cs
// EncodedRed mode: score is stored in the RED channel (0..255) of a mask texture.
// Reads exact bytes via GetPixels32() (cached). Nearest-texel sample + small neighbor probe.
// IMPORT SETTINGS (critical):
//   Read/Write: ON
//   sRGB (Color Texture): OFF
//   Filter Mode: Point
//   Generate Mip Maps: OFF
//   Compression: None
//   Wrap Mode: Clamp (recommended)

using UnityEngine;
using System;
using System.Collections.Generic;

[AddComponentMenu("Minigames/Darts Board Mask Scorer")]
public class DartsBoardMaskScorer : MonoBehaviour
{
    [Serializable] public struct ColorScore { public Color color; public int scoreValue; }
    public enum DecodeMode { EncodedRed, Palette }

    [Header("Board")]
    public int boardIndex1Based = 1;

    [Header("Mask")]
    public Texture2D maskTexture;

    [Header("Decoding")]
    public DecodeMode decodeMode = DecodeMode.EncodedRed;
    [Tooltip("If Palette is selected but colorMap is empty, auto-use EncodedRed to avoid Inspector mishaps.")]
    public bool preferEncodedIfPaletteEmpty = true;

    [Tooltip("Palette mode only: per-channel tolerance for flat colors.")]
    [Range(0f, 0.2f)] public float channelTolerance = 0.02f;

    [Header("EncodedRed Sampling")]
    [Tooltip("Neighbor probe radius in texels if center returns 0. 0=off, 1=3x3, 2=5x5.")]
    [Range(0, 2)] public int probeRadius = 1;

    [Header("Debug")]
    public bool debugLogs = false;

    // Palette (used only if DecodeMode = Palette and list not empty)
    public List<ColorScore> colorMap = new List<ColorScore>();

    // Cached raw pixels (post-import, uncompressed, exact bytes)
    private Color32[] _pixels;
    private int _pw, _ph;
    private Texture2D _cachedTex;

    private void OnEnable()
    {
        RefreshCache();
    }

    private void OnValidate()
    {
        // Refresh cache when settings change in editor
        RefreshCache();
    }

    private void RefreshCache()
    {
        _pixels = null;
        _pw = _ph = 0;
        _cachedTex = null;

        if (maskTexture != null && maskTexture.isReadable)
        {
            _pixels = maskTexture.GetPixels32();
            _pw = maskTexture.width;
            _ph = maskTexture.height;
            _cachedTex = maskTexture;
        }
    }

    private int ReadRedByteNearest(Vector2 uv)
    {
        if (maskTexture == null || !maskTexture.isReadable) return 0;

        if (_pixels == null || _cachedTex != maskTexture || _pw != maskTexture.width || _ph != maskTexture.height)
            RefreshCache();

        if (_pixels == null || _pw <= 0 || _ph <= 0) return 0;

        int px = Mathf.Clamp(Mathf.RoundToInt(uv.x * (_pw - 1)), 0, _pw - 1);
        int py = Mathf.Clamp(Mathf.RoundToInt(uv.y * (_ph - 1)), 0, _ph - 1);
        int idx = py * _pw + px;

        int r = _pixels[idx].r;

        if (r == 0 && probeRadius > 0)
        {
            int maxR = 0;
            for (int dy = -probeRadius; dy <= probeRadius; dy++)
            {
                int sy = Mathf.Clamp(py + dy, 0, _ph - 1);
                int rowBase = sy * _pw;
                for (int dx = -probeRadius; dx <= probeRadius; dx++)
                {
                    int sx = Mathf.Clamp(px + dx, 0, _pw - 1);
                    int rr = _pixels[rowBase + sx].r;
                    if (rr > maxR) maxR = rr;
                }
            }
            r = maxR;
        }
        return r;
    }

    public bool TryScoreFromHit(RaycastHit hit, out int boardIndex, out int scoreValue)
    {
        boardIndex = boardIndex1Based;
        scoreValue = 0;

        if (maskTexture == null)
        {
            if (debugLogs) Debug.LogWarning("[DartsMask] No maskTexture.");
            return false;
        }
        if (!hit.collider)
        {
            if (debugLogs) Debug.LogWarning("[DartsMask] No collider on hit.");
            return false;
        }

        Vector2 uv = hit.textureCoord; // requires MeshCollider with valid UVs
        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
        {
            if (debugLogs) Debug.LogWarning("[DartsMask] UV out of range: " + uv);
            return false;
        }

        bool useEncoded = (decodeMode == DecodeMode.EncodedRed) ||
                          (decodeMode == DecodeMode.Palette && preferEncodedIfPaletteEmpty && (colorMap == null || colorMap.Count == 0));

        if (useEncoded)
        {
            int rInt = ReadRedByteNearest(uv);
            scoreValue = Mathf.Clamp(rInt, 0, 255);

            if (debugLogs)
                Debug.Log("[DartsMask/EncodedRed] UV=" + uv + " R=" + scoreValue + " board=" + boardIndex);

            return scoreValue > 0;
        }
        else
        {
            // Palette mode
            Color c = maskTexture.GetPixelBilinear(uv.x, uv.y);
            for (int i = 0; i < colorMap.Count; i++)
            {
                if (Mathf.Abs(c.r - colorMap[i].color.r) <= channelTolerance &&
                    Mathf.Abs(c.g - colorMap[i].color.g) <= channelTolerance &&
                    Mathf.Abs(c.b - colorMap[i].color.b) <= channelTolerance)
                {
                    scoreValue = Mathf.Max(0, colorMap[i].scoreValue);
                    if (debugLogs) Debug.Log("[DartsMask/Palette] UV=" + uv + " -> score=" + scoreValue);
                    return scoreValue > 0;
                }
            }
            if (debugLogs) Debug.Log("[DartsMask/Palette] No palette match at UV=" + uv);
            return false;
        }
    }
}
