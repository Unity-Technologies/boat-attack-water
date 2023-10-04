using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using WaterSystem.Rendering;
using WaterSystem.Settings;

namespace WaterSystem
{
    /// <summary>
    /// This can be used to create something like an Ocean or large body of water.
    /// Currently not recommended for rivers or streams.
    /// </summary>
    public class Water : WaterBody // TODO rename to Ocean
    {
        #region Properties
        
        // Water Settings
        public Settings settings = new Settings();
        
        // Temp Data
        private TempData _tempData = new();

        // Mesh Surface
        private MeshSurface _meshSurface;
        private MeshSurface.WaterMeshSettings _meshSettings;
        
        // Materials
        private Material _waterMaterial;
        
        // Passes
        private InfiniteWaterPlane _infiniteWaterPlanePass;
        
        // Serialized Modifier Data
        public GerstnerWaves.Data gerstnerData = new();
        //public Depth.DepthData depthData = new(); // TODO - add back once work done here for better depth setup.

        #endregion

        #region Initialization

        protected override void Setup()
        {
            base.Setup();
            InitializeData();
        }
        
        private void InitializeData()
        {
            _tempData.Initialize(settings, name);
            _meshSurface ??= new MeshSurface();
            //depthData.Initialize(this, transform.localToWorldMatrix); // TODO - add back once work done here for better depth setup.
            CreateMeshSettings();
        }

        #endregion

        #region Update

        public override void Render(Camera cam, ScriptableRenderer scriptableRenderer)
        {
            if(shape.type == WaterShapeType.Infinite)
                DrawInfiniteWaterPlane(scriptableRenderer);
            
            DrawMeshSurface(cam);
        }
        
        private void DrawInfiniteWaterPlane(ScriptableRenderer scriptableRenderer)
        {
            _infiniteWaterPlanePass ??= new InfiniteWaterPlane();
            scriptableRenderer.EnqueuePass(_infiniteWaterPlanePass);
        }

        #endregion

        #region Tear Down

        protected override void Cleanup()
        {
            base.Cleanup();
            _tempData.Cleanup();
            _meshSurface?.Cleanup();
        }

        #endregion
        
        #region Modifiers

        protected override void SetupModifiers()
        {
            if(!Modifiers.ContainsKey(typeof(GerstnerWaves.Data)))
                Modifiers.Add(typeof(GerstnerWaves.Data) , gerstnerData);
            //if(!Modifiers.ContainsKey(typeof(Depth.DepthData))) // TODO - add back once work done here for better depth setup.
            //    Modifiers.Add(typeof(Depth.DepthData) , depthData);
        }

        public override Type[] GetModifierTypes()
        {
            return new[] { typeof(GerstnerWaves) };
        }

        #endregion

        #region Utility

        private Material depthMat;
        
        private void DrawMeshSurface(Camera cam)
        {
            if (_meshSurface == null)
            {
                Debug.LogError("Mesh Surface is null, please check if the WaterBody is setup correctly.", gameObject);
                return;
            }
            _meshSurface.transformMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            if (_waterMaterial == null)
            {
                _waterMaterial = CoreUtils.CreateEngineMaterial(ProjectSettings.Instance.resources.WaterShader);
                _waterMaterial.enableInstancing = true;
            }
            SetMaterialProperties();
            _meshSurface.GenerateSurface(ref _meshSettings, ref cam, ref _waterMaterial, gameObject.layer);
        }
        
        private void DrawDepthMesh(Camera cam)
        {
            // get the unitMesh from the WaterManager
            var unitMesh = WaterManager.GetUnitQuad();
            // draw the mesh surface to fit the water shape
            var matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(shape.size.x, 1f, shape.size.y));
            if(depthMat == null)
                depthMat = CoreUtils.CreateEngineMaterial(ProjectSettings.Instance.resources.WaterDepthShader);
            //depthMat.SetTexture(Utilities.ShaderIDs.DepthMap, depthData.DepthMap); // TODO - add back once work done here for better depth setup.
            Graphics.DrawMesh(unitMesh, matrix, depthMat, gameObject.layer);
        }

