using System;
using UnityEditor;
using UnityEngine;
using WaterSystem;

[CustomEditor(typeof(DepthGenerator))]
public class DepthGeneratorEditor : Editor
{
    private readonly GUIContent _generateButton = new("Generate", "Generates the current depth tile.");
    private readonly GUIContent _generateAllButton = new("Generate All", "Generates all the depth tiles in the scene.");

    private SerializedProperty _size;
    private SerializedProperty _tileRes;
    
    private void OnEnable()
    {
        _size = serializedObject.FindProperty(nameof(DepthGenerator.size));
        _tileRes = serializedObject.FindProperty(nameof(DepthGenerator.tileRes));
    }

    public override void OnInspectorGUI()
    {
        DepthGenerator depthGen = target as DepthGenerator;

        EditorGUILayout.PropertyField(_size);
        EditorGUILayout.PropertyField(_tileRes);

        GUILayout.Space(10);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(_generateButton, new []{ GUILayout.Width(200), GUILayout.Height(30) }))
        {
            depthGen?.CaptureDepth();
        }
        /*
        if (GUILayout.Button(generateAllButton, GUILayout.Width(200)))
        {
            var generators = FindObjectsOfType<DepthGenerator>();
            foreach (var generator in generators)
            {
                generator.CaptureDepth();
            }
        }
        */
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        
        serializedObject.ApplyModifiedProperties();
    }
}
