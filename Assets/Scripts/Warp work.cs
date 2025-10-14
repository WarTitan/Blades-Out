using UnityEngine;
using TMPro;

[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public class TMP_WarpStatic : MonoBehaviour
{
    public AnimationCurve vertexCurve = new AnimationCurve(
        new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
    public float curveScale = 30f;   // increase for stronger bend

    TMP_Text tmp;
    Mesh mesh;

    void OnEnable()
    {
        tmp = GetComponent<TMP_Text>();
        BendNow();
    }

    void OnValidate() => BendNow();
    void Update() { if (Application.isPlaying) BendNow(); }

    void BendNow()
    {
        if (tmp == null) return;
        tmp.ForceMeshUpdate();
        var textInfo = tmp.textInfo;

        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            var mi = textInfo.meshInfo[m];
            var verts = mi.vertices;

            for (int c = 0; c < textInfo.characterCount; c++)
            {
                var ch = textInfo.characterInfo[c];
                if (!ch.isVisible || ch.materialReferenceIndex != m) continue;

                int i = ch.vertexIndex;

                // Normalize X across the rendered bounds (0..1)
                float x0 = ch.bottomLeft.x;
                float x1 = ch.topRight.x;
                float xMid = (x0 + x1) * 0.5f;
                float t = Mathf.InverseLerp(
                    textInfo.meshInfo[m].mesh.bounds.min.x,
                    textInfo.meshInfo[m].mesh.bounds.max.x,
                    xMid);

                // Vertical offset from curve
                float yOffset = vertexCurve.Evaluate(t) * curveScale;

                for (int v = 0; v < 4; v++)
                    verts[i + v].y += yOffset;
            }

            // apply back
            mi.mesh.vertices = verts;
            tmp.UpdateGeometry(mi.mesh, m);
        }
    }
}
