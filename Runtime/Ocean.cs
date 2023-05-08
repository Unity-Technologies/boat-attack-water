using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;
using WaterSystem.Rendering;
using WaterSystem.Settings;

namespace WaterSystem
{
    [ExecuteAlways, DisallowMultipleComponent]
    [AddComponentMenu("URP Water System/Ocean")]
    public class Ocean : MonoBehaviour
    {
        private int lastQualityLevel = -1;
        // Singleton
        private static Ocean _instance;
        public static Ocean Instance
        {
            get
            {
                if (_instance == null)
                {
                    #if UNITY_2023_1_OR_NEWER
                    _instance = (Ocean) FindFirstObjectByType(typeof(Ocean));
                    #else
                    _instance = (Ocean) FindObjectOfType(typeof(Ocean));
                    #endif
                }

                return _instance;
            }
        }
        // Script references
        private PlanarReflections _planarReflections;

        private bool _useComputeBuffer;
        public bool computeOverride;
        
        [HideInInspector, SerializeField] public Data.Wave[] waves;

        private ComputeBuffer waveBuffer;
        private float _maxWaveHeight;
        private float _waveHeight;

        [SerializeReference] public Data.OceanSettings settingsData = new Data.OceanSettings();

        public DebugShading shadingDebug;

        private MeshSurface mesh;
        private MeshSurface.WaterMeshSettings meshSettings;
        
        // Render Passes
        private InfiniteWaterPlane _infiniteWaterPass;
        private WaterFxPass _waterBufferPass;
        private WaterCausticsPass _causticsPass;
        
        // RuntimeMaterials
        private Material _waterMaterial;
        private Material _infiniteWaterMaterial;
        private Material _causticMaterial;

        // Runttime Resources
        private Texture2D _rampTexture;
        
        // Shader props
        private static readonly int CameraRoll = Shader.PropertyToID("_CameraRoll");
        private static readonly int InvViewProjection = Shader.PropertyToID("_InvViewProjection");
        private static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
        private static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        private static readonly int SurfaceNormals = Shader.PropertyToID("_SurfaceNormals");
        private static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
        private static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
        private static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
        private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
        private static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
        private static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");
        private static readonly int WaveData = Shader.PropertyToID("waveData");
        private static readonly int WaterFXShaderTag = Shader.PropertyToID("_WaterFXMap");
        private static readonly int DitherTexture = Shader.PropertyToID("_DitherPattern");
        private static readonly int BoatAttackWaterDebugPass = Shader.PropertyToID("_BoatAttack_Water_DebugPass");
        private static readonly int BoatAttackWaterDistanceBlend = Shader.PropertyToID("_BoatAttack_Water_DistanceBlend");
        private static readonly int AbsorptionColor = Shader.PropertyToID("_AbsorptionColor");
        private static readonly int ScatteringColor = Shader.PropertyToID("_ScatteringColor");
        private static readonly int BoatAttackWaterMicroWaveIntensity = Shader.PropertyToID("_BoatAttack_Water_MicroWaveIntensity");
        private static readonly int BoatAttackWaterFoamIntensity = Shader.PropertyToID("_BoatAttack_water_FoamIntensity");
        private static readonly int RampTexture = Shader.PropertyToID("_BoatAttack_RampTexture");
        
        // Shader Keywords
        private static GlobalKeyword _volShadowsLow;
        private static GlobalKeyword _volShadowsMedium;
        private static GlobalKeyword _volShadowsHigh;
        private static GlobalKeyword _dispersion;
        
        private void OnEnable()
        {
            if (WaterProjectSettings.Instance == null || WaterProjectSettings.Instance.resources == null)
            {
                Debug.LogError($"No Water Settings Present, disabling {GetType()}");
                return;
            }

            if (_instance == null)
            {
                _instance = this;
            }
            else if(_instance != this)
            {
                Debug.LogError("Multiple Ocean Components cannot exist in tandem");
                //SafeDestroy(this);
            }
            
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            if (!computeOverride)
                _useComputeBuffer = SystemInfo.supportsComputeShaders &&
                                   Application.platform != RuntimePlatform.WebGLPlayer &&
                                   Application.platform != RuntimePlatform.Android;
            else
                _useComputeBuffer = false;
            Init();
        }

        private void OnDisable() 
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(_waterMaterial);
            CoreUtils.Destroy(_infiniteWaterMaterial);
            CoreUtils.Destroy(_causticMaterial);
            GerstnerWavesJobs.Cleanup();
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            mesh?.Cleanup();
            waveBuffer?.Dispose();
        }

        private void BeginCameraRendering(ScriptableRenderContext src, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview || _instance == null) return;

