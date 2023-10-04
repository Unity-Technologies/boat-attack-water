using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Settings
{
    // Settings GUI
    public static class WaterSettingsGUI
    {
        private static int activeTab;

        // Group Foldout bools
        private static bool showResources;
        private static bool showDefault;
        private static bool[] showQualityLevel;
        
        // Raw data
        private static ProjectSettings rawData;
        
        // Serialized data
        private static SerializedObject settings;
        private static SerializedProperty resources;
        private static SerializedProperty defaultSettings;
        private static SerializedProperty[] qualitySettings;
        
        // Data
        private static string[] qualityNames;
        private static string[] tabStrings = new []{"Quality Settings", "Resources"};
        private static Vector2 scroll;
        private static string editorPrefPrefix = "BAW_WaterProjectSettings_";
        private static string editorPrefFoldout =  editorPrefPrefix + "Foldout_";

        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/Water System (Boat Attack)", SettingsScope.Project)
            {
                // onActivate
                activateHandler = (s, element) =>
                {
                    // Serialized data
                    settings = GetSerializedSettings();
                    resources = settings.FindProperty(nameof(ProjectSettings._resources));
                    defaultSettings = settings.FindProperty(nameof(ProjectSettings.defaultQuality));
                    
                    // string data
                    qualityNames = new string[QualitySettings.names.Length];
                    QualitySettings.names.CopyTo(qualityNames, 0);
                    
                    // Get all quality data
                    qualitySettings = new SerializedProperty[qualityNames.Length];
                    var tempProp = settings.FindProperty(nameof(ProjectSettings.qualitySettings));
                    for (var i = 0; i < qualitySettings.Length; i++)
                    {
                        if(tempProp.arraySize > i)
                            qualitySettings[i] = tempProp.GetArrayElementAtIndex(i);
                    }

                    showQualityLevel = new bool[qualityNames.Length];
                    for (var index = 0; index < showQualityLevel.Length; index++)
                    {
                        showQualityLevel[index] = EditorPrefs.GetBool(editorPrefFoldout + qualityNames[index]);
                    }
                },
                // onDeactivate
                deactivateHandler = () =>
                {
                    for (var index = 0; index < showQualityLevel?.Length; index++)
                    {
                        EditorPrefs.SetBool(editorPrefFoldout + qualityNames[index], showQualityLevel[index]);
                    }
                },
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    activeTab = GUILayout.Toolbar(activeTab, tabStrings);

                    switch (activeTab)
                    {
                        case 1:
                        {
                            // draw resources
                            DrawFoldoutArea(ref showResources, 
                                new GUIContent("Resources", "hello"), 
                                settings, 
                                0, 
                                DrawResources,
                                ShowResourceHeaderContextMenu);
                        }
                            break;
                        case 0:
                        {
                            if(Application.isPlaying)
                                EditorGUILayout.HelpBox("Cannot edit/view settings in Playmode.", MessageType.Warning);
                            EditorGUI.BeginDisabledGroup(Application.isPlaying);
                            // draw default
                            DrawFoldoutArea(ref showDefault,
                                new GUIContent("Default", "hello"),
                                settings,
                                -1,
                                DrawQualitySettings);

                            // draw quality levels
                            for (var index = 0; index < qualityNames.Length; index++)
                            {
                                var qualityString = qualityNames[index];
                                if (QualitySettings.GetQualityLevel() == index)
                                    qualityString += " (Current)";
                                DrawFoldoutArea(ref showQualityLevel[index],
                                    new GUIContent(qualityString, "hello"),
                                    settings,
                                    index,
                                    DrawQualitySettings,
                                    ShowQualityHeaderContextMenu);
                            }
                            EditorGUI.EndDisabledGroup();
                        }
                            break;
                    }
                    EditorGUILayout.EndVertical();
                    if (EditorGUI.EndChangeCheck())
                    {
                        settings.ApplyModifiedProperties();
                        // TODO need to update this
                        //if(Ocean.Instance != null)
                        //    Ocean.Instance.Init(BaseSystem.GetInstance<WaterManager>().CurrentState);
                    }
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] {"SSR"})
            };

            return provider;
        }
        
        static void DrawQualitySettings(SerializedObject obj, int index)
        {
            if (index >= 0)
            {
                var disable = qualitySettings[index] == null || qualitySettings[index].objectReferenceValue ==
                    defaultSettings.objectReferenceValue;
                var data = disable ? defaultSettings : qualitySettings[index];
                EditorGUI.BeginDisabledGroup(disable);
                EditorGUILayout.PropertyField(data, true);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.PropertyField(defaultSettings, true);
            }
        }

        static void DrawResources(SerializedObject obj, int index)
        {
            EditorGUILayout.PropertyField(resources,
                true);
        }

        static void DrawFoldoutArea(ref bool foldoutBool, GUIContent foldoutContent, SerializedObject obj, int index, Action<SerializedObject, int> contentGUI, Action<Rect, int> contextAction = null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foldoutBool = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutBool, foldoutContent, null, rect => {contextAction?.Invoke(rect, index);});
            if (foldoutBool)
            {
                contentGUI?.Invoke(obj, index);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();
        }

        static void ShowResourceHeaderContextMenu(Rect position, int index)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Reset"), false, ResetResources);
            menu.DropDown(position);
        }

        static void ResetResources()
        {
            if (EditorUtility.DisplayDialog("Reset Water System Resources",
                    "This action cannot be undone!",
                    "Reset",
                    "Cancel"))
            {
                ProjectSettings.Instance.resources.Init();
            }
        }
        
        static void ShowQualityHeaderContextMenu(Rect position, int index)
        {
            var menu = new GenericMenu();
            var unique = rawData.qualitySettings[index] != rawData.defaultQuality;
            var a = new GUIContent("Reset to Default");
            var b = new GUIContent("Custom settings");

            menu.AddItem(unique ? a : b, false, () => {SetQualityLevel(index, !unique);});
            menu.AddDisabledItem(unique ? b : a);
            menu.DropDown(position);
        }
        
        static void SetQualityLevel(int index, bool unique)
        {
            if (!unique)
            {
                if (EditorUtility.DisplayDialog("Reset Water System Resources",
                        "This action cannot be undone!",
                        "Reset",
                        "Cancel"))
                {
                    qualitySettings[index].managedReferenceValue = defaultSettings.managedReferenceValue;
                }
            }
            else
            {
                ProjectSettings.Instance.qualitySettings[index] = Quality.Create();
                qualitySettings[index].managedReferenceValue = ProjectSettings.Instance.qualitySettings[index];
            }
        }

        private static ProjectSettings GetOrCreateSettings()
        {
            if(ProjectSettings.Instance != null) return ProjectSettings.Instance;
            var settings = AssetDatabase.LoadAssetAtPath<ProjectSettings>(SettingsConsts.FullBuildPath);
            if (settings != null) return settings;
            
            Debug.Log("Making new WaterProjectSettings asset");
            settings = ScriptableObject.CreateInstance<ProjectSettings>();

            // check for folder
#if UNITY_2023_1_OR_NEWER
            if (!AssetDatabase.AssetPathExists(SettingsConsts.BuildRelativeFolder))
#else
            if (!AssetDatabase.IsValidFolder(SettingsConsts.BuildRelativeFolder))
#endif
            {
                AssetDatabase.CreateFolder(SettingsConsts.AssetFolder, SettingsConsts.Build);
            }

            AssetDatabase.CreateAsset(settings, SettingsConsts.FullBuildPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }

        [InitializeOnLoadMethod]
        public static void LoadEditorSettingsAsset()
        {
            if (!Application.isPlaying)
            {
                ProjectSettings.Instance = GetOrCreateSettings();
            }
        }
    }
}