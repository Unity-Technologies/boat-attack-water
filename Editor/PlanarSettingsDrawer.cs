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
			
			// Don't make child fields be indented
			//var indent = EditorGUI.indentLevel;
			//EditorGUI.indentLevel = 0;

			/*
			// Rects
			Rect resMultiRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			Rect offsetRect = new Rect(position.x, resMultiRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
			Rect layerMaskRect = new Rect(position.x, offsetRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
			Rect shadowRect = new Rect(position.x, layerMaskRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width * 0.5f, EditorGUIUtility.singleLineHeight);
			Rect maxLODRect = new Rect(position.x + position.width * 0.5f, layerMaskRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width * 0.5f, EditorGUIUtility.singleLineHeight);
			Rect rendererModeRect = new Rect(position.x, shadowRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
			Rect rendererIndexRect = new Rect(position.x, rendererModeRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
*/

			//Rect newRect = EditorGUILayout.GetControlRect(true);
			
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

			/*
			var resMulti = property.FindPropertyRelative("m_ResolutionMultiplier");
			EditorGUI.PropertyField(resMultiRect, resMulti);
			position.y += EditorGUIUtility.singleLineHeight;
			var offset = property.FindPropertyRelative("m_ClipPlaneOffset");
			EditorGUI.Slider(offsetRect, offset, -0.500f, 0.500f);
			var layerMask = property.FindPropertyRelative("m_ReflectLayers");
			EditorGUI.PropertyField(layerMaskRect, layerMask);
			var shadows = property.FindPropertyRelative("m_Shadows");
			EditorGUI.PropertyField(shadowRect, shadows);
			
			EditorGUI.PropertyField(rendererModeRect, rendererMode);
			
			*/
			
			// Set indent back to what it was
			//EditorGUI.indentLevel = indent;

			EditorGUI.EndProperty();
		}

		//public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		//{
		//	return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4f;
		//}
	}
}