            if (WaterProjectSettings.QualitySettings.reflectionSettings.reflectionType == Data.ReflectionSettings.Type.PlanarReflection)
                PlanarReflections.Execute(src, cam, _instance.transform);
            
            var urpData = cam.GetUniversalAdditionalCameraData();

            if (WaterProjectSettings.QualitySettings.causticSettings.Mode != Data.CausticSettings.CausticMode.Off)
            {
                DoCaustics(urpData.scriptableRenderer);
            }

            _waterBufferPass ??= new WaterFxPass();
            urpData.scriptableRenderer.EnqueuePass(_waterBufferPass);

            if (_infiniteWaterMaterial == null)
            {
                _infiniteWaterMaterial = CoreUtils.CreateEngineMaterial(WaterProjectSettings.Instance.resources.infiniteWaterShader);
            }

            _infiniteWaterPass ??= new InfiniteWaterPlane(_infiniteWaterMaterial);
            urpData.scriptableRenderer.EnqueuePass(_infiniteWaterPass);

#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
            // inject dummy pass later in the frame to keep resources alive
            urpData.scriptableRenderer.EnqueuePass(new PassUtilities.DummyResourcePass(new[]
                    {PassUtilities.WaterResources.BufferA, PassUtilities.WaterResources.BufferB},
                RenderPassEvent.AfterRenderingTransparents));
#endif
            
            var roll = cam.transform.localEulerAngles.z;
            Shader.SetGlobalFloat(CameraRoll, roll);
            Shader.SetGlobalMatrix(InvViewProjection,
                (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);

            // Water matrix
            const float quantizeValue = 6.25f;
            const float forwards = 10f;
            const float yOffset = -0.25f;

            var newPos = cam.transform.TransformPoint(Vector3.forward * forwards);
            newPos.y = yOffset + _instance.transform.position.y;
            newPos.x = quantizeValue * (int) (newPos.x / quantizeValue);
            newPos.z = quantizeValue * (int) (newPos.z / quantizeValue);

            mesh.transformMatrix = transform.localToWorldMatrix;
            
            if (_waterMaterial == null)
            {
                _waterMaterial = CoreUtils.CreateEngineMaterial(WaterProjectSettings.Instance.resources.waterShader);
                _waterMaterial.enableInstancing = true;
            }
            
            mesh.GenerateSurface(ref meshSettings, ref cam, ref _waterMaterial, gameObject.layer);
        }

        private void DoCaustics(ScriptableRenderer renderer)
        {
            if (_causticMaterial == null)
            {
                _causticMaterial = CoreUtils.CreateEngineMaterial(WaterProjectSettings.Instance.resources.causticShader);
                _causticMaterial.SetTexture("_CausticMap", WaterProjectSettings.Instance.resources.surfaceMap);
            }
            _causticsPass ??= new WaterCausticsPass(_causticMaterial);
            renderer.EnqueuePass(_causticsPass);
        }

        [ContextMenu("Init")]
        public void Init()
        {
            
            
            GenerateColorRamp();
            SetWaves();

            mesh?.Cleanup();
            mesh = new MeshSurface();
            meshSettings = MeshSurface.NewMeshSettings();
            
            meshSettings.infinite = true;
            meshSettings.maxDivisions = 5;
            meshSettings.baseTileSize = 20;
            meshSettings.density = 0.15f;
            meshSettings.size = new float2(350, 0);
            meshSettings.maxWaveHeight = _maxWaveHeight;

            PlanarReflections.m_planeOffset = transform.position.y;
            
            _volShadowsLow = GlobalKeyword.Create("_SHADOW_SAMPLES_LOW");
            _volShadowsMedium = GlobalKeyword.Create("_SHADOW_SAMPLES_MEDIUM");
            _volShadowsHigh = GlobalKeyword.Create("_SHADOW_SAMPLES_HIGH");
            _dispersion = GlobalKeyword.Create("_DISPERSION");
            
            SetDebugMode(shadingDebug);
            
            //CPU side
            if(GerstnerWavesJobs.Initialized == false)
                GerstnerWavesJobs.Init();
        }

        private void LateUpdate()
        {
            GerstnerWavesJobs.UpdateHeights();
            // remove with callback
            if (QualitySettings.GetQualityLevel() != lastQualityLevel)
            {
                Init();
                lastQualityLevel = QualitySettings.GetQualityLevel();
            }
        }

        public static void SetDebugMode(DebugShading mode)
        {
            if (mode != DebugShading.none)
            {
                Shader.EnableKeyword("BOAT_ATTACK_WATER_DEBUG_DISPLAY");
                Shader.SetGlobalInt(BoatAttackWaterDebugPass, (int)mode);
            }
            else
            {
                Shader.DisableKeyword("BOAT_ATTACK_WATER_DEBUG_DISPLAY");
            }
        }

