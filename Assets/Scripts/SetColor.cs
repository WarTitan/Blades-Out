using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class SetColor : MonoBehaviour
{
    public Color color = Color.cyan;  // set in Inspector
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"); // URP/HDRP
    private static readonly int StdColor = Shader.PropertyToID("_Color");     // Built-in Standard

    void Start()
    {
        var r = GetComponent<Renderer>();
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        // Try URP/HDRP first, fall back to Standard
        if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColor))
            mpb.SetColor(BaseColor, color);
        else
            mpb.SetColor(StdColor, color);

        r.SetPropertyBlock(mpb);
    }
}
