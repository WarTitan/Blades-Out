// FILE: DrunkBlitFeature.cs
// URP render feature that runs a full screen blit with a material.
// Designed to behave like classic Graphics.Blit so old image-effect
// shaders that sample _MainTex work correctly.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DrunkBlitFeature : ScriptableRendererFeature
{
    private class DrunkBlitPass : ScriptableRenderPass
    {
        private RTHandle tempTexture;

        public DrunkBlitPass()
        {
            profilingSampler = new ProfilingSampler("Drunk Blit (URP)");
        }

        // Compatibility path: Unity still calls this in Compatibility Mode.
        [System.Obsolete("Legacy OnCameraSetup override for compatibility.")]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Material mat = DrunkBlitFeature.RuntimeMaterial;
            if (mat == null || mat.shader == null)
                return;

            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

#pragma warning disable 618
            // Allocate or resize a RTHandle that matches the camera target.
            RenderingUtils.ReAllocateIfNeeded(
                ref tempTexture,
                descriptor,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                false,          // isShadowMap
                1,              // anisoLevel
                0f,             // mipMapBias
                "_DrunkBlitTemp"
            );
#pragma warning restore 618
        }

        // Compatibility path: Unity still calls this in Compatibility Mode.
        [System.Obsolete("Legacy Execute override for compatibility.")]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Material mat = DrunkBlitFeature.RuntimeMaterial;
            if (mat == null || mat.shader == null || tempTexture == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("Drunk Blit (URP)");

#pragma warning disable 618
            // Use cameraColorTargetHandle as URP requires (no cameraColorTarget).
            RTHandle sourceHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;
#pragma warning restore 618

            // Convert RTHandles to RenderTargetIdentifier so we can use cmd.Blit,
            // which sets _MainTex like the old Built-in Graphics.Blit.
            RenderTargetIdentifier src = sourceHandle.nameID;
            RenderTargetIdentifier dst = tempTexture.nameID;

            using (new ProfilingScope(cmd, profilingSampler))
            {
                // source -> temp with material (your shader samples _MainTex)
                cmd.Blit(src, dst, mat, 0);
                // temp -> source back
                cmd.Blit(dst, src);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle is managed by ReAllocateIfNeeded; do not ReleaseTemporaryRT here.
        }

        public void Dispose()
        {
            if (tempTexture != null)
            {
                tempTexture.Release();
                tempTexture = null;
            }
        }
    }

    public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

    private DrunkBlitPass pass;

    // Material used by the pass. It is set from VodkaEffect at runtime.
    public static Material RuntimeMaterial;
    public static bool DebugLogs;

    public override void Create()
    {
        pass = new DrunkBlitPass();
        pass.renderPassEvent = passEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (RuntimeMaterial == null || RuntimeMaterial.shader == null)
            return;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && pass != null)
        {
            pass.Dispose();
            pass = null;
        }
    }

    public static void SetRuntimeMaterial(Material mat)
    {
        RuntimeMaterial = mat;
    }
}