        public void FragWaveNormals(bool toggle)
        {
            var mat = GetComponent<Renderer>().sharedMaterial;
            if (toggle)
                mat.EnableKeyword("GERSTNER_WAVES");
            else
                mat.DisableKeyword("GERSTNER_WAVES");
        }

        private void SetWaves()
        {
            SetupWaves(settingsData._customWaves);

            // set default resources
            Shader.SetGlobalTexture(FoamMap, WaterProjectSettings.Instance.resources.foamMap);
            Shader.SetGlobalTexture(SurfaceNormals, WaterProjectSettings.Instance.resources.detailNormalMap);
            Shader.SetGlobalTexture(WaterFXShaderTag, WaterProjectSettings.Instance.resources.waterFX);
            Shader.SetGlobalTexture(DitherTexture, WaterProjectSettings.Instance.resources.ditherNoise);

            _maxWaveHeight = 0f;
            foreach (var w in waves)
            {
                _maxWaveHeight += w.amplitude;
            }
            _maxWaveHeight /= waves.Length;

            _waveHeight = transform.position.y;

            Shader.SetGlobalColor(AbsorptionColor, settingsData._absorptionColor.gamma);
            Shader.SetGlobalColor(ScatteringColor, settingsData._scatteringColor.linear);
            
            Shader.SetGlobalFloat(WaveHeight, _waveHeight);
            Shader.SetGlobalFloat(BoatAttackWaterMicroWaveIntensity, settingsData._microWaveIntensity);
            Shader.SetGlobalFloat(MaxWaveHeight, _maxWaveHeight);
            Shader.SetGlobalFloat(MaxDepth, settingsData._waterMaxVisibility);
            Shader.SetGlobalFloat(BoatAttackWaterDistanceBlend, settingsData.distanceBlend);
            Shader.SetGlobalFloat(BoatAttackWaterFoamIntensity, settingsData._foamIntensity);
            
            foreach (Data.ReflectionSettings.Type reflect in Enum.GetValues(typeof(Data.ReflectionSettings.Type)))
            {
                if(WaterProjectSettings.QualitySettings.reflectionSettings.reflectionType == reflect)
                    Shader.EnableKeyword(Data.GetReflectionKeyword(reflect));
                else
                    Shader.DisableKeyword(Data.GetReflectionKeyword(reflect));
            }
            
            switch(WaterProjectSettings.QualitySettings.reflectionSettings.reflectionType)
            {
                case Data.ReflectionSettings.Type.Cubemap:
                    Shader.SetGlobalTexture(CubemapTexture, settingsData.cubemapRefType);
                    break;
                case Data.ReflectionSettings.Type.ScreenSpaceReflection:
                    Shader.SetGlobalTexture(CubemapTexture,
                        settingsData.cubemapRefType == null
                            ? ReflectionProbe.defaultTexture
                            : settingsData.cubemapRefType);
                    Vector3 settings = new Vector3(WaterProjectSettings.QualitySettings.ssrSettings.stepSize, 
                        WaterProjectSettings.QualitySettings.ssrSettings.thickness, 
                        0);
                    Shader.SetGlobalVector("_SSR_Settings", settings);
                    switch (WaterProjectSettings.QualitySettings.ssrSettings.steps)
                    {
                        case Data.SsrSettings.Steps.Low:
                            Shader.EnableKeyword("_SSR_SAMPLES_LOW");
                            Shader.DisableKeyword("_SSR_SAMPLES_MEDIUM");
                            Shader.DisableKeyword("_SSR_SAMPLES_HIGH");
                            break;
                        case Data.SsrSettings.Steps.Medium:
                            Shader.EnableKeyword("_SSR_SAMPLES_MEDIUM");
                            Shader.DisableKeyword("_SSR_SAMPLES_LOW");
                            Shader.DisableKeyword("_SSR_SAMPLES_HIGH");
                            break;
                        case Data.SsrSettings.Steps.High:
                            Shader.EnableKeyword("_SSR_SAMPLES_HIGH");
                            Shader.DisableKeyword("_SSR_SAMPLES_LOW");
                            Shader.DisableKeyword("_SSR_SAMPLES_MEDIUM");
                            break;
                    }
                    break;
            }
            
            
            var volumeShadows = WaterProjectSettings.QualitySettings.lightingSettings.Mode == Data.LightingSettings.LightingMode.Volume;
            if (volumeShadows)
            {
                var volumeShadowsQuality = WaterProjectSettings.QualitySettings.lightingSettings.VolumeSamples;
                Shader.SetKeyword(_volShadowsLow, volumeShadowsQuality == Data.LightingSettings.VolumeSample.Low);
                Shader.SetKeyword(_volShadowsMedium, volumeShadowsQuality == Data.LightingSettings.VolumeSample.Medium);
                Shader.SetKeyword(_volShadowsHigh, volumeShadowsQuality == Data.LightingSettings.VolumeSample.High);
            }
            else
            {
                Shader.DisableKeyword(_volShadowsLow);
                Shader.DisableKeyword(_volShadowsMedium);
                Shader.DisableKeyword(_volShadowsHigh);
            }

            var dispersion = WaterProjectSettings.QualitySettings.causticSettings.Dispersion;
            Shader.SetKeyword(_dispersion, dispersion);
            
            Shader.SetGlobalInt(WaveCount, waves.Length);

            //GPU side
            if (_useComputeBuffer)
            {
                Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
                waveBuffer?.Dispose();
                waveBuffer = new ComputeBuffer(WaveCount,  UnsafeUtility.SizeOf<Data.Wave>());
                waveBuffer.SetData(waves);
                Shader.SetGlobalBuffer(WaveDataBuffer, waveBuffer);
            }
            else
            {
                Shader.DisableKeyword("USE_STRUCTURED_BUFFER");
                Shader.SetGlobalVectorArray(WaveData, GetWaveData());
            }
        }
        
