using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using WaterSystem.Settings;

namespace WaterSystem.Settings
{
    //[CustomPropertyDrawer(typeof(WaterQualitySettings))]
    public class WaterQualitySettingsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            base.OnGUI(position, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label);
        }
    }
}
