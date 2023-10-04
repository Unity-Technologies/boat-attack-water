using System.Collections.Generic;
using UnityEngine;

namespace WaterSystem.Settings
{
    public class ProjectSettings : ScriptableObject
    {
        private static ProjectSettings _instance;

        /// <summary>
        /// The current instance of the WaterSystems Project Settings, this contains the base resources and also the quality settings for each level
        /// </summary>
        public static ProjectSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = UnityEngine.Resources.Load<ProjectSettings>(SettingsConsts.AssetString);
                return _instance;
            }
            internal set => _instance = value;
        }
        
        /// <summary>
        /// Default base WaterQualitySettings.
        /// </summary>
        public Quality defaultQuality;

        /// <summary>
        /// List of WaterQualitySettings for all the Quality Levels
        /// </summary>
        public List<Quality> qualitySettings = new List<Quality>();

        /// <summary>
        /// Resources for the WaterSystem, contains shaders, meshes, textures, etc..
        /// </summary>
        public Resources _resources;
        
        public ProjectSettings()
        {
            defaultQuality = new Quality();
        }

        private void OnEnable()
        {
            _instance = this;
        }

        /// <summary>
        /// Returns the WaterQualitySettings that matches the current Quality Level.
        /// </summary>
        public static Quality Quality
        {
            get
            {
                var qualityLevel = UnityEngine.QualitySettings.GetQualityLevel();
                return GetQualitySettings(qualityLevel);
            }
        }

        public static Quality GetQualitySettings(int index)
        {
            if (_instance.qualitySettings.Count == 0 || _instance.qualitySettings.Count < index) return _instance.defaultQuality;
            return _instance.qualitySettings[index] ?? _instance.defaultQuality;
        }

        public Resources resources
        {
            get
            {
                if (_resources != null)
                {
                    return _resources;
                }
                
                _resources = new Resources();
                return _resources;
            }
        }
    }
    
    public static class SettingsConsts
    {
        public const string Build = "Resources";
        public const string AssetFolder = "Assets";
        public static string BuildRelativeFolder => AssetFolder + "/" + Build;
        public const string AssetString = "WaterSystemSettings";
        private const string ext = ".asset";
        public static string FullBuildPath => BuildRelativeFolder + "/" + AssetString + ext;
    }
}