using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEditor;

namespace DistortionsPro_20X
{
    public sealed class FlowMosh_20X : ScriptableRendererFeature
    {
        [SerializeField]              // Shader used by the fullscreen material
        private Shader m_Shader;

        [SerializeField, HideInInspector]
        private Material material;    // Material instance built from m_Shader

        private FlowMoshPass Distortion_RenderPass;  // Our single render pass

        public RenderPassEvent Event = RenderPassEvent.BeforeRenderingPostProcessing; // Injection point

        public override void Create()
        {
            // Locate the shader in the project (returns null if missing)
            m_Shader = Shader.Find("DistortionsPro_20X/FlowMosh");
            if (m_Shader == null) return;  // Abort if shader not found

            // Instantiate material so it survives into build (serialised)
            material = new Material(m_Shader);

            // Create the pass and set the desired render‑event
            Distortion_RenderPass = new FlowMoshPass(material)
            {
                renderPassEvent = Event
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Grab FlowMosh component (real one or the fallback default instance)
            FlowMosh myVolume = VolumeManager.instance.stack?.GetComponent<FlowMosh>();

            // Skip if component inactive – note: IsActive() checks Blend & override
            if (myVolume == null || !myVolume.IsActive()) return;

            // Optional global PP toggle: if PP disabled & user wants to respect that
            if (!renderingData.cameraData.postProcessEnabled && myVolume.GlobalPostProcessingSettings.value)
                return;

            // Only run in Game view (skip Scene/Reflection/Preview cameras)
            if (renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(Distortion_RenderPass);
        }

        protected override void Dispose(bool disposing)
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
#else
        Destroy(material);
#endif
        }

        private sealed class FlowMoshPass : ScriptableRenderPass
        {
            // -------- Shader property IDs (avoid string lookups every frame) ----
            static readonly int BrightnessThreshold = Shader.PropertyToID("_BrightnessThreshold");
            static readonly int _Threshold = Shader.PropertyToID("_Threshold");
            static readonly int _Size = Shader.PropertyToID("_Size");
            static readonly int _NoiseAmp = Shader.PropertyToID("_NoiseAmp");
            static readonly int _ChromaShift = Shader.PropertyToID("ChromaShift");
            static readonly int _Fade = Shader.PropertyToID("fade");
            static readonly int _MicroContrastBoost = Shader.PropertyToID("_MicroContrastBoost");

            // ------------------------------------------------------------------
            private readonly Material m_Mat;  // Shared fullscreen material
            private RTHandle m_History;       // Persistent feedback buffer

            public FlowMoshPass(Material mat)
            {
                m_Mat = mat;
                requiresIntermediateTexture = true;   // we sample cameraColor
                ConfigureInput(ScriptableRenderPassInput.Motion); // need motion vectors
            }

            // -------------------------------- DATA STRUCTS passed to RG funcs ---
            private class MainData
            {
                public Material mat;
                public TextureHandle src;   // cameraColor
                public TextureHandle prev;  // history as TextureHandle
                public RTHandle history;    // RTHandle for size/alloc
                public FlowMosh vol;
            }
            private class CopyData { public TextureHandle src; public RTHandle history; }


            public override void RecordRenderGraph(RenderGraph rg, ContextContainer frame)
            {
                var vol = VolumeManager.instance.stack.GetComponent<FlowMosh>();
                bool enabled = vol != null && vol.Blend.overrideState && vol.Blend.value > 0f;
                if (!enabled) { ReleaseHistory(); return; }

                var res = frame.Get<UniversalResourceData>();
                var cam = frame.Get<UniversalCameraData>();

                EnsureHistory(cam.cameraTargetDescriptor);

                // ── PASS #1: 
                TextureHandle temp;
                {
                    using var builder = rg.AddRasterRenderPass<MainData>("Flow-Mosh", out var data);

                    builder.AllowGlobalStateModification(true);

                    data.mat = m_Mat;
                    data.vol = vol;
                    data.history = m_History;                       // RTHandle
                    data.src = res.cameraColor;                 // TextureHandle
                    data.prev = rg.ImportTexture(m_History);     // TextureHandle

                    builder.UseTexture(data.src, AccessFlags.Read);
                    builder.UseTexture(data.prev, AccessFlags.Read);

                    var desc = rg.GetTextureDesc(res.cameraColor);
                    desc.name = "_FlowMoshTemp";
                    desc.clearBuffer = false;
                    temp = rg.CreateTexture(desc);
                    builder.SetRenderAttachment(temp, 0, AccessFlags.Write);

                    builder.SetRenderFunc((MainData d, RasterGraphContext ctx) =>
                    {
                        ApplyVolume(d.mat, d.vol, d.history);
                        ctx.cmd.SetGlobalTexture("_PreviousTex", d.prev);
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0);
                    });
                }

                res.cameraColor = temp;

                // ── PASS #2: temp → history
                {
                    using var builder = rg.AddRasterRenderPass<CopyData>("Flow-Mosh History", out var data);
                    data.src = temp;
                    data.history = m_History;

                    builder.UseTexture(data.src, AccessFlags.Read);

                    var dst = rg.ImportTexture(m_History);
                    builder.SetRenderAttachment(dst, 0, AccessFlags.Write);

                    builder.SetRenderFunc((CopyData d, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1, 1, 0, 0), 0f, false);
                    });
                }
            }
            static bool IsVRActive()
            {
                var gs = XRGeneralSettings.Instance;
                if (gs != null && gs.Manager != null && gs.Manager.activeLoader != null) return true;
                return XRSettings.isDeviceActive;
            }
            private void EnsureHistory(in RenderTextureDescriptor camDesc)
            {
                var desc = camDesc;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.enableRandomWrite = false;
                desc.graphicsFormat = camDesc.graphicsFormat;

                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref m_History,
                    in desc,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    anisoLevel: 1,
                    mipMapBias: 0f,
                    name: "_FlowMoshHistory");
            }


            private static void ApplyVolume(Material mat, FlowMosh v, RTHandle history)
            {
                if (v == null) return; // safety

                mat.SetFloat(BrightnessThreshold, 1 - v.BrightnessThreshold.value);
                mat.SetFloat(_MicroContrastBoost, 1 - v.MicroContrastBoost.value);
                mat.SetFloat(_Threshold, 1 - v.ActivationThreshold.value);
                mat.SetFloat(_Size, v.BlockSize.value);
                mat.SetFloat(_NoiseAmp, v.NoiseAmplitude.value);
                mat.SetFloat(_ChromaShift, v.ChromaShift.value);
                mat.SetFloat("FLOW_GAIN", v.FlowGain.value);
                mat.SetFloat("_FlowXScale", v.FlowXScale.value);
                mat.SetFloat(_Fade, v.Blend.value);
                mat.SetFloat("C_Offset", IsVRActive() ? 100 : 1000);
            }

            public void Dispose() => ReleaseHistory(); // called by feature

            private void ReleaseHistory()
            {
                if (m_History != null)
                {
                    RTHandles.Release(m_History);
                    m_History = null;
                }
            }
        }
    }
}
