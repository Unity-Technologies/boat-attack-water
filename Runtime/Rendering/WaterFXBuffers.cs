using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace WaterSystem.Rendering
{
    public class WaterBuffers : ScriptableRenderPass
    {
        private static string m_BufferATexture = "_WaterBufferA";
        private static string m_BufferBTexture = "_WaterBufferB";
        
#if UNITY_2022_1_OR_NEWER
        private RTHandle m_BufferTargetA, m_BufferTargetB;
#else
        private int m_BufferTargetA, m_BufferTargetB;
#endif
        
        private const string k_RenderWaterFXTag = "Render Water FX";
        private readonly ShaderTagId m_WaterFXShaderTag = new ShaderTagId("WaterFX");

        //r = foam mask
        //g = normal.x
        //b = normal.z
        //a = displacement
        private readonly Color m_ClearColor = new(0.0f, 0.5f, 0.5f, 0.5f);

        private FilteringSettings m_FilteringSettings;
        
        public WaterBuffers()
        {
            profilingSampler = new ProfilingSampler(GetType().Name);
            // only wanting to render transparent objects
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        }

        // Calling Configure since we are wanting to render into a RenderTexture and control cleat
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var td = GetRTD(cameraTextureDescriptor.width, cameraTextureDescriptor.height);
            
#if UNITY_2022_1_OR_NEWER
            RenderingUtils.ReAllocateIfNeeded(ref m_BufferTargetA, td, FilterMode.Bilinear, name:m_BufferATexture);
            RenderingUtils.ReAllocateIfNeeded(ref m_BufferTargetB, td, FilterMode.Bilinear, name:m_BufferBTexture);
            RTHandle[] multiTargets = { m_BufferTargetA, m_BufferTargetB };
            cmd.SetGlobalTexture(m_BufferATexture, m_BufferTargetA.nameID);
            cmd.SetGlobalTexture(m_BufferBTexture, m_BufferTargetB.nameID);
#else
            m_BufferTargetA = Shader.PropertyToID(m_BufferATexture);
            m_BufferTargetB = Shader.PropertyToID(m_BufferBTexture);
            cmd.GetTemporaryRT(m_BufferTargetA, td, FilterMode.Bilinear);
            cmd.GetTemporaryRT(m_BufferTargetB, td, FilterMode.Bilinear);
            RenderTargetIdentifier[] multiTargets = { m_BufferTargetA, m_BufferTargetB };
#endif
            ConfigureTarget(multiTargets);
            // clear the screen with a specific color for the packed data
            ConfigureClear(ClearFlag.Color, m_ClearColor);

#if UNITY_2021_1_OR_NEWER
            ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
#endif
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            var drawSettings =
                CreateDrawingSettings(m_WaterFXShaderTag, ref renderingData, SortingCriteria.CommonTransparent);
            
            var cmd = CommandBufferPool.Get();
            //using (new ProfilingScope(cmd, profilingSampler)) // TODO - Causes a hard crash on Metal + Apple Silicon
            //{
                cmd.Clear();
#if UNITY_2023_1_OR_NEWER // RenderGraph
            var rendererListParams =
                new RendererListParams(renderingData.cullResults, drawSettings, m_FilteringSettings);
            var rendererList =
                context.CreateRendererList(ref rendererListParams);
            cmd.DrawRendererList(rendererList);
#else
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
#endif
            //}

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
        
        private class PassData
        {
            // one for RG
            public RendererListHandle renderListHdl;
            // one for non-RG
            public RendererList renderList;
            // clear color
            public Color clearColor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer contextContainer)
        {
            UniversalCameraData cameraData = contextContainer.Get<UniversalCameraData>();
            // Textures
            var td = GetRTD(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight);
            var bufferA = Utilities.CreateRenderGraphTexture(renderGraph, td, m_BufferATexture, false, clearColor: m_ClearColor);
            var bufferB = Utilities.CreateRenderGraphTexture(renderGraph, td, m_BufferBTexture, false, clearColor: m_ClearColor);
            
            // Resources
            var waterResourceData = contextContainer.GetOrCreate<Utilities.WaterResourceData>();
            waterResourceData.BufferA = bufferA;
            waterResourceData.BufferB = bufferB;
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_RenderWaterFXTag, out var passData, profilingSampler))
            {
                builder.AllowPassCulling(false);

                passData.clearColor = m_ClearColor;

                UniversalRenderingData renderingData = contextContainer.Get<UniversalRenderingData>();
                var renderListDesc = RenderListDesc(renderingData.cullResults, cameraData.camera);
                passData.renderListHdl = renderGraph.CreateRendererList(renderListDesc);
                builder.UseRendererList(passData.renderListHdl);
                
                builder.UseTextureFragment(bufferA, 0);
                builder.UseTextureFragment(bufferB, 1);
                
                //UniversalResourceData frameResources = contextContainer.Get<UniversalResourceData>();
                //builder.UseTextureFragmentDepth(frameResources.cameraDepth, IBaseRenderGraphBuilder.AccessFlags.None);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    //context.cmd.ClearRenderTarget(false, true, data.clearColor);
                    context.cmd.DrawRendererList(data.renderListHdl);
                });
            }
            
            // set global
            Utilities.SetGlobalTexture(renderGraph, m_BufferATexture, bufferA, "Set WaterFX A Texture");
            Utilities.SetGlobalTexture(renderGraph, m_BufferBTexture, bufferB, "Set WaterFX B Texture");
        }

        private RendererListDesc RenderListDesc(CullingResults cullingResults, Camera camera)
        {
            var rld = new RendererListDesc(m_WaterFXShaderTag, cullingResults, camera)
            {
                sortingCriteria = SortingCriteria.CommonTransparent,
                renderQueueRange = RenderQueueRange.transparent,
            };
            return rld;
        }
        
#endif

        private RenderTextureDescriptor GetRTD(int width, int height)
        {
            var td = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0);
            // dimension
            td.dimension = TextureDimension.Tex2D;
            td.msaaSamples = 1;
            td.useMipMap = false;
            td.stencilFormat = GraphicsFormat.None;
            td.volumeDepth = 1;
            td.sRGB = false;
            return td;
        }
    }
}