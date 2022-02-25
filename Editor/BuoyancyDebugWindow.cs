using UnityEditor;
using UnityEngine;
using WaterSystem;

public class BuoyancyDebugWindow : EditorWindow
{
    string myString = "Hello World";
    bool groupEnabled;
    bool myBool = true;
    float myFloat = 1.23f;
    
    // Add menu named "My Window" to the Window menu
    [MenuItem("Window/URP Water System/Buoyancy Debug")]
    private static void Init()
    {
        // Get existing open window or if none, make a new one:
        var window = (BuoyancyDebugWindow)GetWindow(typeof(BuoyancyDebugWindow));
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("URP Water System : Buoyancy Debug", EditorStyles.largeLabel);

        EditorGUILayout.HelpBox(
            $"Total Objects:{GerstnerWavesJobs.Registry.Count} " +
            $"Sample Point Count:{GerstnerWavesJobs._positionCount}", 
            MessageType.None);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var count = 0;
        foreach (var registryEntry in GerstnerWavesJobs.Registry)
        {
            var obj = EditorUtility.InstanceIDToObject(registryEntry.Key);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{obj.name}", $"GUID:{registryEntry.Key}");
            EditorGUILayout.LabelField($"indicies:{registryEntry.Value.x}-{registryEntry.Value.y}",
                $"size:{registryEntry.Value.y - registryEntry.Value.x}");
            if (GUILayout.Button("Ping Object"))
            {
                EditorGUIUtility.PingObject(registryEntry.Key);
            }
            count++;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }
}
