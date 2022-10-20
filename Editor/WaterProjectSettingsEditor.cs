using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Settings
{
    // Settings GUI
    public static class WaterSettingsGUI
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/Water System (Boat Attack)", SettingsScope.Project)
            {
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    var settings = GetSerializedSettings();
                    EditorGUILayout.PropertyField(settings.FindProperty("m_Number"), new GUIContent("My Number"));
                    EditorGUILayout.PropertyField(settings.FindProperty("m_SomeString"), new GUIContent("My String"));
                    if (settings.hasModifiedProperties)
                        settings.ApplyModifiedProperties();
                    //    SaveOnChange(settings);
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] {"Number", "Some String"})
            };

            return provider;
        }
        
        public static WaterProjectSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<WaterProjectSettings>(SettingsConsts.FullBuildPath + ".asset");
            if (settings != null) return settings;
            Debug.LogError("Making new water asset");
            settings = ScriptableObject.CreateInstance<WaterProjectSettings>();
            AssetDatabase.CreateAsset(settings, SettingsConsts.FullBuildPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        [InitializeOnLoadMethod]
        public static void LoadEditorSettingsAsset()
        {
            if (!Application.isPlaying)
            {
                WaterProjectSettings.Instance = GetOrCreateSettings();
            }
        }
    }
}