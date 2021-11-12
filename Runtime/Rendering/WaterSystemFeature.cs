using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem
{
    public class WaterSystemFeature : ScriptableRendererFeature
    {

        #region Water Effects Pass

        public class WaterFxPass : ScriptableRenderPass
        {
            private static int m_BufferATexture = Shader.PropertyToID("_WaterBufferA");
            private static int m_BufferBTexture = Shader.PropertyToID("_WaterBufferB");
            private RenderTargetIdentifier m_BufferTargetA = new RenderTargetIdentifier(m_BufferATexture);
            private RenderTargetIdentifier m_BufferTargetB = new RenderTargetIdentifier(m_BufferBTexture);

            
            private const string k_RenderWaterFXTag = "Render Water FX";
            private ProfilingSampler m_WaterFX_Profile = new ProfilingSampler(k_RenderWaterFXTag);
            private readonly ShaderTagId m_WaterFXShaderTag = new ShaderTagId("WaterFX");
            private readonly Color m_ClearColor = new Color(0.0f, 0.5f, 0.5f, 0.5f); //r = foam mask, g = normal.x, b = normal.z, a = displacement
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
                RenderTextureDescriptor rtd = new RenderTextureDescriptor();
                // no need for a depth buffer
                rtd.depthBufferBits = 0;
                // dimension
                rtd.dimension = TextureDimension.Tex2D;
                // Half resolution
                rtd.width = cameraTextureDescriptor.width;// / 2;
                rtd.height = cameraTextureDescriptor.height;// / 2;
                // default format TODO research usefulness of HDR format
                rtd.colorFormat = RenderTextureFormat.Default;
                rtd.msaaSamples = 1;
                rtd.useMipMap = false;
                // get a temp RT for rendering into
                cmd.GetTemporaryRT(m_BufferATexture, rtd, FilterMode.Bilinear);
                cmd.GetTemporaryRT(m_BufferBTexture, rtd, FilterMode.Bilinear);
                
                RenderTargetIdentifier[] multiTargets = { m_BufferTargetA, m_BufferTargetB };
                ConfigureTarget(multiTargets);
                // clear the screen with a specific color for the packed data
                ConfigureClear(ClearFlag.Color, m_ClearColor);
                
                ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
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
                if(mesh)
                    infiniteMesh = mesh;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Camera cam = renderingData.cameraData.camera;
                
                if(cam.cameraType != CameraType.Game &&
                    cam.cameraType != CameraType.SceneView ||
                    cam.name.Contains("Reflections")) return;
                
                if (infiniteMesh == null)
                {
                    Debug.LogError("Infinite Water Pass Mesh is missing.");
                    return;
                }

                if (infiniteMaterial == null)
                    infiniteMaterial = CoreUtils.CreateEngineMaterial("Boat Attack/Water/InfiniteWater");

                if(!infiniteMaterial || !infiniteMesh) return;

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
                    matBloc.CopySHCoefficientArraysFrom(new []{probe});
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

                if(cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;

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
                        m_mesh = GenerateCausticsMesh(1000f);

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

        InfiniteWaterPass m_InfiniteWaterPass;
        WaterFxPass m_WaterFxPass;
        WaterCausticsPass m_CausticsPass;

        public WaterSystemSettings settings;
        [HideInInspector][SerializeField] private Shader causticShader;
        [HideInInspector][SerializeField] private Texture2D causticTexture;

        private Material _causticMaterial;

        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int Size = Shader.PropertyToID("_Size");
        private static readonly int CausticTexture = Shader.PropertyToID("_CausticMap");

        public override void Create()
        {
            // InfiniteWater Pass
            m_InfiniteWaterPass = new InfiniteWaterPass(settings.mesh);

            // WaterFX Pass
            m_WaterFxPass = new WaterFxPass();
            
            causticShader = causticShader ? causticShader : Shader.Find("Hidden/BoatAttack/Caustics");
            if (causticShader == null) return;
            if (_causticMaterial)
            {
                DestroyImmediate(_causticMaterial);
            }
            _causticMaterial = CoreUtils.CreateEngineMaterial(causticShader);
            _causticMaterial.SetFloat("_BlendDistance", settings.causticBlendDistance);

            if (causticTexture == null)
            {
                Debug.Log("Caustics Texture missing, attempting to load.");
#if UNITY_EDITOR
                causticTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.verasl.water-system/Textures/WaterSurface_single.tif");
#endif
            }
            _causticMaterial.SetTexture(CausticTexture, causticTexture);

            // Caustic Pass
            m_CausticsPass = new WaterCausticsPass(_causticMaterial);
            
            switch (settings.debug)
            {
                case WaterSystemSettings.DebugMode.Caustics:
                    _causticMaterial.SetFloat(SrcBlend, 1f);
                    _causticMaterial.SetFloat(DstBlend, 0f);
                    _causticMaterial.EnableKeyword("_DEBUG");
                    m_CausticsPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                    break;
                case WaterSystemSettings.DebugMode.WaterEffects:
                    break;
                case WaterSystemSettings.DebugMode.Disabled:
                    // Caustics
                    _causticMaterial.SetFloat(SrcBlend, 2f);
                    _causticMaterial.SetFloat(DstBlend, 0f);
                    _causticMaterial.DisableKeyword("_DEBUG");
                    m_CausticsPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                    // WaterEffects
                    break;
            }

            _causticMaterial.SetFloat(Size, settings.causticScale);
            m_CausticsPass.WaterCausticMaterial = _causticMaterial;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.camera.name.Contains("Planar Reflections"))
            {
                renderer.EnqueuePass(m_InfiniteWaterPass);
                renderer.EnqueuePass(m_WaterFxPass);
                renderer.EnqueuePass(m_CausticsPass);
            }
        }

        /// <summary>
        /// This function Generates a flat quad for use with the caustics pass.
        /// </summary>
        /// <param name="size">The length of the quad.</param>
        /// <returns></returns>
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