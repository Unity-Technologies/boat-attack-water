using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace WaterSystem.Settings
{
    /// <summary>
    /// Resources for the system, shaders, textures meshes, etc
    /// </summary>
    [Serializable]
    public class Resources
    {
        public const string BasePath = "Packages/com.unity.urp-water-system/Runtime/";
        
        [Tooltip("Texture used for the foam.")]
        [SerializeField] private Texture2D foamMap; // a default foam texture map
        [SerializeField] private Texture2D surfaceMap; // a default normal/caustic map
        [SerializeField] private Texture2D detailNormalMap; // default normal map
        [SerializeField] private Texture2D waterFX; // texture with correct values for default WaterFX
        [SerializeField] private Texture2D ditherNoise; // blue noise normal map
        [SerializeField] private Mesh infiniteWaterMesh;
        [SerializeField] private Mesh waterTile; 
        [SerializeField] private Shader waterShader;
        [SerializeField] private Shader causticShader;
        [SerializeField] private Shader infiniteWaterShader;
        [SerializeField] private Shader waterDepthShader;
        
        public T GetResourceIfNull<T>(ref T resource, BuiltinAssets path) where T : UnityEngine.Object
        {
            if (resource == null)
            {
                resource = GetResource<T>(path);
            }
            return resource;
        }
        
        public Texture2D FoamMap
        {
            get => GetResourceIfNull(ref foamMap, BuiltinAssets.FoamMap);
            set => foamMap = value;
        }
        
        public Texture2D SurfaceMap
        {
            get => GetResourceIfNull(ref surfaceMap, BuiltinAssets.SurfaceMap);
            set => surfaceMap = value;
        }

        public Texture2D DetailNormalMap
        {
            get => GetResourceIfNull(ref detailNormalMap, BuiltinAssets.DetailNormalMap);
            set => detailNormalMap = value;
        }

        public Texture2D WaterFX
        {
            get => GetResourceIfNull(ref waterFX, BuiltinAssets.WaterFX);
            set => waterFX = value;
        }

        public Texture2D DitherNoise
        {
            get => GetResourceIfNull(ref ditherNoise, BuiltinAssets.DitherNoise);
            set => ditherNoise = value;
        }
        
        public Mesh WaterTile
        {
            get => GetResourceIfNull(ref waterTile, BuiltinAssets.WaterTile);
            set => waterTile = value;
        }

        public Mesh InfiniteWaterMesh
        {
            get => GetResourceIfNull(ref infiniteWaterMesh, BuiltinAssets.InfiniteWaterMesh);
            set => infiniteWaterMesh = value;
        }

        public Shader WaterShader
        {
            get => GetResourceIfNull(ref waterShader, BuiltinAssets.WaterShader);
            set => waterShader = value;
        }

        public Shader CausticShader
        {
            get => GetResourceIfNull(ref causticShader, BuiltinAssets.CausticShader);
            set => causticShader = value;
        }

        public Shader InfiniteWaterShader
        {
            get => GetResourceIfNull(ref infiniteWaterShader, BuiltinAssets.InfiniteWaterShader);
            set => infiniteWaterShader = value;
        }
        
        public Shader WaterDepthShader
        {
            get => GetResourceIfNull(ref waterDepthShader, BuiltinAssets.WaterDepthShader);
            set => waterDepthShader = value;
        }

#if UNITY_EDITOR
        public void Init()
        {

            // Textures
            FoamMap = GetResource<Texture2D>(BuiltinAssets.FoamMap);
        }
#endif
        
        

        [NonSerialized] public string ValidationString;
        
        public bool ValidateResources()
        {
            bool valid = true;
            ValidationString = "";
            
            // loop through all the properties, including private ones and check if they are null via refleciton, adding a line to the logString for each
            foreach (var propertyInfo in GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (propertyInfo.GetValue(this) == null)
                {
                    valid = false;
                    ValidationString += $"Resource {propertyInfo.Name} is null\n";
                }
                else
                {
                    ValidationString += $"Resource {propertyInfo.Name} is valid\n";
                }
            }
            
            if(valid)
                Debug.Log(ValidationString);
            else
                Debug.LogError(ValidationString);

            return valid;
        }

        private class AssetInfo
        {
            public readonly string Path;
            public readonly Type Type;
            
            public AssetInfo(string path, Type type)
            {
                Path = type != typeof(Shader) ? $"{BasePath}{path}" : path;
                Type = type;
            }
        }

        public enum BuiltinAssets
        {
            FoamMap,
            DetailNormalMap,
            SurfaceMap,
            WaterFX,
            DitherNoise,
            InfiniteWaterMesh,
            WaterTile,
            WaterShader,
            CausticShader,
            InfiniteWaterShader,
            WaterDepthShader
        }

        private Dictionary<BuiltinAssets, AssetInfo> _assetInfo = new()
        {
            {BuiltinAssets.FoamMap, new AssetInfo("Textures/WaterFoam.png", typeof(Texture2D))},
            {BuiltinAssets.DetailNormalMap, new AssetInfo("Textures/WaterNormals.tif", typeof(Texture2D))},
            {BuiltinAssets.SurfaceMap, new AssetInfo("Textures/WaterSurface_single.tif", typeof(Texture2D))},
            {BuiltinAssets.WaterFX, new AssetInfo("Textures/DefaultWaterFX.tif", typeof(Texture2D))},
            {BuiltinAssets.DitherNoise, new AssetInfo("Textures/normalNoise.png", typeof(Texture2D))},
            {BuiltinAssets.InfiniteWaterMesh, new AssetInfo("Meshes/InfiniteSea.fbx", typeof(Mesh))},
            {BuiltinAssets.WaterTile, new AssetInfo("Meshes/WaterTile.fbx", typeof(Mesh))},
            {BuiltinAssets.WaterShader, new AssetInfo("Boat Attack/Water", typeof(Shader))},
            {BuiltinAssets.CausticShader, new AssetInfo("Boat Attack/Water/Caustics", typeof(Shader))},
            {BuiltinAssets.InfiniteWaterShader, new AssetInfo("Boat Attack/Water/InfiniteWater", typeof(Shader))},
            {BuiltinAssets.WaterDepthShader, new AssetInfo("Boat Attack/Water/WaterBuffer/WaterDepthOnly", typeof(Shader))},
        };

        // method to return a resource based on the BuiltinAssets enum
        public T GetResource<T>(BuiltinAssets asset)
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(ProjectSettings.Instance);
            return _assetInfo[asset].Type switch
            {
                { } textureType when textureType == typeof(Texture2D) => (T) (object) AssetDatabase
                    .LoadAssetAtPath<Texture2D>(_assetInfo[asset].Path),
                { } meshType when meshType == typeof(Mesh) => (T) (object) AssetDatabase.LoadAssetAtPath<Mesh>(
                    _assetInfo[asset].Path),
                { } shaderType when shaderType == typeof(Shader) => (T) (object) Shader.Find(_assetInfo[asset].Path),
                _ => default
            };
#else
            return default(T);
#endif
        }
    }
}