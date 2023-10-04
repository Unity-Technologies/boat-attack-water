using UnityEditor;
using UnityEngine;
using WaterSystem;
using WaterSystem.Settings;
using WaterSystem.Physics;

public class BuoyancyDebugWindow : EditorWindow
{
    private Vector2 _scrollPosition;
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

        var unusedCount = ProjectSettings.Quality.BuoyancySamples - WaterPhysics.ActiveSampleCount;
        EditorGUILayout.HelpBox(
            $"Total Objects:{BaseSystem.GetInstance<WaterPhysics>().QueryRegistry.Count}   " +
            $"Sample Point Count:{WaterPhysics.ActiveSampleCount}   " +
            $"Unused:{unusedCount}", 
            MessageType.None);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        var count = 0;
        foreach (var registryEntry in (BaseSystem.GetInstance<WaterPhysics>().QueryRegistry))
        {
            if(registryEntry.Key == null) continue;
            var obj = registryEntry.Key;
            var box = EditorGUILayout.BeginHorizontal();
            if(count % 2 == 0)
                GUI.Box(box, GUIContent.none);
            EditorGUILayout.LabelField($"{obj.name}", $"GUID:{registryEntry.Key.GetInstanceID()}");
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
        EditorGUILayout.EndScrollView();
    }
}
