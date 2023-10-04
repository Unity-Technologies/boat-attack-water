using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using WaterSystem.Physics;
using WaterSystem.Settings;
using Random = Unity.Mathematics.Random;
using Utilities = WaterSystem.Rendering.Utilities;

namespace WaterSystem
{
    public class GerstnerWaves : WaterModifier<GerstnerWaves.Data, GerstnerWaves.JobData>
    {
        #region ModifierSpecific
        
        protected override void Init()
        {
            ChangeState(State.Setup);
            // do setup
            ChangeState(State.Ready);
        }

        public override JobHandle EnqueueJob(ref NativeArray<WaterSystem.Data.WaterSample> queryPositions,
            ref NativeArray<WaterSystem.Data.WaterSurface> waterSurface, ref JobHandle handle, NativeArray<WaterPhysics.WaterBodyData> waterBodyData, DataHashSet dataset)
        {
            var t = Time.time;
            if (!Application.isPlaying)
                t = Time.realtimeSinceStartup;
            
            // Calculate the Gerstner waves
            var heightJob = new HeightJob
            {
                Position = queryPositions,
                WaterBodyID = dataset.WaterBodyId,
                WaveData = dataset.Data,
                Time = t,
                Output = waterSurface,
                OffsetLength = new int2(0, ProjectSettings.Quality.BuoyancySamples),
            };

            return heightJob.Schedule(WaterPhysics.ActiveSampleCount, 64, handle);
        }

        protected override JobData GetJobData(Data input)
        {
            return SetupWaves(input);
        }

        #endregion

        #region Jobs

        [BurstCompile]
        private struct HeightJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<WaterSystem.Data.WaterSample> Position;
            [ReadOnly] public JobData WaveData;
            [ReadOnly] public int WaterBodyID;

            [ReadOnly] public float Time;
            [ReadOnly] public int2 OffsetLength;

            public NativeArray<WaterSystem.Data.WaterSurface> Output;

            // The code actually running on the job
            public void Execute(int i)
            {
                // Skip if not in the offset range
                if (i < OffsetLength.x || i >= OffsetLength.y - OffsetLength.x) return;
                // Skip if the sample is not in this water body
                if (Position[i].WaterBodyID != WaterBodyID) return;
                
                var surface = Output[i];
                GerstnerWaveCalc(ref surface, Position[i], WaveData.Waves, Time);

                Output[i] = surface;
            }
        }

        #endregion

        #region DataTypes

        /// <summary>
        /// This holds all the data that needs to be Serialized on the WaterBody itself.
        /// </summary>
        [Serializable]
        public class Data : IModifierData
        {
            public WaveType waveType;
            public BasicWaves basicWaves = new(0.5f, 45f, 5f);
            public List<Wave> waves = new();
            
            private ComputeBuffer _waveBuffer;
            
            public enum WaveType
            {
                Basic = 0,
                Manual = 4,
            }
            
            public int GetWaveCount()
            { 
                return waveType == WaveType.Basic ? basicWaves.waveCount : waves.Count;
            }

            public override void Hash(ref Hash128 hash)
            {
                // hash the wave type
                hash.Append((int) waveType);
                // hash BasicWaves
                hash.Append(basicWaves.amplitude);
                // hash the waves
                foreach (var wave in waves)
                {
                    hash.Append(wave.amplitude);
                    hash.Append(wave.direction);
                    hash.Append(wave.wavelength);
                    hash.Append(wave.origin.GetHashCode());
                    hash.Append(wave.onmiDir);
                }
            }
            
            // method to set the shader properties based off the data
            public void SetShaderProperties(ref Material material, Data waveSettings)
            {
                if (SystemInfo.supportsComputeShaders &&
                     Application.platform != RuntimePlatform.WebGLPlayer &&
                     Application.platform != RuntimePlatform.Android)
                {
                    material.EnableKeyword("USE_STRUCTURED_BUFFER");
                    _waveBuffer?.Dispose();
                    _waveBuffer = new ComputeBuffer(Utilities.ShaderIDs.WaveCount,  UnsafeUtility.SizeOf<Wave>());
                    _waveBuffer.SetData(GetWaveArray(waveSettings));
                    material.SetBuffer(Utilities.ShaderIDs.WaveDataBuffer, _waveBuffer);
                }
                else
                {
                    material.DisableKeyword("USE_STRUCTURED_BUFFER");
                    material.SetVectorArray(Utilities.ShaderIDs.WaveData, GetWaveData(GetWaveArray(waveSettings)));
                }
            }
            
            private Vector4[] GetWaveData(Wave[] waveData)
            {
                var waveShaderData = new Vector4[20];
                for (var i = 0; i < waveData.Length; i++)
                {
                    waveShaderData[i] = new Vector4(waveData[i].amplitude, waveData[i].direction, waveData[i].wavelength, waveData[i].onmiDir);
                    waveShaderData[i+10] = new Vector4(waveData[i].origin.x, waveData[i].origin.y, 0, 0);
                }
                return waveShaderData;
            }
        }

        public struct JobData : IJobData
        {
            public NativeArray<Wave> Waves;
            
            public void Dispose()
            {
                Waves.Dispose();
            }
        }
        
        /// <summary>
        /// Basic wave type, this is for the base Gerstner wave values
        /// it will drive automatic generation of n amount of waves
        /// </summary>
        [Serializable]
        public class BasicWaves
        {
            [Range(3, 12)]
            public int waveCount;
            public float amplitude;
            public float direction;
            public float wavelength;
            public uint seed;

