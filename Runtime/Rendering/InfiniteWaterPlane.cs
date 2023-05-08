using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Settings;
#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
using UnityEngine.Experimental.Rendering.RenderGraphModule;
#endif

namespace WaterSystem.Rendering
{
    public class InfiniteWaterPlane : ScriptableRenderPass
    {
        private PassData _passData = new PassData();
        
        private class PassData
        {
            public Mesh InfiniteMesh;
            public Shader InfiniteShader;
            public Material InfiniteMaterial;
            public MaterialPropertyBlock MPB;
            public Matrix4x4 Matrix;
        }

        private readonly Material _material;
        private readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private readonly SphericalHarmonicsL2[] _ambientProbe = new SphericalHarmonicsL2[1];

        private const string PassName = nameof(InfiniteWaterPlane);

        public InfiniteWaterPlane(Material material)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            profilingSampler = new ProfilingSampler(PassName);
            _material = material;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!ExecutionCheck(renderingData.cameraData)) return;
            SetupPassData(ref _passData, renderingData.cameraData);
            
            
            var cmd = CommandBufferPool.Get();
            cmd.DrawMesh(_passData.InfiniteMesh, _passData.Matrix, _passData.InfiniteMaterial, 0, 0, _passData.MPB);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        
#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
        
        public override void RecordRenderGraph(RenderGraph renderGraph, FrameResources frameResources,
            ref RenderingData renderingData)
        {
            if (!ExecutionCheck(renderingData.cameraData)) return;
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData, profilingSampler))
            {
                SetupPassData(ref passData, renderingData.cameraData);
                
                builder.UseTextureFragment(frameResources.GetTexture(UniversalResource.CameraColor), 0);
                builder.UseTextureFragmentDepth(frameResources.GetTexture(UniversalResource.CameraDepth), 0);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawMesh(data.InfiniteMesh, data.Matrix, data.InfiniteMaterial, 0, 0,
                        data.MPB);
                });
            }
        }

#endif

        private void SetupPassData(ref PassData data, CameraData cameraData)
        {
            data.InfiniteMesh = WaterProjectSettings.Instance.resources?.infiniteWaterMesh;
            data.InfiniteMaterial = _material;
            data.Matrix = Matrix4x4.TRS(cameraData.worldSpaceCameraPos, Quaternion.identity, Vector3.one);
            data.MPB = _mpb;
            _ambientProbe[0] = RenderSettings.ambientProbe;
            data.MPB.CopySHCoefficientArraysFrom(_ambientProbe);
        }

        private bool ExecutionCheck(CameraData cameraData)
        {
            var cameraType = cameraData.camera.cameraType;
            if (cameraType is not CameraType.Game or CameraType.SceneView &&
                cameraData.camera.name.Contains("Reflections"))
            {
                Debug.Log($"Infinite water plane Skipping Camera {cameraData.camera.name}");
                return false;
            }
            
            if (!WaterProjectSettings.Instance.resources?.infiniteWaterMesh)
            {
                Debug.LogError($"Infinite water place pass kipping due to missing Mesh or Material");
                return false;
            }

            return true;
        }
    }
}