using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace WaterSystem.Rendering
{
    public class WaterBuffers : ScriptableRenderPass
    {
        private static string m_BufferATexture = "_WaterBufferA";
        private static string m_BufferBTexture = "_WaterBufferB";
        
        private RTHandle m_BufferTargetA, m_BufferTargetB;
        private const string k_RenderWaterFXTag = "Render Water FX";
        private readonly ShaderTagId m_WaterFXShaderTag = new ShaderTagId("WaterFX");
        //r = foam mask
        //g = normal.x
        //b = normal.z
        //a = displacement
        private readonly Color m_ClearColor = new(0.0f, 0.5f, 0.5f, 0.5f);
        
        public WaterBuffers()
        {
            profilingSampler = new ProfilingSampler(GetType().Name);
            // only wanting to render transparent objects
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        }

        private class PassData
        {
            public RendererListHandle renderList;
            // clear color
            public Color clearColor;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            // Textures
            //Water FX: Using raster commands from a pass with no active render targets is not allowed as it will use an undefined render target state. Please set-up the pass's render targets using SetRenderAttachments
            var td = GetRTD(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight); 
            
            RenderingUtils.ReAllocateIfNeeded(ref m_BufferTargetA, td, FilterMode.Bilinear, name:m_BufferATexture);
            RenderingUtils.ReAllocateIfNeeded(ref m_BufferTargetB, td, FilterMode.Bilinear, name:m_BufferBTexture);
            RTHandle[] multiTargets = { m_BufferTargetA, m_BufferTargetB };
            var bufferA = renderGraph.ImportTexture(m_BufferTargetA);
            var bufferB = renderGraph.ImportTexture(m_BufferTargetB);
            
            
            // ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            
            // Resources
            var waterResourceData = frameData.GetOrCreate<Utilities.WaterResourceData>();
            waterResourceData.BufferA = bufferA;
            waterResourceData.BufferB = bufferB;
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(k_RenderWaterFXTag, out var passData, profilingSampler))
            {
                builder.AllowPassCulling(false);
                // clear the screen with a specific color for the packed data
                passData.clearColor = m_ClearColor;

                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                var renderListDesc = 
                    new RendererListDesc(m_WaterFXShaderTag, renderingData.cullResults, cameraData.camera) {
                        sortingCriteria = SortingCriteria.CommonTransparent,
                        renderQueueRange = RenderQueueRange.transparent,
                    };
                
                passData.renderList = renderGraph.CreateRendererList(renderListDesc);
                builder.UseRendererList(passData.renderList);
                
                builder.SetRenderAttachment(bufferA, 0);
                builder.SetRenderAttachment(bufferB, 1);
                
                // UniversalResourceData frameResources = contextContainer.Get<UniversalResourceData>();
                // builder.UseTexture(frameResources.cameraDepth);
                
                // builder.AllowGlobalStateModification(true);
                builder.SetGlobalTextureAfterPass(bufferA,Shader.PropertyToID(m_BufferATexture));
                builder.SetGlobalTextureAfterPass(bufferB,Shader.PropertyToID(m_BufferBTexture));
                
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    context.cmd.ClearRenderTarget(false, true, data.clearColor);
                    context.cmd.DrawRendererList(data.renderList);
                });
                
            }
        }

        private RenderTextureDescriptor GetRTD(int width, int height)
        {
            var td = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0);
            // dimension
            td.dimension = TextureDimension.Tex2D;
            td.msaaSamples = 1;
            td.useMipMap = false;
            td.stencilFormat = 0;//GraphicsFormat.None;
            td.volumeDepth = 1;
            td.sRGB = false;
            return td;
        }
    }
}


        //
        // public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        // {
        //     var cam = renderingData.cameraData.camera;
        //     if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        //
        //     var drawSettings =
        //         CreateDrawingSettings(m_WaterFXShaderTag, ref renderingData, SortingCriteria.CommonTransparent);
        //     
        //     var cmd = CommandBufferPool.Get();
        //     
        //         cmd.Clear();
        //
        //     var rendererListParams =
        //         new RendererListParams(renderingData.cullResults, drawSettings, m_FilteringSettings);
        //     var rendererList =
        //         context.CreateRendererList(ref rendererListParams);
        //     cmd.DrawRendererList(rendererList);
        //
        //     context.ExecuteCommandBuffer(cmd);
        //     CommandBufferPool.Release(cmd);
        // }
        

        