using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem.Rendering
{
    #region Water Effects Pass

    public class WaterFxPass : ScriptableRenderPass
    {
        private static int m_BufferATexture = Shader.PropertyToID("_WaterBufferA");
        private static int m_BufferBTexture = Shader.PropertyToID("_WaterBufferB");

        private static int
            m_MockDepthTexture = Shader.PropertyToID("_DepthBufferMock"); // TODO remove once bug is fixed

        private RenderTargetIdentifier m_BufferTargetA = new RenderTargetIdentifier(m_BufferATexture);
        private RenderTargetIdentifier m_BufferTargetB = new RenderTargetIdentifier(m_BufferBTexture);

        private RenderTargetIdentifier
            m_BufferDepth = new RenderTargetIdentifier(m_MockDepthTexture); // TODO also remove


        private const string k_RenderWaterFXTag = "Render Water FX";
        private ProfilingSampler m_WaterFX_Profile = new ProfilingSampler(k_RenderWaterFXTag);
        private readonly ShaderTagId m_WaterFXShaderTag = new ShaderTagId("WaterFX");

        private readonly Color
            m_ClearColor =
                new Color(0.0f, 0.5f, 0.5f, 0.5f); //r = foam mask, g = normal.x, b = normal.z, a = displacement

        private FilteringSettings m_FilteringSettings;
        private RenderTargetHandle m_WaterFX = RenderTargetHandle.CameraTarget;

        public WaterFxPass()
        {
            m_WaterFX.Init("_WaterFXMap");
            // only wanting to render transparent objects
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        }

        // Calling Configure since we are wanting to render into a RenderTexture and control cleat
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor rtd = new RenderTextureDescriptor
            {
                // no need for a depth buffer
                depthBufferBits = 0,
                // dimension
                dimension = TextureDimension.Tex2D,
                // Half resolution
                width = cameraTextureDescriptor.width, // / 2;
                height = cameraTextureDescriptor.height, // / 2;
                // default format TODO research usefulness of HDR format
                colorFormat = RenderTextureFormat.Default,
                msaaSamples = 1,
                useMipMap = false,
            };

            // get a temp RT for rendering into
            cmd.GetTemporaryRT(m_BufferATexture, rtd, FilterMode.Bilinear);
            cmd.GetTemporaryRT(m_BufferBTexture, rtd, FilterMode.Bilinear);
            cmd.GetTemporaryRT(m_MockDepthTexture, rtd, FilterMode.Point);

            RenderTargetIdentifier[] multiTargets = { m_BufferTargetA, m_BufferTargetB };
            ConfigureTarget(multiTargets, m_MockDepthTexture);
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

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_WaterFX_Profile)) // makes sure we have profiling ability
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // here we choose renderers based off the "WaterFX" shader pass and also sort back to front
                var drawSettings = CreateDrawingSettings(m_WaterFXShaderTag, ref renderingData,
                    SortingCriteria.CommonTransparent);

                // draw all the renderers matching the rules we setup
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // since the texture is used within the single cameras use we need to cleanup the RT afterwards
            cmd.ReleaseTemporaryRT(m_BufferATexture);
            cmd.ReleaseTemporaryRT(m_BufferBTexture);
            cmd.ReleaseTemporaryRT(m_MockDepthTexture);
        }
    }

    #endregion

    #region InfiniteWater Pass

    public class InfiniteWaterPass : ScriptableRenderPass
    {
        private Mesh infiniteMesh;
        private Material infiniteMaterial;

        public InfiniteWaterPass(Mesh mesh)
        {
            if (mesh)
                infiniteMesh = mesh;
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

            if (infiniteMaterial == null)
                infiniteMaterial = CoreUtils.CreateEngineMaterial("Boat Attack/Water/InfiniteWater");

            if (!infiniteMaterial || !infiniteMesh) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler("Infinite Water")))
            {

                var probe = RenderSettings.ambientProbe;

                infiniteMaterial.SetFloat("_BumpScale", 0.5f);

                // Create the matrix to position the caustics mesh.
                var position = cam.transform.position;
                var matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                // Setup the CommandBuffer and draw the mesh with the caustic material and matrix
                MaterialPropertyBlock matBloc = new MaterialPropertyBlock();
                matBloc.CopySHCoefficientArraysFrom(new[] { probe });
                cmd.DrawMesh(infiniteMesh, matrix, infiniteMaterial, 0, 0, matBloc);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }

    #endregion

    #region Caustics Pass

    public class WaterCausticsPass : ScriptableRenderPass
    {
        private const string k_RenderWaterCausticsTag = "Render Water Caustics";
        private ProfilingSampler m_WaterCaustics_Profile = new ProfilingSampler(k_RenderWaterCausticsTag);
        public Material WaterCausticMaterial;
        private static Mesh m_mesh;

        public WaterCausticsPass(Material material)
        {
            if (WaterCausticMaterial == null)
            {
                WaterCausticMaterial = material;
            }
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cam = renderingData.cameraData.camera;
            // Stop the pass rendering in the preview or material missing
            if (cam.cameraType == CameraType.Preview || !WaterCausticMaterial)
                return;

            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_WaterCaustics_Profile))
            {
                var sunMatrix = RenderSettings.sun != null
                    ? RenderSettings.sun.transform.localToWorldMatrix
                    : Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-45f, 45f, 0f), Vector3.one);
                WaterCausticMaterial.SetMatrix("_MainLightDir", sunMatrix);
                WaterCausticMaterial.SetFloat("_WaterLevel", Ocean.Instance.transform.position.y);


                // Create mesh if needed
                if (!m_mesh)
                    m_mesh = PassUtilities.GenerateCausticsMesh(1000f);

                // Create the matrix to position the caustics mesh.
                var position = cam.transform.position;
                //position.y = 0; // TODO should read a global 'water height' variable.
                position.y = Ocean.Instance.transform.position.y;
                var matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                // Setup the CommandBuffer and draw the mesh with the caustic material and matrix
                cmd.DrawMesh(m_mesh, matrix, WaterCausticMaterial, 0, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    #endregion

    public static class PassUtilities
    {
        public static Mesh GenerateCausticsMesh(float size, bool flat = true)
        {
            var m = new Mesh();
            size *= 0.5f;

            var verts = new[]
            {
                new Vector3(-size, flat ? 0f : -size, flat ? -size : 0f),
                new Vector3(size, flat ? 0f : -size, flat ? -size : 0f),
                new Vector3(-size, flat ? 0f : size, flat ? size : 0f),
                new Vector3(size, flat ? 0f : size, flat ? size : 0f)
            };
            m.vertices = verts;

            var tris = new[]
            {
                0, 2, 1,
                2, 3, 1
            };
            m.triangles = tris;

            return m;
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
    }
}