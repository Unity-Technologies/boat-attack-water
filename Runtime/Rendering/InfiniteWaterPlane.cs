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
        #region Required Garbage

        private PassData passData = new PassData();
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            SetupPassData(ref passData, renderingData.cameraData.worldSpaceCameraPos);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera cam = renderingData.cameraData.camera;

            if (cam.cameraType != CameraType.Game &&
                cam.cameraType != CameraType.SceneView ||
                cam.name.Contains("Reflections")) return;

            if (!passData.infiniteMaterial || !passData.infiniteMesh) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_InfiniteWater_Profile))
            {
                var probe = RenderSettings.ambientProbe;

                //passData.infiniteMaterial.SetFloat(BumpScale, 0.5f);

                // Create the matrix to position the caustics mesh.
                var position = cam.transform.position;
                var matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                // Setup the CommandBuffer and draw the mesh with the caustic material and matrix
                MaterialPropertyBlock matBloc = new MaterialPropertyBlock();
                matBloc.CopySHCoefficientArraysFrom(new[] { probe });
                cmd.DrawMesh(passData.infiniteMesh, matrix, passData.infiniteMaterial, 0, 0, matBloc);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        #endregion

        private class PassData
        {
            public Mesh infiniteMesh;
            public Shader infiniteShader;
            public Material infiniteMaterial;
            public MaterialPropertyBlock mpb;
            public Matrix4x4 matrix;
        }

        private readonly Material m_Material;
        private readonly MaterialPropertyBlock m_MPB = new MaterialPropertyBlock();
        private readonly SphericalHarmonicsL2[] m_AmbientProbe = new SphericalHarmonicsL2[1];

        private const string PassName = nameof(InfiniteWaterPlane);
        private readonly ProfilingSampler m_InfiniteWater_Profile = new ProfilingSampler(PassName);

        public InfiniteWaterPlane(Material material)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            m_Material = material;
            m_AmbientProbe[0] = RenderSettings.ambientProbe;
        }
        
#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
        
        public override void RecordRenderGraph(RenderGraph renderGraph, FrameResources frameResources,
            ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            
            if (cam.cameraType != CameraType.Game &&
                cam.cameraType != CameraType.SceneView) return;
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(PassName, out var passData, m_InfiniteWater_Profile))
            {
                if (!SetupPassData(ref passData, renderingData.cameraData.worldSpaceCameraPos)) return;
                
                builder.AllowPassCulling(true);

                builder.UseTextureFragment(frameResources.GetTexture(UniversalResource.CameraColor), 0);
                builder.UseTextureFragmentDepth(frameResources.GetTexture(UniversalResource.CameraDepth), 0);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawMesh(data.infiniteMesh, data.matrix, data.infiniteMaterial, 0, 0,
                        data.mpb);
                });
            }
        }

#endif

        private bool SetupPassData(ref PassData data, Vector3 cameraPosition)
        {
            data.infiniteMesh = WaterProjectSettings.Instance.resources.infiniteWaterMesh;
            if (!data.infiniteMesh) return false;
            data.infiniteMaterial = m_Material;
            if (!data.infiniteMaterial) return false;

            data.matrix = Matrix4x4.TRS(cameraPosition, Quaternion.identity, Vector3.one);
            data.mpb = m_MPB;
            m_AmbientProbe[0] = RenderSettings.ambientProbe;
            data.mpb.CopySHCoefficientArraysFrom(m_AmbientProbe);
            return true;
        }
    }
}


// OLD code
/*
#region InfiniteWater Pass

    public class InfiniteWaterPass : ScriptableRenderPass
    {
        public Mesh infiniteMesh;
        public Shader infiniteShader;
        public Material infiniteMaterial;

        private ProfilingSampler m_InfiniteWater_Profile = new ProfilingSampler(nameof(InfiniteWaterPass));
        private static readonly int BumpScale = Shader.PropertyToID("_BumpScale");

        public InfiniteWaterPass(Mesh mesh, Shader shader)
        {
            if (mesh) infiniteMesh = mesh;
            if (shader) infiniteShader = shader;
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera cam = renderingData.cameraData.camera;

            if (cam.cameraType != CameraType.Game &&
                cam.cameraType != CameraType.SceneView ||
                cam.name.Contains("Reflections")) return;

            if (infiniteMesh == null)
            {
                Debug.LogError("Infinite Water Pass Mesh is missing.");
                return;
            }

            if (infiniteShader)
            {
                if(infiniteMaterial == null)
                    infiniteMaterial = new Material(infiniteShader);
            }

            if (!infiniteMaterial || !infiniteMesh) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_InfiniteWater_Profile))
            {
                var probe = RenderSettings.ambientProbe;

                infiniteMaterial.SetFloat(BumpScale, 0.5f);

                // Create the matrix to position the caustics mesh.
                var position = cam.transform.position;
                var matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                // Setup the CommandBuffer and draw the mesh with the caustic material and matrix
                MaterialPropertyBlock matBloc = new MaterialPropertyBlock();
                matBloc.CopySHCoefficientArraysFrom(new[] { probe });
                cmd.DrawMesh(infiniteMesh, matrix, infiniteMaterial, 0, 0, matBloc);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    #endregion
    
    */