            public BasicWaves(float amp, float dir, float len)
            {
                waveCount = 6;
                amplitude = amp;
                direction = dir;
                wavelength = len;
                seed = 123456;
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
            
            // TODO - potential descriptor helper, probably will be removed in the future
            [Serializable]
            public struct WaveDescriptor
            {
                private float4 _data;
            
                public float Amplitude
                {
                    get => _data.x;
                    set => _data.x = value;
                }
            
                public float Direction
                {
                    get => _data.y;
                    set => _data.y = value;
                }
            
                public float Wavelength
                {
                    get => _data.z;
                    set => _data.z = value;
                }
            
                public WaveDescriptor(float amp, float dir, float len)
                {
                    _data = new float4(amp, dir, len, 0);
                }
            }
        }

        #endregion

        #region Utilities

        private JobData SetupWaves(Data settingsData)
        {
            var waveCount = settingsData.waveType == Data.WaveType.Basic
                ? settingsData.basicWaves.waveCount
                : settingsData.waves.Count;
            
            var waveArray = new NativeArray<Wave>(waveCount, Allocator.Persistent);
            waveArray.CopyFrom(GetWaveArray(settingsData));
            
            var output = new JobData
            {
                Waves = waveArray
            };
            return output;
        }

        private static Wave[] GetWaveArray(Data settingsData)
        {
            var waveCount = settingsData.waveType == Data.WaveType.Basic
                ? settingsData.basicWaves.waveCount
                : settingsData.waves.Count;
            
            var waveArray = new Wave[waveCount];
            
            if(settingsData.waveType == Data.WaveType.Basic)
            {
                var rnd = new Random();
                //create basic waves based off basic wave settings
                var basicWaves = settingsData.basicWaves;
                var a = basicWaves.amplitude;
                var d = basicWaves.direction;
                var l = basicWaves.wavelength;
                var numWave = basicWaves.waveCount;

                var r = 1f / numWave;

                for (var i = 0; i < numWave; i++)
                {
                    rnd.InitState(settingsData.basicWaves.seed + (uint)i * 123456);
                    var p = Mathf.Lerp(0.1f, 1.9f, i * r);
                    var amp = a * math.lerp(0.5f, 2f, rnd.NextFloat());
                    var dir = d + math.lerp(-90f, 90f, rnd.NextFloat());
                    var len = l * math.lerp(0.5f, 2f, rnd.NextFloat());
                    waveArray[i] = new Wave(amp, dir, len, float2.zero, false);
                }
            }
            else
            {
                waveArray = settingsData.waves.ToArray();
            }

            return waveArray;
        }

        private static void GerstnerWaveCalc(ref WaterSystem.Data.WaterSurface surface, WaterSystem.Data.WaterSample waterSample, NativeArray<Wave> waveData, float t)
        {
            var waveCountMulti = 1f / waveData.Length;
            var wavePos = float3.zero;
            var waveNorm = float3.zero;

            for (var wave = 0; wave < waveData.Length; wave++) // for each wave
            {
                // Wave data vars
                var pos = waterSample.Position.xz;

                var amplitude = waveData[wave].amplitude;
                var direction = waveData[wave].direction;
                var wavelength = waveData[wave].wavelength;
                var omniPos = waveData[wave].origin;
                ////////////////////////////////wave value calculations//////////////////////////
                var w = 6.28318f / wavelength; // 2pi over wavelength(hardcoded)
                var wSpeed = math.sqrt(9.8f * w); // frequency of the wave based off wavelength
                const float peak = 2f; // peak value, 1 is the sharpest peaks
                var qi = peak / (amplitude * w * waveData.Length);

                var windDir = new float2(0f, 0f);

                direction = math.radians(direction); // convert the incoming degrees to radians
                var windDirInput = new float2(math.sin(direction), math.cos(direction)) * (1 - waveData[wave].onmiDir); // calculate wind direction - TODO - currently radians
                var windOmniInput = (pos - omniPos) * waveData[wave].onmiDir;

                windDir += windDirInput;
                windDir += windOmniInput;
                windDir = math.normalize(windDir);
                var dir = math.dot(windDir, pos - (omniPos * waveData[wave].onmiDir));

                ////////////////////////////position output calculations/////////////////////////
                var calc = dir * w + -t * wSpeed; // the wave calculation
                var cosCalc = math.cos(calc); // cosine version(used for horizontal undulation)
                var sinCalc = math.sin(calc); // sin version(used for vertical undulation)

                // calculate the offsets for the current point
                wavePos.xz += qi * amplitude * windDir.xy * cosCalc;
                wavePos.y += sinCalc * amplitude * waveCountMulti; // the height is divided by the number of waves 

                ////////////////////////////normal output calculations/////////////////////////
                var wa = w * amplitude;
                // normal vector
                var norm = new float3(-(windDir.xy * wa * cosCalc),
                    1 - (qi * wa * sinCalc));
                waveNorm += (norm * waveCountMulti) * amplitude;
            }

            // post wave processing
            //wavePos *= math.saturate(Opacity[i]);
            //wavePos.y += WaveLevelOffset;
            //waveNorm.xy *= Opacity[i];

            surface.Position.xyz += wavePos.xyz;
            surface.Normal = math.normalize(waveNorm.xzy);
        }
        
        #endregion
    }
}