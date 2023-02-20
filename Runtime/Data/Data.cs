using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using WaterSystem.Rendering;

namespace WaterSystem
{
    public class Data
    {
        // Shader Keywords
        private static readonly string KeyRefCubemap = "_REFLECTION_CUBEMAP";
        private static readonly string KeyRefProbe = "_REFLECTION_PROBES";
        private static readonly string KeyRefPlanar = "_REFLECTION_PLANARREFLECTION";
        private static readonly string KeyRefSSR = "_REFLECTION_SSR";
        
        /// <summary>
        /// This class stores the settings for a water system
        /// </summary>
        [System.Serializable]
        public class OceanSettings
        {
            // General
            public bool isInfinite; // Is the water infinite (shader incomplete)
            public float distanceBlend = 100.0f;
            public int randomSeed = 3234;
            
            // Cubemap settings
            public Cubemap cubemapRefType;
            
            // Visual Surface
            public float _waterMaxVisibility = 5.0f;
            public Color _absorptionColor = new Color(0.2f, 0.6f, 0.8f);
            public Color _scatteringColor = new Color(0.0f, 0.085f, 0.1f);
            
            // Waves
            public List<Wave> _waves = new List<Wave>();
            public bool _customWaves;
            public BasicWaves _basicWaveSettings = new BasicWaves(0.5f, 45.0f, 5.0f);
            public AnimationCurve _waveFoamProfile = AnimationCurve.Linear(0.02f, 0f, 0.98f, 1f);
            public AnimationCurve _waveDepthProfile = AnimationCurve.Linear(0.0f, 1f, 0.98f, 0f);
            
            // Micro(surface) Waves
            public float _microWaveIntensity = 0.25f;
            
            // Shore
            public float _foamIntensity = 0.5f;
            public AnimationCurve _shoreFoamProfile = AnimationCurve.Linear(0.02f, 0f, 0.98f, 1f);
        }
        
        /// <summary>
        /// Basic wave type, this is for the base Gerstner wave values
        /// it will drive automatic generation of n amount of waves
        /// </summary>
        [Serializable]
        public class BasicWaves
        {
            [Range(3, 12)]
            public int waveCount = 6;
            public float amplitude;
            public float direction;
            public float wavelength;

            public BasicWaves(float amp, float dir, float len)
            {
                waveCount = 6;
                amplitude = amp;
                direction = dir;
                wavelength = len;
            }
        }
        
        /// <summary>
        /// Class to describe a single Gerstner Wave
        /// </summary>
        [Serializable]
        public struct Wave
        {
            public float amplitude; // height of the wave in units(m)
            public float direction; // direction the wave travels in degrees from Z+
            public float wavelength; // distance between crest>crest
            public float2 origin; // Omi directional point of origin
            public float onmiDir; // Is omni?

            public Wave(float amp, float dir, float length, float2 org, bool omni)
            {
                amplitude = amp;
                direction = dir;
                wavelength = length;
                origin = org;
                onmiDir = omni ? 1 : 0;
            }
        }
        
        /// <summary>
        /// The type of geometry, either vertex offset or tessellation
        /// </summary>
        [Serializable]
        public enum GeometryType
        {
            VertexOffset,
            //Tesselation,
        }

        /// <summary>
        /// Refleciton settings, this also contains a planar reflection copy
        /// </summary>
        [Serializable]
        public class ReflectionSettings
        {
            public Type reflectionType = Type.PlanarReflection; // How the reflections are generated
            public PlanarReflections.PlanarReflectionSettings planarSettings = new(); // Planar reflection settings

            /// <summary>
            /// The type of reflection source, custom cubemap, closest refelction probe, planar reflection
            /// </summary>
            [Serializable]
            public enum Type
            {
                Cubemap,
                ReflectionProbe,
                PlanarReflection,
                ScreenSpaceReflection,
            }
        }

        /// <summary>
        /// SSR Reflection quality settings
        /// </summary>
        [Serializable]
        public class SsrSettings
        {
            public Steps steps = Steps.Medium;
            [Range(0.01f, 1f)]
            public float stepSize = 0.1f;
            [Range(0.25f, 3f)]
            public float thickness = 2f;
		
            [Serializable]
            public enum Steps
            {
                Low = 8,
                Medium = 16,
                High = 32,
            }
        }

        /// <summary>
        /// Lighting Settings
        /// </summary>
        [Serializable]
        public class LightingSettings
        {
            public LightingMode Mode = LightingMode.Basic;
            public bool Soft = true;
            public VolumeSample VolumeSamples = VolumeSample.Low;

            [Serializable]
            public enum LightingMode
            {
                Off,
                Basic,
                Volume,
            }
            
            [Serializable]
            public enum VolumeSample
            {
                Low = 4,
                Medium = 8,
                High = 16,
            }
        }

        /// <summary>
        /// Refraction Settings
        /// </summary>
        [Serializable]
        public class RefractionSettings
        {
            public RefractionMode Mode = RefractionMode.Simple;
            public bool Dispersion = false;
		
            public enum RefractionMode
            {
                Off,
                Simple,
                //Raymarch,
            }
        }

        /// <summary>
        /// Caustic Settings
        /// </summary>
        [Serializable]
        public class CausticSettings
        {
            public CausticMode Mode = CausticMode.Simple;
            public bool Dispersion = false;
		
            public enum CausticMode
            {
                Off,
                Simple,
                //Raymarch,
            }
        }
        
        public static string GetReflectionKeyword(ReflectionSettings.Type type)
        {
            switch (type)
            {
                case ReflectionSettings.Type.Cubemap:
                    return KeyRefCubemap;
                case ReflectionSettings.Type.ReflectionProbe:
                    return KeyRefProbe;
                case ReflectionSettings.Type.PlanarReflection:
                    return KeyRefPlanar;
                case ReflectionSettings.Type.ScreenSpaceReflection:
                    return KeyRefSSR;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}