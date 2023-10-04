using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_2022_3_OR_NEWER
using UnityEngine.Experimental.Rendering.RenderGraphModule;
#endif
using WaterSystem.Settings;

namespace WaterSystem.Rendering
{
    public static class Utilities
    {
        #region WaterShaderTools

        public static void GenerateColorRamp(ref Texture2D rampTexture, Water.Settings settingsData, string name = "Water")
        {
            const int rampCount = 2;
            const int rampRes = 128;
            
            var pixelHeight = Mathf.CeilToInt(rampCount / 4.0f);

            if (rampTexture == null)
            {
                rampTexture = new Texture2D(rampRes, pixelHeight, GraphicsFormat.R8G8B8A8_SRGB,
                    TextureCreationFlags.None)
                {
                    name = name + "_RampTexture",
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            
            // Foam shore
            var cols = new Color[rampRes * pixelHeight];
            for (var i = 0; i < rampRes; i++)
            {
                var val = settingsData.shoreFoamProfile.Evaluate(i / (float)rampRes);
                cols[i].r = Mathf.LinearToGammaSpace(val);
            }
            // Foam Gerstner waves
            for (var i = 0; i < rampRes; i++)
            {
                var val = settingsData.waveFoamProfile.Evaluate(i / (float)rampRes);
                cols[i].g = Mathf.LinearToGammaSpace(val);
            }
            // Depth Gerstner waves
            for (var i = 0; i < rampRes; i++)
            {
                var val = settingsData.waveDepthProfile.Evaluate(i / (float)rampRes);
                cols[i].b = Mathf.LinearToGammaSpace(val);
            }
            
            rampTexture.SetPixels(cols);
            rampTexture.Apply();
        }
        
        // public static method that sets up some global shader settings for the water
        public static void SetupMaterialProperties()
        {
            // set default resources
            Shader.SetGlobalTexture(ShaderIDs.FoamMap, ProjectSettings.Instance.resources.FoamMap);
            Shader.SetGlobalTexture(ShaderIDs.SurfaceNormals, ProjectSettings.Instance.resources.DetailNormalMap);
            Shader.SetGlobalTexture(ShaderIDs.WaterFXShaderTag, ProjectSettings.Instance.resources.WaterFX);
            Shader.SetGlobalTexture(ShaderIDs.DitherTexture, ProjectSettings.Instance.resources.DitherNoise);
            
            // set reflection keywords
            var reflectionType = ProjectSettings.Quality.reflectionSettings.reflectionType;
            foreach (Data.ReflectionSettings.Type reflect in Enum.GetValues(typeof(Data.ReflectionSettings.Type)))
            {
                var globalKeyword = ShaderKeywords.GetReflectionKeyword(reflect);
                Shader.SetKeyword(in globalKeyword, reflectionType == reflect);
            }
            if(reflectionType == Data.ReflectionSettings.Type.ScreenSpaceReflection)
            {
                Shader.SetGlobalVector(ShaderIDs.SsrSettings, (Vector3)ProjectSettings.Quality.reflectionSettings.ssrSettings.GetPacked());
                var ssrStep = ProjectSettings.Quality.reflectionSettings.ssrSettings.steps;
                foreach (Data.SsrSettings.Steps step in Enum.GetValues(typeof(Data.SsrSettings.Steps)))
                {
                    var globalKeyword = ShaderKeywords.GetSsrKeyword(step);
                    Shader.SetKeyword(in globalKeyword, ssrStep == step);
                }
            }
            
            // set shadow quality keywords
            if (ProjectSettings.Quality.lightingSettings.Mode == Data.LightingSettings.LightingMode.Volume)
            {
                var volumeSamples = ProjectSettings.Quality.lightingSettings.VolumeSamples;
                foreach (Data.LightingSettings.VolumeSample samples in Enum.GetValues(typeof(Data.LightingSettings.VolumeSample)))
                {
                    var globalKeyword = ShaderKeywords.GetVolumeShadowKeyword(samples);
                    Shader.SetKeyword(in globalKeyword, volumeSamples == samples);
                }
            }
            else
            {
                Shader.SetKeyword(in ShaderKeywords.KeyShadowsLow, false);
                Shader.SetKeyword(in ShaderKeywords.KeyShadowsMedium, false);
                Shader.SetKeyword(in ShaderKeywords.KeyShadowsHigh, false);
            }
            
            // set caustics keywords
            Shader.SetKeyword(in ShaderKeywords.KeyCausticDispersion, ProjectSettings.Quality.causticSettings.Dispersion);
        }

        #endregion
        
        #region ShaderKeywords

        public static class ShaderKeywords
        {
            // Reflection keywords
            private static readonly GlobalKeyword KeyRefCubemap = GlobalKeyword.Create("_REFLECTION_CUBEMAP");
            private static readonly GlobalKeyword KeyRefProbe = GlobalKeyword.Create("_REFLECTION_PROBE");
            private static readonly GlobalKeyword KeyRefPlanar = GlobalKeyword.Create("_REFLECTION_PLANARREFLECTION");
            private static readonly GlobalKeyword KeyRefSSR = GlobalKeyword.Create("_REFLECTION_SSR");
            
            // SSR Keywords
            private static readonly GlobalKeyword KeySsrLow = GlobalKeyword.Create("_SSR_SAMPLES_LOW");
            private static readonly GlobalKeyword KeySsrMedium = GlobalKeyword.Create("_SSR_SAMPLES_MEDIUM");
            private static readonly GlobalKeyword KeySsrHigh = GlobalKeyword.Create("_SSR_SAMPLES_HIGH");
            
            // Volume shadow keywords
            public static readonly GlobalKeyword KeyShadowsLow = GlobalKeyword.Create("_SHADOW_SAMPLES_LOW");
            public static readonly GlobalKeyword KeyShadowsMedium = GlobalKeyword.Create("_SHADOW_SAMPLES_MEDIUM");
            public static readonly GlobalKeyword KeyShadowsHigh = GlobalKeyword.Create("_SHADOW_SAMPLES_HIGH");
            
            // caustics keywords
            public static readonly GlobalKeyword KeyCausticDispersion = GlobalKeyword.Create("_DISPERSION");
            
            // method to retrieve the correct reflection keyword from a given reflection mode
            public static GlobalKeyword GetReflectionKeyword(Data.ReflectionSettings.Type mode)
            {
                return mode switch
                {
                    Data.ReflectionSettings.Type.Cubemap => KeyRefCubemap,
                    Data.ReflectionSettings.Type.PlanarReflection => KeyRefPlanar,
                    Data.ReflectionSettings.Type.ReflectionProbe => KeyRefProbe,
                    Data.ReflectionSettings.Type.ScreenSpaceReflection => KeyRefSSR,
                    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
                };
            }
            
            // method to retrieve the correct SSR keyword from a given SSR mode
            public static GlobalKeyword GetSsrKeyword(Data.SsrSettings.Steps mode)
            {
                return mode switch
                {
                    Data.SsrSettings.Steps.Low => KeySsrLow,
                    Data.SsrSettings.Steps.Medium => KeySsrMedium,
                    Data.SsrSettings.Steps.High => KeySsrHigh,
                    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
                };
            }
            
            // method to retrieve the correct volume shadow keyword from a given volume sample mode
            public static GlobalKeyword GetVolumeShadowKeyword(Data.LightingSettings.VolumeSample mode)
            {
                return mode switch
                {
                    Data.LightingSettings.VolumeSample.Low => KeyShadowsLow,
                    Data.LightingSettings.VolumeSample.Medium => KeyShadowsMedium,
                    Data.LightingSettings.VolumeSample.High => KeyShadowsHigh,
                    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
                };
            }
        }
        
        #endregion
        
        #region ShaderProperties

        public static class ShaderIDs
        {
            public static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
            public static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
            public static readonly int SurfaceNormals = Shader.PropertyToID("_SurfaceNormals");
            public static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
            public static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
            public static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
            public static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
            public static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
            public static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");
            public static readonly int WaveData = Shader.PropertyToID("waveData");
            public static readonly int WaterFXShaderTag = Shader.PropertyToID("_WaterFXMap");
            public static readonly int DitherTexture = Shader.PropertyToID("_DitherPattern");
            public static readonly int BoatAttackWaterDebugPass = Shader.PropertyToID("_BoatAttack_Water_DebugPass");
            public static readonly int BoatAttackWaterDistanceBlend = Shader.PropertyToID("_BoatAttack_Water_DistanceBlend");
            public static readonly int AbsorptionColor = Shader.PropertyToID("_AbsorptionColor");
            public static readonly int ScatteringColor = Shader.PropertyToID("_ScatteringColor");
            public static readonly int BoatAttackWaterMicroWaveIntensity = Shader.PropertyToID("_BoatAttack_Water_MicroWaveIntensity");
            public static readonly int BoatAttackWaterFoamIntensity = Shader.PropertyToID("_BoatAttack_water_FoamIntensity");
            public static readonly int RampTexture = Shader.PropertyToID("_BoatAttack_RampTexture");    
            public static readonly int SsrSettings = Shader.PropertyToID("_SSR_Settings");    
            public static readonly int DepthMap = Shader.PropertyToID("_Depth");
        }
        
        #endregion
        
        #region PassUtilities
    
        private static Mesh _causticMesh;

#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
        public class WaterResourceData : ContextItem
        {
            public TextureHandle BufferA;
            public TextureHandle BufferB;
            
            public override void Reset()
            {
                BufferA = TextureHandle.nullHandle;
                BufferB = TextureHandle.nullHandle;
            }
        }
#endif

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
            
            // add UVs
            var uvs = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            _causticMesh.uv = uvs;

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
        
        public static TextureHandle CreateRenderGraphTexture(RenderGraph renderGraph, RenderTextureDescriptor desc, string name, bool clear,
            FilterMode filterMode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Clamp, Color clearColor = default)
        {
            TextureDesc rgDesc = new TextureDesc(desc.width, desc.height);
            rgDesc.dimension = desc.dimension;
            rgDesc.clearBuffer = clear;
            rgDesc.bindTextureMS = desc.bindMS;
            rgDesc.colorFormat = desc.graphicsFormat;
            rgDesc.clearColor = clearColor;
            rgDesc.depthBufferBits = (DepthBits)desc.depthBufferBits;
            rgDesc.slices = desc.volumeDepth;
            rgDesc.msaaSamples = (MSAASamples)desc.msaaSamples;
            rgDesc.name = name;
            rgDesc.enableRandomWrite = desc.enableRandomWrite;
            rgDesc.filterMode = filterMode;
            rgDesc.wrapMode = wrapMode;
            rgDesc.isShadowMap = desc.shadowSamplingMode != ShadowSamplingMode.None && desc.depthStencilFormat != GraphicsFormat.None;
            // TODO RENDERGRAPH: depthStencilFormat handling?

            return renderGraph.CreateTexture(rgDesc);
        }
        
        internal class DummyResourcePass : ScriptableRenderPass
        {
            private string _passName;

            public DummyResourcePass(RenderPassEvent injectionPoint = RenderPassEvent.AfterRendering, string passName = "DummyResourcePass")
            {
                renderPassEvent = injectionPoint;
                _passName = passName;
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // N/A
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer container)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(_passName, out _, profilingSampler))
                {
                    builder.AllowPassCulling(false);

                    WaterResourceData resourceData = container.Get<WaterResourceData>();
                    builder.UseTexture(resourceData.BufferA);
                    builder.UseTexture(resourceData.BufferB);

                    builder.SetRenderFunc((PassData _, RasterGraphContext _) => { });
                }
            }

        }

#endif
        
        #endregion
    }
}