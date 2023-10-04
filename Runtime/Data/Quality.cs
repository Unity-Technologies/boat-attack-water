using System;

namespace WaterSystem.Settings
{
	/// <summary>
	/// This scriptable object stores teh graphical/rendering settings for a water system
	/// </summary>
	[Serializable]
	public class Quality
	{
		// Visuals
		public Data.GeometrySettings geometrySettings = new(); // The type of geometry, either vertex offset or tessellation
		// Reflection settings
		public Data.ReflectionSettings reflectionSettings = new();
		// SSS/Lighting Settings
		public Data.LightingSettings lightingSettings = new();
		// Caustics
		public Data.CausticSettings causticSettings = new();
		// Physics
		public int BuoyancySamples = 4096;

		public static Quality Create()
		{
			if (!ProjectSettings.Instance || ProjectSettings.Instance.defaultQuality == null) return null;
			
			var defaultSettings = ProjectSettings.Instance.defaultQuality;
			var wqs = new Quality
			{
				geometrySettings = defaultSettings.geometrySettings,
				reflectionSettings = defaultSettings.reflectionSettings,
				lightingSettings = defaultSettings.lightingSettings,
				causticSettings = defaultSettings.causticSettings,
				BuoyancySamples = defaultSettings.BuoyancySamples
			};
			return wqs;
		}
	}
}