        private void GenerateColorRamp()
        {
            const int rampCount = 2;
            const int rampRes = 128;
            
            var pixelHeight = Mathf.CeilToInt(rampCount / 4.0f);
            
            if(_rampTexture == null)
                _rampTexture = new Texture2D(rampRes,  pixelHeight, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
            _rampTexture.wrapMode = TextureWrapMode.Clamp;
            
            // Foam shore
            var cols = new Color[rampRes * pixelHeight];
            for (var i = 0; i < rampRes; i++)
            {
                var val = settingsData._shoreFoamProfile.Evaluate(i / (float)rampRes);
                cols[i].r = Mathf.LinearToGammaSpace(val);
            }
            // Foam Gerstner waves
            for (var i = 0; i < rampRes; i++)
            {
                var val = settingsData._waveFoamProfile.Evaluate(i / (float)rampRes);
                cols[i].g = Mathf.LinearToGammaSpace(val);
            }
            // Depth Gerstner waves
            for (var i = 0; i < rampRes; i++)
            {
                var val = settingsData._waveDepthProfile.Evaluate(i / (float)rampRes);
                cols[i].b = Mathf.LinearToGammaSpace(val);
            }
            
            _rampTexture.SetPixels(cols);
            _rampTexture.Apply();
            Shader.SetGlobalTexture(RampTexture, _rampTexture);
        }

        private Vector4[] GetWaveData()
        {
            var waveData = new Vector4[20];
            for (var i = 0; i < waves.Length; i++)
            {
                waveData[i] = new Vector4(waves[i].amplitude, waves[i].direction, waves[i].wavelength, waves[i].onmiDir);
                waveData[i+10] = new Vector4(waves[i].origin.x, waves[i].origin.y, 0, 0);
            }
            return waveData;
        }

        private void SetupWaves(bool custom)
        {
            if(!custom)
            {
                //create basic waves based off basic wave settings
                var backupSeed = Random.state;
                Random.InitState(settingsData.randomSeed);
                var basicWaves = settingsData._basicWaveSettings;
                var a = basicWaves.amplitude;
                var d = basicWaves.direction;
                var l = basicWaves.wavelength;
                var numWave = basicWaves.waveCount;
                waves = new Data.Wave[numWave];

                var r = 1f / numWave;

                for (var i = 0; i < numWave; i++)
                {
                    var p = Mathf.Lerp(0.1f, 1.9f, i * r);
                    var amp = a * p * Random.Range(0.66f, 1.24f);
                    var dir = d + Random.Range(-90f, 90f);
                    var len = l * p * Random.Range(0.75f, 1.2f);
                    waves[i] = new Data.Wave(amp, dir, len, Vector2.zero, false);
                    Random.InitState(settingsData.randomSeed + i + 1);
                }
                Random.state = backupSeed;
            }
            else
            {
                waves = settingsData._waves.ToArray();
            }
        }

        [Serializable]
        public enum DebugMode { none, stationary, screen };
        
        [Serializable]
        public enum DebugShading
        {
            none,
            normalWS,
            Reflection,
            Refraction,
            Specular,
            SSS,
            Foam,
            FoamMask,
            WaterBufferA,
            WaterBufferB,
            Depth,
            WaterDepth,
            Fresnel,
            Mesh,
        }
    }
}
