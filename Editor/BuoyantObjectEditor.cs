using System;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Physics
{
    [CustomEditor(typeof(BuoyantObject))]
    public class BuoyantObjectEditor : Editor
    {
        private BuoyantObject Obj => serializedObject.targetObject as BuoyantObject;

        [SerializeField]
        private bool _heightsDebugBool;

        [SerializeField]
        private bool _generalSettingsBool;

        public override void OnInspectorGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                base.OnInspectorGUI();
                return;
            }

            _generalSettingsBool = EditorGUILayout.BeginFoldoutHeaderGroup(_generalSettingsBool, "General Settings");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (_generalSettingsBool)
            {
                base.OnInspectorGUI();
            }

            _heightsDebugBool = EditorGUILayout.BeginFoldoutHeaderGroup(_heightsDebugBool, "Height Debug Values");
            EditorGUILayout.EndFoldoutHeaderGroup();
            if (_heightsDebugBool)
            {
                if (Obj.Heights != null)
                {
                    for (var i = 0; i < Obj.Heights.Length; i++)
                    {
                        var h = Obj.Heights[i];
                        EditorGUILayout.LabelField($"{i})Wave(heights):", $"X:{h.x:00.00} Y:{h.y:00.00} Z:{h.z:00.00}");
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Height debug info only available in playmode.", MessageType.Info);
                }
            }
        }
    }
}
