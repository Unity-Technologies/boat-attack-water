using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        /// The type of geometry, either vertex offset or tessellation
        /// </summary>
        [Serializable]
        public enum GeometryType
        {
            VertexOffset,
            //Tesselation,
        }
        
        /// <summary>
        /// Geometry settings
        /// </summary>
        [Serializable]
        public class GeometrySettings
        {
            public GeometryType geometryType = GeometryType.VertexOffset;
            public float density = 1;
            public int maxDivisions = 5;
        }
        
        /// <summary>
        /// Refleciton settings, this also contains a planar reflection copy
        /// </summary>
        [Serializable]
        public class ReflectionSettings
        {
            public Type reflectionType = Type.PlanarReflection; // How the reflections are generated
            public Cubemap fallbackCubemap;
            public PlanarReflections.PlanarReflectionSettings planarSettings = new(); // Planar reflection settings
            public SsrSettings ssrSettings = new(); // SSR settings

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

            // method to get the SSR settings packed into a float4
            public float3 GetPacked()
            {
                return new float3(stepSize, thickness, 0);
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
        
        /// <summary>
        /// This is the struct that will be used to store the IWaterQuery sample and the GUID of the object it belongs to
        /// </summary>
        public struct WaterSample
        {
            /// <summary>
            /// Data1.xyz = position, Data1.w = WaterBodyID, recommended to use methods to access this data.
            /// </summary>
            private float4 Data1;
            /// <summary>
            /// InstanceID of the IWaterQuery object this sample belongs to
            /// </summary>
            public int InstanceID;
            
            /// <summary>
            /// Position of the sample in world space
            /// </summary>
            public float3 Position
            {
                get => Data1.xyz;
                set => Data1.xyz = value;
            }
            
            /// <summary>
            /// WaterBodyID represents the InstanceID of the water body this sample is within
            /// </summary>
            public int WaterBodyID
            {
                get => (int)Data1.w;
                set => Data1.w = value;
            }
        }
        
        /// <summary>
        /// Struct to describe Water Surface
        /// </summary>
        public struct WaterSurface
        {
            public float3 Position;
            public uint GUID;
            public float3 Normal;
            public float Depth;
            public float2 Current;
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
        
        [Serializable]
        public enum DebugShading
        {
            none,
            normalWS,
            Reflection,
            Refraction,
            Specular,
            SSS,
            Shadow,
            Foam,
            FoamMask,
            WaterBufferA,
            WaterBufferB,
            Depth,
            WaterDepth,
            Fresnel,
            Mesh,
        }
    }
}