using UnityEngine;
using UnityEditor;

namespace WaterSystem
{
    public class CommonEditor : Editor
    {
        public static class Styles
        {
            public static GUIContent ColorHeader =
                new GUIContent("Color Settings", Tooltips.ColorHeader);
            public static GUIContent WavesHeader =
                new GUIContent("Waves", Tooltips.WavesHeader);
            public static GUIContent ReflectionHeader =
                new GUIContent("Reflection", Tooltips.ReflectionHeader);
            public static GUIContent FlowHeader =
                new GUIContent("Flow Map", Tooltips.FlowHeader);
            public static GUIContent ShoreHeader =
                new GUIContent("Shoreline", Tooltips.ShoreHeader);
            public static GUIContent CausticHeader =
                new GUIContent("Caustics", Tooltips.CausticHeader);
        }

        public static class Tooltips
        {
            public static string ColorHeader = "Area to tweak the general look of the water";
            public static string WavesHeader = "Area to tweak the Gerstner waves";
            public static string ReflectionHeader = "Area to tweak how reflections are done and look";
            public static string FlowHeader = "Area to change flowmap settings";
            public static string ShoreHeader = "Area to tweak the shoreline";
            public static string CausticHeader = "Area to tweak the caustic effect of the water";
        }

        private const string CreateMenuString = "GameObject/Boat Attack Water/";
        
        [ MenuItem( CreateMenuString + "Water", false, 10) ]
        static void CreateOcean()
        {
            var gameObject = new GameObject("Ocean", typeof(Water));
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + gameObject.name);
            Selection.activeObject = gameObject;
        }
        
        public static float SingleLineHeight(bool withSpacing = true)
        {
            return EditorGUIUtility.singleLineHeight + (withSpacing ? EditorGUIUtility.standardVerticalSpacing : 0);
        }
    }
}