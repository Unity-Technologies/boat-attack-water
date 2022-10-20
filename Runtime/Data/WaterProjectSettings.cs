using UnityEngine;

namespace WaterSystem.Settings
{
    public class WaterProjectSettings : ScriptableObject
    {
        private static WaterProjectSettings _instance;

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

        [SerializeField] public int m_Number = 42;
        [SerializeField] public string m_SomeString = "The answer to the universe";
        
    }
    
    public static class SettingsConsts
    {
        public static string Build = "/Resources/";
        public static string BuildRelativeFolder = "Assets" + Build;
        public static string AssetString = "WaterSystemSettings";
        
        public static string FullBuildPath => BuildRelativeFolder + AssetString;
    }
}