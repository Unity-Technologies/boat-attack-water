using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem
{
    using CED = CoreEditorDrawer<OceanEditor>;
    
    [CustomEditor(typeof(Ocean))]
    public class OceanEditor : Editor
    {
        public static class Styles
        {
            // Color settings
            public static GUIContent maxVisibility          = new GUIContent("Max Visibility");
            public static GUIContent absoptionColor         = new GUIContent("Absorption");
            public static GUIContent scatteringColor        = new GUIContent("Scattering");
            // Wave
            public static GUIContent basicWaveCount         = new GUIContent("Layers");
            public static GUIContent basicWaveAmp           = new GUIContent("Amplitude");
            public static GUIContent basicWaveDir           = new GUIContent("Direction");
            public static GUIContent basicWaveWavelength    = new GUIContent("Wavelength");
            public static GUIContent microWaveIntenisty     = new GUIContent("Intensity");
            public static GUIContent waveFoamProfile     = new GUIContent("Profile");
            public static GUIContent waveDepthProfile     = new GUIContent("Depth Multiplier");
            // Reflection
            public static GUIContent cubemap                = new GUIContent("Cubemap");
            // Shore
            public static GUIContent foamIntensity          = new GUIContent("Foam Amount");
            public static GUIContent foamProfile          = new GUIContent("Foam Profile");
        }

        // color settings
        private SerializedProperty maxVisibility;
        private SerializedProperty absorptionColor;
        private SerializedProperty scatteringColor;
        // waves
        private SerializedProperty customWaves;
        private SerializedProperty basicWaveCount;
        private SerializedProperty basicWaveAmp;
        private SerializedProperty basicWaveDir;
        private SerializedProperty basicWaveWavelength;

        private SerializedProperty microWaveIntensity;
        
        private SerializedProperty waveFoamProfile;
        
        private SerializedProperty waveDepthProfile;
        // reflection
        private SerializedProperty cubemap;
        // flow
        // shore
        private SerializedProperty foamIntensity;
        private SerializedProperty foamProfile;
        // volume scattering
        // caustics
        // underwater
        // rendering
        
        private bool _colorHeader;
        private bool _wavesHeader;
        private bool _reflectionHeader;
        private bool _flowHeader;
        private bool _shoreHeader;
        private bool _causticHeader;

        private bool _baseGUI;
        
        private void OnEnable()
        {
            var settings = serializedObject.FindProperty(nameof(Ocean.settingsData));
            
            // Color Settings
            maxVisibility = settings.FindPropertyRelative(nameof(Data.OceanSettings._waterMaxVisibility));
            absorptionColor = settings.FindPropertyRelative(nameof(Data.OceanSettings._absorptionColor));
            scatteringColor = settings.FindPropertyRelative(nameof(Data.OceanSettings._scatteringColor));
            // Wave Settings
            customWaves = settings.FindPropertyRelative(nameof(Data.OceanSettings._customWaves));
            var basicWaves = settings.FindPropertyRelative(nameof(Data.OceanSettings._basicWaveSettings));
            basicWaveCount = basicWaves.FindPropertyRelative(nameof(Data.BasicWaves.waveCount));
            basicWaveAmp = basicWaves.FindPropertyRelative(nameof(Data.BasicWaves.amplitude));
            basicWaveDir = basicWaves.FindPropertyRelative(nameof(Data.BasicWaves.direction));
            basicWaveWavelength = basicWaves.FindPropertyRelative(nameof(Data.BasicWaves.wavelength));
            
            microWaveIntensity = settings.FindPropertyRelative(nameof(Data.OceanSettings._microWaveIntensity));
            
            waveFoamProfile = settings.FindPropertyRelative(nameof(Data.OceanSettings._waveFoamProfile));
            
            waveDepthProfile = settings.FindPropertyRelative(nameof(Data.OceanSettings._waveDepthProfile));
            // Reflection Settings
            cubemap = settings.FindPropertyRelative(nameof(Data.OceanSettings.cubemapRefType));

            // Shore
            foamIntensity = settings.FindPropertyRelative(nameof(Data.OceanSettings._foamIntensity));
            foamProfile = settings.FindPropertyRelative(nameof(Data.OceanSettings._shoreFoamProfile));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var ocean = target as Ocean;
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Ocean.shadingDebug)));
            
            foreach (var value in Enum.GetValues(typeof(Sections)))
            {
                DoSection((Sections)value);
            }

            serializedObject.ApplyModifiedProperties();
            
            if (EditorGUI.EndChangeCheck())
            {
                ocean.Init();
            }
            //DoRaw(); //  draws original GUI
        }

        void DoColorSettings()
        {
            EditorGUILayout.Slider(maxVisibility, 0.01f, 50f, Styles.maxVisibility);
            absorptionColor.colorValue = EditorGUILayout.ColorField(Styles.absoptionColor,
                absorptionColor.colorValue,
                true,
                false,
                false);
            scatteringColor.colorValue = EditorGUILayout.ColorField(Styles.scatteringColor,
                scatteringColor.colorValue, 
                true,
                false,
                false);
        }

        void DoWaves()
        {
            EditorGUILayout.LabelField("Gerstner Waves", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            if (customWaves.boolValue)
            {
                // show custom waves GUI
            }
            else
            {
                EditorGUILayout.PropertyField(basicWaveCount, Styles.basicWaveCount);
                EditorGUILayout.Slider(basicWaveAmp, 0.1f, 3f, Styles.basicWaveAmp);
                EditorGUILayout.Slider(basicWaveDir, 0f, 360f, Styles.basicWaveDir);
                EditorGUILayout.Slider(basicWaveWavelength, 0.5f, 100f, Styles.basicWaveWavelength);
            }

            EditorGUILayout.PropertyField(waveDepthProfile, Styles.waveDepthProfile);

            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Micro Waves", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(microWaveIntensity, 0.0f, 2f, Styles.microWaveIntenisty);
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField("Foam", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(waveFoamProfile, Styles.waveFoamProfile);
            EditorGUI.indentLevel--;
        }

        void DoReflection()
        {
            EditorGUILayout.PropertyField(cubemap, Styles.cubemap);
        }

        void DoFlow()
        {
            EditorGUILayout.HelpBox("Not implemented yet.", MessageType.Warning);
        }

        void DoShore()
        {
            EditorGUILayout.Slider(foamIntensity, 0f, 4f, Styles.foamIntensity);
            EditorGUILayout.PropertyField(foamProfile, Styles.foamProfile);
        }

        void DoSection(Sections section)
        {
            switch (section)
            {
                case Sections.Color:
                    SectionDraw(DoColorSettings, CommonEditor.Styles.ColorHeader, ref _colorHeader);
                    break;
                case Sections.Waves:
                    SectionDraw(DoWaves, CommonEditor.Styles.WavesHeader, ref _wavesHeader);
                    break;
                case Sections.Reflection:
                    SectionDraw(DoReflection, CommonEditor.Styles.ReflectionHeader, ref _reflectionHeader);
                    break;
                case Sections.Flow:
                    SectionDraw(DoFlow, CommonEditor.Styles.FlowHeader, ref _flowHeader);
                    break;
                case Sections.Shore:
                    SectionDraw(DoShore, CommonEditor.Styles.ShoreHeader, ref _shoreHeader);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(section), section, null);
            }
        }

        void SectionDraw(Action method, GUIContent content, ref bool header)
        {
            header = CoreEditorUtils.DrawHeaderFoldout(content, header);
            if (header)
            {
                method();
            }//EditorGUILayout.EndFoldoutHeaderGroup();
        }

        void DoRaw()
        {
            _baseGUI = CoreEditorUtils.DrawHeaderFoldout("Raw GUI", _baseGUI);
            if(_baseGUI)
                base.OnInspectorGUI();
        }
        
        private enum Sections
        {
            Color,
            Waves,
            Reflection,
            Flow,
            Shore,
        }
    }
}