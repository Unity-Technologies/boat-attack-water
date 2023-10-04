using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Settings;
using WaterSystem.Rendering;
#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
#endif

namespace WaterSystem.Rendering
{
    public class WaterCaustics : ScriptableRenderPass
    {
        private static Mesh _mesh;
        private static Material _material;

        private static Dictionary<WaterBody, PassData> _passData = new Dictionary<WaterBody, PassData>();

        private class PassData
        {
            internal Data data;
            internal struct Data
            {
                internal Material WaterCausticMaterial;
                internal Mesh m_mesh;
                internal Matrix4x4 matrix;
            }
        }

        public WaterCaustics()
        {
            profilingSampler = new ProfilingSampler(GetType().Name);

            if (_material == null)
            {
                _material = CoreUtils.CreateEngineMaterial(ProjectSettings.Instance.resources.CausticShader);
                _material.SetTexture("_CausticMap", ProjectSettings.Instance.resources.SurfaceMap);
            }
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureInput(ScriptableRenderPassInput
                .Color); // TODO, adding here but is needed for the water in the transparent pass
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Stop the pass rendering in the preview and if material is missing
            if (!ExecutionCheck(renderingData.cameraData.camera, _material)) return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                // for each water body in WaterManager add a pass data if it doesn't exist
                foreach (var waterBody in WaterManager.WaterBodies)
                {
                    if (waterBody == null) continue;
                    PassData data;
                    if (!_passData.ContainsKey(waterBody))
                    {
                        data = new PassData();
                        _passData.Add(waterBody, data);
                    }

                    data = _passData[waterBody];
                    SetupPassData(ref data, waterBody, renderingData.cameraData.worldSpaceCameraPos);
                    _passData[waterBody] = data;

#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
            ExecutePass(_passData[waterBody], CommandBufferHelpers.GetRasterCommandBuffer(cmd));
#else
                    ExecutePass(_passData[waterBody], cmd);
#endif
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer contextContainer)
        {
            // populate pass data for each water body and call RenderRG
            foreach (var waterBody in WaterManager.WaterBodies)
            {
                if (waterBody == null) continue;
                PassData data;
                if (!_passData.ContainsKey(waterBody))
                {
                    data = new PassData();
                    _passData.Add(waterBody, data);
                }
                
                UniversalCameraData cameraData = contextContainer.Get<UniversalCameraData>();
                UniversalResourceData resourceData = contextContainer.Get<UniversalResourceData>();

                data = _passData[waterBody];
                SetupPassData(ref data, waterBody, cameraData.worldSpaceCameraPos);
                _passData[waterBody] = data;
                RenderRG(renderGraph, _passData[waterBody], cameraData.camera, resourceData);
            }
        }

        private void RenderRG(RenderGraph renderGraph, PassData inputData, Camera camera, UniversalResourceData resourceData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(nameof(WaterCaustics), out var passData, profilingSampler))
            {
                passData.data = inputData.data;
                // Stop the pass rendering in the preview and if material is missing
                if (!ExecutionCheck(camera, passData.data.WaterCausticMaterial)) return;
                
                builder.AllowPassCulling(false);
                
                // set buffers
                builder.UseTextureFragment(resourceData.activeColorTexture, 0);
                builder.UseTextureFragmentDepth(resourceData.activeDepthTexture, IBaseRenderGraphBuilder.AccessFlags.Read);

                // set depthtexture read for the shader
                builder.UseTexture(resourceData.cameraDepthTexture);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    if(data.data.m_mesh != null || data.data.WaterCausticMaterial != null)
                        context.cmd.DrawMesh(data.data.m_mesh, data.data.matrix, data.data.WaterCausticMaterial, 0, 0);
                    //ExecutePass(data, context.cmd);
                });
            }
        }
        
#endif

#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
        static void ExecutePass(PassData data, RasterCommandBuffer cmd)
#else
        static void ExecutePass(PassData data, CommandBuffer cmd)
#endif
        {
            // Draw the mesh with the caustic material and matrix
            if(data.data.m_mesh != null || data.data.WaterCausticMaterial != null)
                cmd.DrawMesh(data.data.m_mesh, data.data.matrix, data.data.WaterCausticMaterial, 0, 0);
        }

        private void SetupPassData(ref PassData data, WaterBody waterBody, Vector3 cameraPosition)
        {
            var sunMatrix = RenderSettings.sun != null
                ? RenderSettings.sun.transform.localToWorldMatrix
                : Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-45f, 45f, 0f), Vector3.one);
            if (data.data.WaterCausticMaterial == null)
            {
                if(_material == null)
                    return;
                data.data.WaterCausticMaterial = Material.Instantiate(_material);
            }

            data.data.WaterCausticMaterial.SetMatrix("_MainLightDir", sunMatrix);

            Matrix4x4 matrix;
            if (waterBody.shape.type == WaterBody.WaterShapeType.Infinite)
            {
                cameraPosition.y = waterBody.transform.position.y;
                data.data.matrix = Matrix4x4.TRS(cameraPosition, Quaternion.identity, Vector3.one * 1000f);
                matrix = Matrix4x4.TRS(cameraPosition, Quaternion.identity, Vector3.one);
            }
            else
            {
                var transform = waterBody.transform;
                var size = waterBody.shape.type == WaterBody.WaterShapeType.Plane ?
                    new Vector3(waterBody.shape.size.x, 1f, waterBody.shape.size.y): Vector3.one * waterBody.shape.Radius * 2f;
                data.data.matrix = Matrix4x4.TRS(transform.position, transform.rotation, size);
                matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            }
            data.data.WaterCausticMaterial.SetMatrix("_WaterBodyToWorld", matrix);

            data.data.WaterCausticMaterial.SetFloat("_WaterLevel", waterBody.transform.position.y);
            // Create mesh if needed

            if (!data.data.m_mesh)
            {
                if (_mesh == null)
                    _mesh = Utilities.GenerateCausticsMesh(1f);
                data.data.m_mesh = _mesh;
            }
        }

        private bool ExecutionCheck(Camera cam, Material mat)
        {
            if (cam.cameraType is not (CameraType.SceneView or CameraType.Game)) return false;
            return mat != null;
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(_material);
            foreach (var data in _passData)
            {
                CoreUtils.Destroy(data.Value.data.WaterCausticMaterial);
            }

            _passData.Clear();
        }
    }
}