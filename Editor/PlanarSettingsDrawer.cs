using UnityEngine;
using UnityEditor;
using PlanarSettings = WaterSystem.Rendering.PlanarReflections.PlanarReflectionSettings;

namespace WaterSystem.Rendering
{
	[CustomPropertyDrawer(typeof(PlanarReflections.PlanarReflectionSettings))]
	public class PlanarSettingsDrawer : PropertyDrawer
	{
		private bool rendererModeExpand = false;

		// Props
		private SerializedProperty resolutionMode;
		private SerializedProperty resolutionMulti;
		private SerializedProperty resolutionCustom;
		private SerializedProperty layerMask;
		private SerializedProperty shadows;
		private SerializedProperty obliqueProjection;
		private SerializedProperty rendererMode;
		private SerializedProperty rendererIndex;

		private void InitProps(SerializedProperty property)
		{
			resolutionMode = property.FindPropertyRelative(nameof(PlanarSettings.m_ResolutionMode));
			resolutionMulti = property.FindPropertyRelative(nameof(PlanarSettings.m_ResolutionMultipliter));
			resolutionCustom = property.FindPropertyRelative(nameof(PlanarSettings.m_ResolutionCustom));
			layerMask = property.FindPropertyRelative(nameof(PlanarSettings.m_ReflectLayers));
			shadows = property.FindPropertyRelative(nameof(PlanarSettings.m_Shadows));
			obliqueProjection = property.FindPropertyRelative(nameof(PlanarSettings.m_ObliqueProjection));
			rendererMode = property.FindPropertyRelative(nameof(PlanarSettings.m_RendererMode));
			rendererIndex = property.FindPropertyRelative(nameof(PlanarSettings.m_RendererIndex));
		}
		
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			InitProps(property);
			// Setup of extra data
			rendererModeExpand = (PlanarReflections.RendererMode)rendererMode.enumValueIndex != PlanarReflections.RendererMode.Match;
			
			EditorGUI.PropertyField(position, resolutionMode);
			EditorGUI.indentLevel++;
			switch (resolutionMode.enumValueIndex)
			{
				case (int)PlanarReflections.ResolutionModes.Multiplier:
					EditorGUI.PropertyField(EditorGUILayout.GetControlRect(true), resolutionMulti);
					break;
				case (int)PlanarReflections.ResolutionModes.Custom:
					EditorGUI.indentLevel++;
					EditorGUI.PropertyField(EditorGUILayout.GetControlRect(true), resolutionCustom);
					EditorGUI.indentLevel--;
					break;
			}
			EditorGUI.indentLevel--;
			EditorGUI.PropertyField(EditorGUILayout.GetControlRect(true), layerMask);
			EditorGUI.PropertyField(EditorGUILayout.GetControlRect(true), shadows);
			EditorGUI.PropertyField(EditorGUILayout.GetControlRect(true), obliqueProjection);
			if (obliqueProjection.boolValue == false)
				EditorGUILayout.HelpBox(
					"Disabling Oblique Projection will lead to objects refelcting below the water," +
					" only use this if you are having issue with certaint effects in the relfeciotns like Fog.",
					MessageType.Info);
			EditorGUI.PropertyField(EditorGUILayout.GetControlRect(true), rendererMode);
			if (rendererModeExpand)
			{
				EditorGUI.indentLevel++;
				EditorGUI.PropertyField(EditorGUILayout.GetControlRect(true), rendererIndex);
				EditorGUI.indentLevel--;
			}
			EditorGUI.EndProperty();
		}
	}
}
