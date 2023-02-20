using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Settings
{
    public class WaterProjectSettings : ScriptableObject
    {
        private static WaterProjectSettings _instance;

        /// <summary>
        /// The current instance of the WaterSystems Project Settings, this contains the base resources and also the quality settings for each level
        /// </summary>
        public static WaterProjectSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<WaterProjectSettings>(SettingsConsts.AssetString);
                return _instance;
            }
            set => _instance = value;
        }

        public WaterProjectSettings()
        {
            resources = new WaterResources();
        }
        
        /// <summary>
        /// Default base WaterQualitySettings.
        /// </summary>
        [SerializeReference] public WaterQualitySettings defaultQualitySettings;

        /// <summary>
        /// List of WaterQualitySettings for all the Quality Levels
        /// </summary>
        [SerializeReference] public List<WaterQualitySettings> qualitySettings;

        /// <summary>
        /// Resources for the WaterSystem, contains shaders, meshes, textures, etc..
        /// </summary>
        [SerializeReference] public WaterResources resources;

        /// <summary>
        /// Returns the WaterQualitySettings that matches the current Quality Level.
        /// </summary>
        public static WaterQualitySettings QualitySettings
        {
            get
            {
                var qualityLevel = UnityEngine.QualitySettings.GetQualityLevel();
                return _instance.qualitySettings[qualityLevel] ?? _instance.defaultQualitySettings;
            }
        }
    }
    
    /// <summary>
    /// Resources for the system, shaders, textures meshes, etc
    /// </summary>
    [Serializable]
    public class WaterResources 
    {
        [Tooltip("Texture used for the foam.")]
        public Texture2D foamMap; // a default foam texture map
        public Texture2D surfaceMap; // a default normal/caustic map
        public Texture2D detailNormalMap; // default normal map
        public Texture2D waterFX; // texture with correct values for default WaterFX
        public Texture2D ditherNoise; // blue noise normal map
        public Mesh infiniteWaterMesh;
        public Mesh waterTile;
        public Shader waterShader;
        public Shader causticShader;
        public Shader infiniteWaterShader;
        
        public void Init()
        {
#if UNITY_EDITOR
            const string basePath = "Packages/com.unity.urp-water-system/Runtime/";
            // Textures
            foamMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "Textures/WaterFoam.png");
            detailNormalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "Textures/WaterNormals.tif");
            surfaceMap = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "Textures/WaterSurface_single.tif");
            waterFX = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "Textures/DefaultWaterFX.tif");
            ditherNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "Textures/normalNoise.png");
            // Materials
            // Meshes
            infiniteWaterMesh = AssetDatabase.LoadAssetAtPath<Mesh>(basePath + "Meshes/InfiniteSea.fbx");
            waterTile = AssetDatabase.LoadAssetAtPath<Mesh>(basePath + "Meshes/WaterTile.fbx");
            // Shaders
            waterShader = Shader.Find("Boat Attack/Water");
            causticShader = Shader.Find("Boat Attack/Water/Caustics");
            infiniteWaterShader = Shader.Find("Boat Attack/Water/InfiniteWater");
#endif
        }
    }
    
    public static class SettingsConsts
    {
        private const string Build = "/Resources/";
        private const string BuildRelativeFolder = "Assets" + Build;
        public const string AssetString = "WaterSystemSettings";
        private const string ext = ".asset";
        public static string FullBuildPath => BuildRelativeFolder + AssetString + ext;
    }
}