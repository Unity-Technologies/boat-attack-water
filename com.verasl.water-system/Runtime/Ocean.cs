using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace WaterSystem
{
    [ExecuteAlways]
    public class Ocean : MonoBehaviour
    {
        // Singleton
        private static Ocean _instance;
        public static Ocean Instance
        {
            get
            {
                if (_instance == null)
                    _instance = (Ocean)FindObjectOfType(typeof(Ocean));
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
        [HideInInspector,SerializeField] private WaterResources resources;

        public DebugShading shadingDebug;
        
        // Render Passes
        private WaterSystemFeature.InfiniteWaterPass _infiniteWaterPass;
        private WaterSystemFeature.WaterFxPass _waterBufferPass;
        private WaterSystemFeature.WaterCausticsPass _causticsPass;
        
        // RuntimeMaterials
        private Material _causticMaterial;

        // Shader props
        private static readonly int CameraRoll = Shader.PropertyToID("_CameraRoll");
        private static readonly int InvViewProjection = Shader.PropertyToID("_InvViewProjection");
        private static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
        private static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        private static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
        private static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
        private static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
        private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
        private static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
        private static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");
        private static readonly int WaveData = Shader.PropertyToID("waveData");
        private static readonly int WaterFXShaderTag = Shader.PropertyToID("_WaterFXMap");
        private static readonly int BoatAttackWaterDebugPass = Shader.PropertyToID("_BoatAttack_Water_DebugPass");
        private static readonly int BoatAttackWaterDistanceBlend = Shader.PropertyToID("_BoatAttack_Water_DistanceBlend");
        private static readonly int AbsorptionColor = Shader.PropertyToID("_AbsorptionColor");
        private static readonly int ScatteringColor = Shader.PropertyToID("_ScatteringColor");
        private static readonly int BoatAttackWaterMicroWaveIntensity = Shader.PropertyToID("_BoatAttack_Water_MicroWaveIntensity");
        private static readonly int BoatAttackWaterFoamIntensity = Shader.PropertyToID("_BoatAttack_water_FoamIntensity");

        private void OnEnable()
        {
            #if UNITY_EDITOR
            if(resources == null)
            {
                var data = AssetDatabase.LoadAssetAtPath<WaterResources>("Packages/com.verasl.water-system/Runtime/Data/WaterResources.asset");
                resources = data;
            }
            #endif
            
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
            if (!computeOverride)
                _useComputeBuffer = SystemInfo.supportsComputeShaders &&
                                   Application.platform != RuntimePlatform.WebGLPlayer &&
                                   Application.platform != RuntimePlatform.Android;
            else
                _useComputeBuffer = false;
            Init();
        }

        private void OnDisable() {
            Cleanup();
        }

        private void OnApplicationQuit()
        {
            GerstnerWavesJobs.Cleanup();
        }

        void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            waveBuffer?.Dispose();
        }

        private void BeginCameraRendering(ScriptableRenderContext src, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview) return;

            PlanarReflections.Execute(src, cam, transform);

            if (_causticMaterial == null)
            {
                _causticMaterial = CoreUtils.CreateEngineMaterial(resources.causticShader);
                _causticMaterial.SetTexture("_CausticMap", resources.defaultSurfaceMap);
            }

            _infiniteWaterPass ??= new WaterSystemFeature.InfiniteWaterPass(resources.defaultInfinitewWaterMesh);
            _waterBufferPass ??= new WaterSystemFeature.WaterFxPass();
            _causticsPass ??= new WaterSystemFeature.WaterCausticsPass(_causticMaterial);

            var urpData = cam.GetUniversalAdditionalCameraData();
            urpData.scriptableRenderer.EnqueuePass(_infiniteWaterPass);
            urpData.scriptableRenderer.EnqueuePass(_waterBufferPass);
            urpData.scriptableRenderer.EnqueuePass(_causticsPass);
            
            var roll = cam.transform.localEulerAngles.z;
            Shader.SetGlobalFloat(CameraRoll, roll);
            Shader.SetGlobalMatrix(InvViewProjection,
                (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);

            // Water matrix
            const float quantizeValue = 6.25f;
            const float forwards = 10f;
            const float yOffset = -0.25f;

            var newPos = cam.transform.TransformPoint(Vector3.forward * forwards);
            newPos.y = yOffset;
            newPos.x = quantizeValue * (int) (newPos.x / quantizeValue);
            newPos.z = quantizeValue * (int) (newPos.z / quantizeValue);

            var matrix = Matrix4x4.TRS(newPos + transform.position, Quaternion.identity, transform.localScale); // transform.localToWorldMatrix;

            foreach (var mesh in resources.defaultWaterMeshes)
            {
                Graphics.DrawMesh(mesh,
                    matrix,
                    resources.defaultSeaMaterial,
                    gameObject.layer,
                    cam,
                    0,
                    null,
                    ShadowCastingMode.Off,
                    true,
                    null,
                    LightProbeUsage.Off,
                    null);
            }
        }

        private static void SafeDestroy(Object o)
        {
            if(Application.isPlaying)
                Destroy(o);
            else
                DestroyImmediate(o);
        }

        [ContextMenu("Init")]
        public void Init()
        {
            SetWaves();

            if (!gameObject.TryGetComponent(out _planarReflections))
            {
                _planarReflections = gameObject.AddComponent<PlanarReflections>();
            }
            _planarReflections.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            _planarReflections.enabled = settingsData.refType == Data.ReflectionType.PlanarReflection;
            PlanarReflections.m_planeOffset = transform.position.y;
            PlanarReflections.m_settings = settingsData.planarSettings;
            PlanarReflections.m_settings.m_ClipPlaneOffset = 0;//transform.position.y;

            if(resources == null)
            {
                resources = Resources.Load("WaterResources") as WaterResources;
            }

            if (shadingDebug != DebugShading.none)
            {
                Shader.EnableKeyword("_BOATATTACK_WATER_DEBUG");
            }
            else
            {
                Shader.DisableKeyword("_BOATATTACK_WATER_DEBUG");
            }
            Shader.SetGlobalInt(BoatAttackWaterDebugPass, (int)shadingDebug);
        }

        private void LateUpdate()
        {
            GerstnerWavesJobs.UpdateHeights();
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
            Shader.SetGlobalTexture(FoamMap, resources.defaultFoamMap);
            Shader.SetGlobalTexture(SurfaceMap, resources.defaultSurfaceMap);
            Shader.SetGlobalTexture(WaterFXShaderTag, resources.defaultWaterFX);

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

            switch(settingsData.refType)
            {
                case Data.ReflectionType.Cubemap:
                    Shader.EnableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    Shader.SetGlobalTexture(CubemapTexture, settingsData.cubemapRefType);
                    break;
                case Data.ReflectionType.ReflectionProbe:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.EnableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                case Data.ReflectionType.PlanarReflection:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.EnableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Shader.SetGlobalInt(WaveCount, waves.Length);

            //GPU side
            if (_useComputeBuffer)
            {
                Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
                waveBuffer?.Dispose();
                waveBuffer = new ComputeBuffer(10, (sizeof(float) * 6));
                waveBuffer.SetData(waves);
                Shader.SetGlobalBuffer(WaveDataBuffer, waveBuffer);
            }
            else
            {
                Shader.DisableKeyword("USE_STRUCTURED_BUFFER");
                Shader.SetGlobalVectorArray(WaveData, GetWaveData());
            }
            //CPU side
            if(GerstnerWavesJobs.Initialized == false && Application.isPlaying)
                GerstnerWavesJobs.Init();
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
                    var p = Mathf.Lerp(0.5f, 1.5f, i * r);
                    var amp = a * p * Random.Range(0.8f, 1.2f);
                    var dir = d + Random.Range(-90f, 90f);
                    var len = l * p * Random.Range(0.6f, 1.4f);
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
            WaterBufferA,
            WaterBufferB,
            Depth,
            WaterDepth
        }
    }
}