        private void SetMaterialProperties()
        {
            // Material setup
            Material infiniteMat = null;
            if(_infiniteWaterPlanePass?.Material)
                infiniteMat = _infiniteWaterPlanePass.Material;
            
            // TODO - missing these
            // wave height - used for the Y level of the infinite water plane
            // max wave height - used for normalizing the wave height in shader and also SSS
            _waterMaterial.SetTexture(Utilities.ShaderIDs.RampTexture, _tempData.RampTexture);
            infiniteMat?.SetTexture(Utilities.ShaderIDs.RampTexture, _tempData.RampTexture);

            _waterMaterial.SetColor(Utilities.ShaderIDs.AbsorptionColor, settings.absorptionColor);
            infiniteMat?.SetColor(Utilities.ShaderIDs.AbsorptionColor, settings.absorptionColor);
            _waterMaterial.SetColor(Utilities.ShaderIDs.ScatteringColor, settings.scatteringColor);
            infiniteMat?.SetColor(Utilities.ShaderIDs.ScatteringColor, settings.scatteringColor);

            _waterMaterial.SetFloat(Utilities.ShaderIDs.BoatAttackWaterMicroWaveIntensity,
                settings.microWaveIntensity);
            infiniteMat?.SetFloat(Utilities.ShaderIDs.BoatAttackWaterMicroWaveIntensity,
                settings.microWaveIntensity);
            _waterMaterial.SetFloat(Utilities.ShaderIDs.MaxDepth, settings.waterMaxVisibility);
            infiniteMat?.SetFloat(Utilities.ShaderIDs.MaxDepth, settings.waterMaxVisibility);

            var distanceBlend = shape.type == WaterShapeType.Infinite ? settings.distanceBlend : float.MaxValue;
            _waterMaterial.SetFloat(Utilities.ShaderIDs.BoatAttackWaterDistanceBlend, distanceBlend);
            infiniteMat?.SetFloat(Utilities.ShaderIDs.BoatAttackWaterDistanceBlend, distanceBlend);
            _waterMaterial.SetFloat(Utilities.ShaderIDs.BoatAttackWaterFoamIntensity, settings.foamIntensity);
            infiniteMat?.SetFloat(Utilities.ShaderIDs.BoatAttackWaterFoamIntensity, settings.foamIntensity);
            
            _waterMaterial.SetTexture(Utilities.ShaderIDs.CubemapTexture, settings.cubemapTexture);
            _waterMaterial.SetInt(Utilities.ShaderIDs.WaveCount, gerstnerData.GetWaveCount());
            
            //gerstner wave setup
            gerstnerData.SetShaderProperties(ref _waterMaterial, gerstnerData);
        }
        
        // method to create and setup the WaterMeshSettings with the provided WaterShape
        private void CreateMeshSettings()
        {
            _meshSettings = new MeshSurface.WaterMeshSettings
            {
                maxWaveHeight = 10f, // TODO - hardcoded values, these should be calculated based on the water settings?
                maxDivisions = 5, // TODO - hardcoded values, these should be settings in project settings
                density = 0.15f
            };

            switch (shape.type)
            {
                case WaterShapeType.Infinite:
                    _meshSettings.infinite = true;
                    _meshSettings.size = new float2(settings.distanceBlend * 2f, 0f);
                    _meshSettings.baseTileSize = 20;
                    break;
                case WaterShapeType.Plane:
                    _meshSettings.infinite = false;
                    _meshSettings.size = shape.size;
                    _meshSettings.baseTileSize = GetTileSize(shape.size);
                    break;
                case WaterShapeType.Circle:
                    _meshSettings.infinite = false;
                    _meshSettings.size = new float2(shape.Radius * 2f, 0f);
                    _meshSettings.baseTileSize = Mathf.Clamp((int)this.shape.Radius / 4, 1, 20);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        /// <summary>
        /// Get the tile size based on the shortest side of a plane
        /// </summary>
        /// <param name="area">The area of the plane</param>
        /// <returns>The tile size in world units</returns>
        private int GetTileSize(float2 area)
        {
            int tileSize;
            // find a tile size that can fit into the area where the shortest side is at least 4 tiles
            tileSize = area.x < area.y ? Mathf.Clamp((int)area.x / 4, 1, 20) : Mathf.Clamp((int)area.y / 4, 1, 20);
            // return a clamped tile size between 1 and 20
            return Mathf.Clamp(tileSize, 1, 20);
        }

        #endregion
        
        #region Data

        [Serializable]
        public class Settings
        {
            // general
            public float distanceBlend = 100.0f;
            
            // Visual Surface
            public float waterMaxVisibility = 5.0f;
            public Color absorptionColor = new Color(0.2f, 0.6f, 0.8f);
            public Color scatteringColor = new Color(0.0f, 0.15f, 0.2f);
            public Texture2D cubemapTexture;
        
            // Waves
            public AnimationCurve waveFoamProfile = AnimationCurve.Linear(0.02f, 0f, 0.98f, 1f);
            public AnimationCurve waveDepthProfile = AnimationCurve.Linear(0.0f, 1f, 0.98f, 0f);
            public AnimationCurve shoreFoamProfile = AnimationCurve.Linear(0.02f, 0f, 0.98f, 1f);

            // Micro(surface) Waves
            public float microWaveIntensity = 0.25f;
            
            // Shore
            public float foamIntensity = 0.5f;
        }

        private class TempData
        {
            [NonSerialized] public Texture2D RampTexture;
            
            public void Initialize(Settings settings, string name)
            {
                if (RampTexture == null)
                {
                    Utilities.GenerateColorRamp(ref RampTexture, settings, name);
                }
            }
            
            public void Cleanup()
            {
                if (RampTexture != null)
                {
                    CoreUtils.Destroy(RampTexture);
                    RampTexture = null;
                }
            }
        }
        
        #endregion

        #region Debug

        public override void OnDrawGizmos()
        {
            base.OnDrawGizmos();
            // draw the water shape
            DrawShapeDebug();
        }

        private void DrawShapeDebug()
        {
            #if UNITY_EDITOR
            Gizmos.color = Color.cyan;
            Handles.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            switch (shape.type)
            {
                case WaterShapeType.Infinite:
                    break;
                case WaterShapeType.Plane:
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(shape.size.x, 0f, shape.size.y));
                    break;
                case WaterShapeType.Circle:
                    Handles.DrawWireDisc(transform.position, Vector3.up, shape.Radius);
                    break;
            }
            #endif
        }

        #endregion
    }
}