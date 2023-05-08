using System;
using UnityEditor;
using UnityEngine;
using WaterSystem.Settings;

namespace WaterSystem
{
    class PropertyDrawers
    {
        [CustomPropertyDrawer(typeof(Data.BasicWaves))]
        public class BasicWavesEditor : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                
                var waveCount = property.FindPropertyRelative(nameof(Data.BasicWaves.waveCount));
                var amplitude = property.FindPropertyRelative(nameof(Data.BasicWaves.amplitude));
                var direction = property.FindPropertyRelative(nameof(Data.BasicWaves.direction));
                var wavelength = property.FindPropertyRelative(nameof(Data.BasicWaves.wavelength));

                var rect = new Rect(position.position, new Vector2(position.width, EditorGUIUtility.singleLineHeight));
                EditorGUI.PropertyField(rect, waveCount);
                rect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, amplitude);
                rect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, direction);
                rect.y += EditorGUIUtility.singleLineHeight;
                EditorGUI.PropertyField(rect, wavelength);
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return EditorGUIUtility.singleLineHeight * 4;
            }
        }
        
        [CustomEditor(typeof(Data.OceanSettings))]
        public class WaterSettingsDrawer : Editor
        {
            // null
        }

        [CustomEditor(typeof(Data))]
        public class DataEditor : Editor
        {
            // null
        }

        [CustomPropertyDrawer(typeof(WaterQualitySettings))]
        public class WaterQualitySettingsDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var baseRect = position;
                
                position.height = SingleLineHeight(false);
                DoGeometry(ref position, ref property);
                DoReflection(ref position, ref property);
                DoLighting(ref position, ref property);
                DoCaustics(ref position, ref property);
            }

            private void DoGeometry(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Geometry", EditorStyles.boldLabel);
                position.y += SingleLineHeight();
                EditorGUI.indentLevel++;
                {
                    EditorGUI.PropertyField(position,
                        property.FindPropertyRelative(nameof(WaterQualitySettings.waterGeomType)));
                    position.y += SingleLineHeight();
                }
                EditorGUI.indentLevel--;
            }
            
            private void DoReflection(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Reflection", EditorStyles.boldLabel);
                position.y += SingleLineHeight();

                var refSettings = property.FindPropertyRelative(nameof(WaterQualitySettings.reflectionSettings));
                
                EditorGUI.indentLevel++;
                {
                    var refMode = refSettings.FindPropertyRelative(nameof(WaterQualitySettings.reflectionSettings
                        .reflectionType));
                    EditorGUI.PropertyField(position, refMode);
                    position.y += SingleLineHeight();

                    EditorGUI.indentLevel++;
                    switch ((Data.ReflectionSettings.Type)refMode.enumValueIndex)
                    {
                        case Data.ReflectionSettings.Type.Cubemap:
                            DoRefTypeCubemap(ref position, ref property);
                            break;
                        case Data.ReflectionSettings.Type.ReflectionProbe:
                            DoRefTypeProbe(ref position, ref property);
                            break;
                        case Data.ReflectionSettings.Type.PlanarReflection:
                            var planarProp =
                                refSettings.FindPropertyRelative(nameof(Data.ReflectionSettings.planarSettings));
                            DoRefTypePlanarRefleciton(ref position, ref planarProp);
                            break;
                        case Data.ReflectionSettings.Type.ScreenSpaceReflection:
                            DoRefTypeSSR(ref position, ref property);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }

            private void DoLighting(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Lighting", EditorStyles.boldLabel);
                position.y += SingleLineHeight();
                EditorGUI.indentLevel++;
                {
                    var lightingProp = property.FindPropertyRelative(nameof(WaterQualitySettings.lightingSettings));
                    EditorGUI.PropertyField(position, lightingProp, true);
                    position.y += EditorGUI.GetPropertyHeight(lightingProp);
                }
                EditorGUI.indentLevel--;
            }
            
            private void DoCaustics(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Caustics", EditorStyles.boldLabel);
                position.y += SingleLineHeight();
                EditorGUI.indentLevel++;
                {
                    var causticsProp = property.FindPropertyRelative(nameof(WaterQualitySettings.causticSettings));
                    EditorGUI.PropertyField(position, causticsProp, true);
                    position.y += EditorGUI.GetPropertyHeight(causticsProp);
                }
                EditorGUI.indentLevel--;
            }

            private void DoRefTypeCubemap(ref Rect position, ref SerializedProperty property)
            {
                // no GUI
            }
            
            private void DoRefTypeProbe(ref Rect position, ref SerializedProperty property)
            {
                // no GUI
            }
            
            private void DoRefTypePlanarRefleciton(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.PropertyField(position, property);
                position.y += EditorGUI.GetPropertyHeight(property);
            }
            
            private void DoRefTypeSSR(ref Rect position, ref SerializedProperty property)
            {
                var ssrProp = property.FindPropertyRelative(nameof(WaterQualitySettings.ssrSettings));
                EditorGUI.PropertyField(position, ssrProp, true);
                position.y += EditorGUI.GetPropertyHeight(ssrProp);
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                var height = SingleLineHeight() * 4; // all 4 catergory headers
                height += SingleLineHeight();// geometry

                var refSettings = property.FindPropertyRelative(nameof(WaterQualitySettings.reflectionSettings));

                if (refSettings == null) return 50f;
                
                var refMode = refSettings.FindPropertyRelative(nameof(Data.ReflectionSettings.reflectionType)).enumValueIndex;
                height += SingleLineHeight();// ref mode dropdown
                switch ((Data.ReflectionSettings.Type)refMode)
                {
                    case Data.ReflectionSettings.Type.Cubemap:
                        height += SingleLineHeight();
                        break;
                    case Data.ReflectionSettings.Type.ReflectionProbe:
                        height += SingleLineHeight();
                        break;
                    case Data.ReflectionSettings.Type.PlanarReflection:
                        height += EditorGUI.GetPropertyHeight(
                            refSettings.FindPropertyRelative(nameof(Data.ReflectionSettings.planarSettings)));
                        break;
                    case Data.ReflectionSettings.Type.ScreenSpaceReflection:
                        height += EditorGUI.GetPropertyHeight(
                            property.FindPropertyRelative(nameof(WaterQualitySettings.ssrSettings)));
                        break;
                }

                height += EditorGUI.GetPropertyHeight(
                    property.FindPropertyRelative(nameof(WaterQualitySettings.lightingSettings)));
                
                height += EditorGUI.GetPropertyHeight(
                    property.FindPropertyRelative(nameof(WaterQualitySettings.causticSettings)));
                
                return height;
            }
        }

        [CustomPropertyDrawer(typeof(Settings.Resources))]
        public class WaterResourcesDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.BeginProperty(position, label, property);
                position.height = SingleLineHeight(false);
                var enumerator = property.GetEnumerator();
                while (enumerator.MoveNext()) {
                    var prop = enumerator.Current as SerializedProperty;
                    if (prop == null) continue;
                    
                    EditorGUI.PropertyField(position, prop);
                    position.y += SingleLineHeight();
                }
                EditorGUI.EndProperty();
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return property.isExpanded ? SingleLineHeight() * (property.CountInProperty() - 1) : 0;
            }
        }

        public static float SingleLineHeight(bool withSpacing = true)
        {
            return EditorGUIUtility.singleLineHeight + (withSpacing ? EditorGUIUtility.standardVerticalSpacing : 0);
        }
    }
}