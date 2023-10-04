using System;
using UnityEditor;
using UnityEngine;
using WaterSystem.Settings;

namespace WaterSystem
{
    class PropertyDrawers
    {
        [CustomPropertyDrawer(typeof(GerstnerWaves.BasicWaves))]
        public class BasicWavesEditor : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                
                var waveCount = property.FindPropertyRelative(nameof(GerstnerWaves.BasicWaves.waveCount));
                var amplitude = property.FindPropertyRelative(nameof(GerstnerWaves.BasicWaves.amplitude));
                var direction = property.FindPropertyRelative(nameof(GerstnerWaves.BasicWaves.direction));
                var wavelength = property.FindPropertyRelative(nameof(GerstnerWaves.BasicWaves.wavelength));

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

        [CustomEditor(typeof(Data))]
        public class DataEditor : Editor
        {
            // null
        }
        
        [CustomPropertyDrawer(typeof(Quality))]
        public class WaterQualitySettingsDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var baseRect = position;
                
                position.height = CommonEditor.SingleLineHeight(false);
                DoGeometry(ref position, ref property);
                DoReflection(ref position, ref property);
                DoLighting(ref position, ref property);
                DoCaustics(ref position, ref property);
            }

            private void DoGeometry(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Geometry", EditorStyles.boldLabel);
                position.y += CommonEditor.SingleLineHeight();
                EditorGUI.indentLevel++;
                {
                    var prop = property.FindPropertyRelative(nameof(Quality.geometrySettings));
                    EditorGUI.PropertyField(position, prop, true);
                    position.y += CommonEditor.SingleLineHeight() * 3;
                }
                EditorGUI.indentLevel--;
            }
            
            private void DoReflection(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Reflection", EditorStyles.boldLabel);
                position.y += CommonEditor.SingleLineHeight();

                var refSettings = property.FindPropertyRelative(nameof(Quality.reflectionSettings));
                EditorGUI.PropertyField(position, refSettings, true);
                position.y += EditorGUI.GetPropertyHeight(refSettings);
            }

            private void DoLighting(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Lighting", EditorStyles.boldLabel);
                position.y += CommonEditor.SingleLineHeight();
                EditorGUI.indentLevel++;
                {
                    var lightingProp = property.FindPropertyRelative(nameof(Quality.lightingSettings));
                    EditorGUI.PropertyField(position, lightingProp, true);
                    position.y += EditorGUI.GetPropertyHeight(lightingProp);
                }
                EditorGUI.indentLevel--;
            }
            
            private void DoCaustics(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.LabelField(position, "Caustics", EditorStyles.boldLabel);
                position.y += CommonEditor.SingleLineHeight();
                EditorGUI.indentLevel++;
                {
                    var causticsProp = property.FindPropertyRelative(nameof(Quality.causticSettings));
                    EditorGUI.PropertyField(position, causticsProp, true);
                    position.y += EditorGUI.GetPropertyHeight(causticsProp);
                }
                EditorGUI.indentLevel--;
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                var height = CommonEditor.SingleLineHeight() * 4; // all 4 catergory headers
                
                height += CommonEditor.SingleLineHeight() * 3; // geometry settings

                height += EditorGUI.GetPropertyHeight(
                    property.FindPropertyRelative(nameof(Quality.reflectionSettings)));

                height += EditorGUI.GetPropertyHeight(
                    property.FindPropertyRelative(nameof(Quality.lightingSettings)));
                
                height += EditorGUI.GetPropertyHeight(
                    property.FindPropertyRelative(nameof(Quality.causticSettings)));
                
                return height;
            }
        }

        [CustomPropertyDrawer(typeof(Settings.Resources))]
        public class WaterResourcesDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                EditorGUI.BeginProperty(position, label, property);
                position.height = CommonEditor.SingleLineHeight(false);
                var enumerator = property.GetEnumerator();
                while (enumerator.MoveNext()) {
                    var prop = enumerator.Current as SerializedProperty;
                    if (prop == null) continue;
                    
                    EditorGUI.PropertyField(position, prop);
                    position.y += CommonEditor.SingleLineHeight();
                }
                EditorGUI.EndProperty();
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                return property.isExpanded ? CommonEditor.SingleLineHeight() * (property.CountInProperty() - 1) : 0;
            }
        }
    }
}