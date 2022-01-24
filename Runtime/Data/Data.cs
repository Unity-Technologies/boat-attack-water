using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace WaterSystem
{
    public class Data
    {
        /// <summary>
        /// This class stores the settings for a water system
        /// </summary>
        [System.Serializable]
        public class OceanSettings
        {
            // General
            public GeometryType waterGeomType; // The type of geometry, either vertex offset or tessellation
            public ReflectionType refType = ReflectionType.PlanarReflection; // How the reflections are generated
            public PlanarReflections.PlanarReflectionSettings planarSettings;
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
            public float _foamIntensity = 1.0f;
            public AnimationCurve _shoreFoamProfile = AnimationCurve.Linear(0.02f, 1f, 0.98f, 0f);
        }
        
        /// <summary>
        /// Basic wave type, this is for the base Gerstner wave values
        /// it will drive automatic generation of n amount of waves
        /// </summary>
        [System.Serializable]
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
        [System.Serializable]
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
        /// The type of reflection source, custom cubemap, closest refelction probe, planar reflection
        /// </summary>
        [System.Serializable]
        public enum ReflectionType
        {
            Cubemap,
            ReflectionProbe,
            PlanarReflection
        }
        
        /// <summary>
        /// The type of geometry, either vertex offset or tessellation
        /// </summary>
        [System.Serializable]
        public enum GeometryType
        {
            VertexOffset,
            Tesselation
        }
    }
}