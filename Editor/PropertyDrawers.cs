using UnityEditor;
using UnityEngine;

namespace WaterSystem
{
    public class PropertyDrawers
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
    }
}