using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using PlanarSettings = WaterSystem.Rendering.PlanarReflections.PlanarReflectionSettings;

namespace WaterSystem.Rendering
{
	[CustomPropertyDrawer(typeof(PlanarReflections.PlanarReflectionSettings))]
	public class PlanarSettingsDrawer : PropertyDrawer
	{
		// Props
		private SerializedProperty resolutionMode;
		private SerializedProperty resolutionMulti;
		private SerializedProperty resolutionCustom;
		private SerializedProperty layerMask;
		private SerializedProperty shadows;
		private SerializedProperty obliqueProjection;
		private SerializedProperty rendererMode;
		private SerializedProperty rendererIndex;

		private float height;

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
			InitProps(property);
			EditorGUI.BeginProperty(position, label, property);

			var basePos = position;
			position.height = SingleLine();
			
			EditorGUI.PropertyField(position, resolutionMode);
			position.y += SingleLine();
			EditorGUI.indentLevel++;
			switch (resolutionMode.enumValueIndex)
			{
				case (int)PlanarReflections.ResolutionModes.Multiplier:
					EditorGUI.PropertyField(position, resolutionMulti);
					position.y += SingleLine();
					break;
				case (int)PlanarReflections.ResolutionModes.Custom:
					EditorGUI.PropertyField(position, resolutionCustom);
					position.y += SingleLine();
					break;
			}
			EditorGUI.indentLevel--;
			EditorGUI.PropertyField(position, layerMask);
			position.y += SingleLine();
			EditorGUI.PropertyField(position, shadows);
			position.y += SingleLine();
			EditorGUI.PropertyField(position, obliqueProjection);
			position.y += SingleLine();
			if (obliqueProjection.boolValue == false)
			{
				position.height = SingleLine() * 2;
				EditorGUI.HelpBox(position,
					"Disabling Oblique Projection will lead to objects refelcting below the water," +
					" only use this if you are having issue with certaint effects in the relfeciotns like Fog.",
					MessageType.Info);
				position.y += SingleLine() * 2;
			}
			position.height = SingleLine();
			EditorGUI.PropertyField(position, rendererMode);
			position.y += SingleLine();
			if (RendererModeExpand())
			{
				EditorGUI.indentLevel++;
				EditorGUI.PropertyField(position, rendererIndex);
				position.y += SingleLine();
				EditorGUI.indentLevel--;
			}
			EditorGUI.EndProperty();

			position.height = height = position.y - basePos.y;
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			InitProps(property);
			var baseHeight = 5; // base properties (res, layers, shadows, oblique, renderer mode) = 5
			baseHeight += (obliqueProjection.boolValue ? 0 : 2); // add height for oblique help box
			baseHeight += (RendererModeExpand() ? 1 : 0); // add height for oblique help box
			baseHeight += resolutionMode.enumValueIndex // add extra space for resolution extras
				is (int) PlanarReflections.ResolutionModes.Custom
				or (int) PlanarReflections.ResolutionModes.Multiplier
				? 1 : 0;

			return SingleLine() * baseHeight;
		}
		
		private float SingleLine()
		{
			return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
		}

		private bool RendererModeExpand()
		{
			return (PlanarReflections.RendererMode) rendererMode.enumValueIndex != PlanarReflections.RendererMode.Match;
		}
	}
}
