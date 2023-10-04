using System;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Settings
{
    [CustomPropertyDrawer(typeof(Data.ReflectionSettings))]
        public class ReflectionSettingsDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var refMode = property.FindPropertyRelative(nameof(Data.ReflectionSettings.reflectionType));
                EditorGUI.PropertyField(position, refMode);
                position.y += CommonEditor.SingleLineHeight();

                EditorGUI.indentLevel++;
                switch ((Data.ReflectionSettings.Type) refMode.enumValueIndex)
                {
                    case Data.ReflectionSettings.Type.Cubemap:
                        DoRefTypeCubemap(ref position, ref property);
                        break;
                    case Data.ReflectionSettings.Type.ReflectionProbe:
                        DoRefTypeProbe(ref position, ref property);
                        break;
                    case Data.ReflectionSettings.Type.PlanarReflection:
                        var planarProp =
                            property.FindPropertyRelative(nameof(Data.ReflectionSettings.planarSettings));
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

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            {
                var height = 0f;
                var refMode = property.FindPropertyRelative(nameof(Data.ReflectionSettings.reflectionType)).enumValueIndex;
                height += CommonEditor.SingleLineHeight();// ref mode dropdown
                switch ((Data.ReflectionSettings.Type)refMode)
                {
                    case Data.ReflectionSettings.Type.Cubemap:
                        break;
                    case Data.ReflectionSettings.Type.ReflectionProbe:
                        break;
                    case Data.ReflectionSettings.Type.PlanarReflection:
                        height += EditorGUI.GetPropertyHeight(
                            property.FindPropertyRelative(nameof(Data.ReflectionSettings.planarSettings)));
                        break;
                    case Data.ReflectionSettings.Type.ScreenSpaceReflection:
                        height += EditorGUI.GetPropertyHeight(
                            property.FindPropertyRelative(nameof(Data.ReflectionSettings.ssrSettings)));
                        break;
                }

                return height;
            }
            
            private void DoRefTypeCubemap(ref Rect position, ref SerializedProperty property)
            {
                // no GUI yet
            }
            
            private void DoRefTypeProbe(ref Rect position, ref SerializedProperty property)
            {
                // no GUI yet
            }
            
            private void DoRefTypePlanarRefleciton(ref Rect position, ref SerializedProperty property)
            {
                EditorGUI.PropertyField(position, property);
                position.y += EditorGUI.GetPropertyHeight(property);
            }
            
            private void DoRefTypeSSR(ref Rect position, ref SerializedProperty property)
            {
                var ssrProp = property.FindPropertyRelative(nameof(Data.ReflectionSettings.ssrSettings));
                EditorGUI.PropertyField(position, ssrProp, true);
                position.y += EditorGUI.GetPropertyHeight(ssrProp);
            }
        }
}