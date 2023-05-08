using System;

namespace WaterSystem.Settings
{
	/// <summary>
	/// This scriptable object stores teh graphical/rendering settings for a water system
	/// </summary>
	[Serializable]
	public class WaterQualitySettings
	{
		// Visuals
		public Data.GeometryType waterGeomType = Data.GeometryType.VertexOffset; // The type of geometry, either vertex offset or tessellation
		// Reflection settings
		public Data.ReflectionSettings reflectionSettings = new();
		// SSS/Lighting Settings
		public Data.SsrSettings ssrSettings = new();
		public Data.LightingSettings lightingSettings = new();
		// Caustics
		public Data.CausticSettings causticSettings = new();

		// Physics
		public int BuoyancySamples = 4096;

		public static WaterQualitySettings Create()
		{
			if (!WaterProjectSettings.Instance || WaterProjectSettings.Instance.defaultQualitySettings == null) return null;
			
			var defaultSettings = WaterProjectSettings.Instance.defaultQualitySettings;
			var wqs = new WaterQualitySettings
			{
				waterGeomType = defaultSettings.waterGeomType,
				reflectionSettings = defaultSettings.reflectionSettings,
				ssrSettings = defaultSettings.ssrSettings,
				lightingSettings = defaultSettings.lightingSettings,
				causticSettings = defaultSettings.causticSettings,
				BuoyancySamples = defaultSettings.BuoyancySamples
			};
			return wqs;
		}
	}
}