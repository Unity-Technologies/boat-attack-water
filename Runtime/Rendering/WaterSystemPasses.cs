using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem.Rendering
{
    public static class PassUtilities
    {
        private static Mesh _causticMesh;

        public enum WaterResources
        {
            BufferA,
            BufferB,
        }

        public static Mesh GenerateCausticsMesh(float size, bool flat = true)
        {
            if (_causticMesh != null) return _causticMesh;

            _causticMesh = new Mesh();
            size *= 0.5f;

            var verts = new[]
            {
                new Vector3(-size, flat ? 0f : -size, flat ? -size : 0f),
                new Vector3(size, flat ? 0f : -size, flat ? -size : 0f),
                new Vector3(-size, flat ? 0f : size, flat ? size : 0f),
                new Vector3(size, flat ? 0f : size, flat ? size : 0f)
            };
            _causticMesh.vertices = verts;

            var tris = new[]
            {
                0, 2, 1,
                2, 3, 1
            };
            _causticMesh.triangles = tris;

            return _causticMesh;
        }

        [System.Serializable]
        public class WaterSystemSettings
        {
            [Header("Caustics Settings")] [Range(0.1f, 1f)]
            public float causticScale = 0.25f;

            public float causticBlendDistance = 3f;

            [Header("Infinite Water")] public Mesh mesh;

            [Header("Advanced Settings")] public DebugMode debug = DebugMode.Disabled;

            public enum DebugMode
            {
                Disabled,
                WaterEffects,
                Caustics
            }
        }

#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
        private class PassData
        {
            public string name;
            public TextureHandle texture;
        }

        private static ProfilingSampler setTextureSampler = new ProfilingSampler("GlobalSetTexture");

        public static void SetGlobalTexture(RenderGraph graph, string name, TextureHandle texture,
            string passName = "Set Global Texture")
        {
            using (var builder = graph.AddRasterRenderPass<PassData>(passName, out var passData, setTextureSampler))
            {
                passData.texture = builder.UseTexture(texture);
                passData.name = name;

                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalTexture(data.name, data.texture);
                });
            }
        }
        
        internal class DummyResourcePass : ScriptableRenderPass
        {
            private string _passName;
            private WaterResources[] _resources;

            public DummyResourcePass(WaterResources[] resources, RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering, string passName = "DummyResourcePass")
            {
                renderPassEvent = injectionPoint;
                _resources = resources;
                _passName = passName;
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // N/A
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, FrameResources frameResources, ref RenderingData renderingData)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(_passName, out _, profilingSampler))
                {
                    builder.AllowPassCulling(false);

                    foreach (var resource in _resources)
                    {
                        builder.UseTexture(frameResources.GetTexture(resource));
                    }

                    builder.SetRenderFunc((PassData _, RasterGraphContext _) => { });
                }
            }

        }
#endif
    }
}