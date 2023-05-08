using System;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Settings
{
    /// <summary>
    /// Resources for the system, shaders, textures meshes, etc
    /// </summary>
    [Serializable]
    public class Resources
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
}