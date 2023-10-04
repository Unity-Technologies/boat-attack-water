using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using WaterSystem.Settings;

namespace WaterSystem.Physics
{
    public class WaterPhysics : BaseSystem
    {
        // number of active samples, used for optimization
        [NonSerialized] public static int ActiveSampleCount;
        // native array of sample positions, read by Jobs in WaterModifier implementations
        internal NativeArray<Data.WaterSample> SamplePositions;
        // cache of sample positions, written by IWaterQuery implementations and copied to SamplePositions
        private Data.WaterSample[] _samplePositionsCache;
        // raw surface data A and B for ping-pong buffer
        internal NativeArray<Data.WaterSurface> WaterSurfaceDataWrite, WaterSurfaceDataRead;

        // array to hold instances of BaseSystem<> that implement PhysicsSystem<>
        [NonSerialized] public static List<IWaterModifier> WaterModifiers = new();
        // private NativeArray of WaterBodyData for use in jobs
        private NativeArray<WaterBodyData> _waterBodyData;
        // private cache of WaterBodyData for main thread use
        private WaterBodyData[] _waterBodyDataCache;
        
        // PhysicsLoop job handle
        private JobHandle _physicsLoopHandle;

        #region Initialization

        protected override void Init()
        {
            ChangeState(State.Setup);

            if (GetInstance<WaterManager>().CurrentState == State.Ready)
            {
                Setup();
            }
            else
            {
                // listen to WaterManager state change when ready call Setup()
                GetInstance<WaterManager>().OnStateChange += state =>
                {
                    if (state == State.Ready)
                    {
                        Setup();
                    }
                };
            }
        }
        
        // Setup function to be called by OnEnable
        // change to IEnumerator if we need to wait for something
        private void Setup()
        {
            var sampleCount = ProjectSettings.Quality.BuoyancySamples;
            // create native arrays
            SamplePositions = new NativeArray<Data.WaterSample>(sampleCount, Allocator.Persistent);
            _samplePositionsCache = new Data.WaterSample[sampleCount];
                
            WaterSurfaceDataWrite = new NativeArray<Data.WaterSurface>(sampleCount, Allocator.Persistent);
            WaterSurfaceDataRead = new NativeArray<Data.WaterSurface>(sampleCount, Allocator.Persistent);
            for (int i = 0; i < sampleCount; i++)
            {
                WaterSurfaceDataWrite[i] = new Data.WaterSurface(){ Position = Vector3.up, GUID = 0};
                WaterSurfaceDataRead[i] = new Data.WaterSurface(){Position = Vector3.one, GUID = 0};
            }

            ChangeState(State.Ready);
        }

        #endregion

        #region UpdateLogic

        private void LateUpdate()
        {
            if(CurrentState != State.Ready) return;
            
            Profiler.BeginSample("Complete Jobs");
            CompleteJobs();
            Profiler.EndSample();
            
            // Gather sample points from all WaterQueries
            Profiler.BeginSample("Update Sample Points");
            UpdateSamplePoints();
            Profiler.EndSample();

            // Bounding Box checks and offset calculations
            Profiler.BeginSample("Bounding Box Checks");
            _physicsLoopHandle = BoundingCull();
            Profiler.EndSample();
            
            // set base water surface data for each based off of the water body and sample positions
            Profiler.BeginSample("Set Base Water Surface Data");
            _physicsLoopHandle = SetBaseWaterSurfaceData(_physicsLoopHandle);
            Profiler.EndSample();
            
            // Update Modifiers
            Profiler.BeginSample("Update Modifiers");
            // loop through all WaterModifiers and call EnqueueJob on each
            _physicsLoopHandle = EnqueueModifiers(_physicsLoopHandle);
            Profiler.EndSample();
        }
        
        private void CompleteJobs()
        {
            _physicsLoopHandle.Complete();
            
            // copy data from write to read
            WaterSurfaceDataWrite.CopyTo(WaterSurfaceDataRead);
            
            SamplePositions.CopyTo(_samplePositionsCache);
            if(_waterBodyData.IsCreated)
                _waterBodyDataCache = _waterBodyData.ToArray();
        }

        private void UpdateSamplePoints()
        {
            // loop through all SamplePositions and set the InstanceID to 0
            Profiler.BeginSample("Resetting Sample Positions");
            for (var i = 0; i < SamplePositions.Length; i++)
            {
                var samplePosition = SamplePositions[i];
                samplePosition.InstanceID = 0;
                samplePosition.WaterBodyID = 0;
                SamplePositions[i] = samplePosition;
            }
            Profiler.EndSample();
            // loop through all WaterQueries and get the sample points for each, adding to _samplePositionsCache
            foreach(var query in QueryRegistry.Keys)
            {
                if(query == null || !query.enabled) continue;
                Profiler.BeginSample($"UpdatingSample points for {query.name}", query);
                var offset = QueryRegistry[query];
                var slice = SamplePositions.Slice(offset.x, offset.y - offset.x);
                query.SetQueryPositions(ref slice);
                query.GetQueryResults(WaterSurfaceDataRead.Slice(offset.x, offset.y - offset.x));
                Profiler.EndSample();
            }
        }

        private JobHandle BoundingCull()
        {
            // do bounding box checks on all sample points based off waterbody bounds, assigning watersample W to index of waterbody
            PopulateWaterBodyData();
            var boundsCheck = new BoundingBoxCheck()
            {
                SamplePositions = SamplePositions,
                WaterBodyBoundsData = _waterBodyData,
            };
            var bounds = boundsCheck.Schedule(ActiveSampleCount, 128);
            
            // need to add a job that will find the start/end offsets for each waterbody in the sample points
            var offsets = new WaterBodyLookup()
            {
                SamplePositions = SamplePositions,
                WaterBodyData = _waterBodyData,
            };

            return offsets.Schedule(ActiveSampleCount, bounds);
        }

        private JobHandle SetBaseWaterSurfaceData(JobHandle inputDeps)
        {
            var setBaseWaterSurfaceData = new WaterSurfacePrep()
            {
                SamplePositions = SamplePositions,
                WaterSurfaceData = WaterSurfaceDataWrite,
                WaterBodyData = _waterBodyData,
            };
            return setBaseWaterSurfaceData.Schedule(ActiveSampleCount, 128, inputDeps);
        }
        
        // Method that loops through a list of WaterBodies and calls EnqueueJob on each
        public JobHandle EnqueueModifiers(JobHandle jobHandle)
        {
            foreach (var modifier in WaterModifiers)
            {
                // for each waterbody
                foreach (var waterBody in WaterManager.WaterBodies)
                {
                    // set off jobs for each system
                    jobHandle = modifier.EnqueueAllJobs(ref SamplePositions, ref WaterSurfaceDataWrite, ref jobHandle, _waterBodyData, waterBody);
                }
            }
            return jobHandle;
        }

        #endregion

        #region QueryRegistry

        /// <summary>
        /// Dictionary containing the objects InstanceID and the start and end index of the sample points.
        /// </summary>
        public readonly Dictionary<WaterQuery, int2> QueryRegistry = new();

        public void AddQuery(WaterQuery query)
        {
            Profiler.BeginSample("Add Query Points");
            var count = query.QueryCount;

            if (QueryRegistry.TryGetValue(query, out var offsets))
            {
                // if the samplePoints length is different than the cached length, remove the entry and add a new one
                if (count != offsets.y - offsets.x)
                {
                    RemoveQuery(query);
                    Debug.Log($"Query {query.name} has changed sample point count({offsets.y - offsets.x} vs {query.QueryCount}). Removing and re-adding to cache.", query);
                    AddQuery(query);
                    return;
                }
            }
            else
            {
                if (ActiveSampleCount + count >= ProjectSettings.Quality.BuoyancySamples)
                {
                    Debug.LogError($"Trying to add more sample points than the cache can hold. {ActiveSampleCount} active points, adding {count} will exceed {ProjectSettings.Quality.BuoyancySamples}", query);
                    return;
                }
                
                offsets = new int2(ActiveSampleCount, ActiveSampleCount + count);
                QueryRegistry.Add(query, offsets);
                ActiveSampleCount += count;
                Debug.Log($"Adding {count} sample points for {query.name} to cache(total active points now {ActiveSampleCount}).", query);
            }
            Profiler.EndSample();
        }

        /// <summary>
        /// Method to remove an entry from the _queryRegistry based on the provided guid
        /// </summary>
        /// <param name="guid">Object Instance ID to remove</param>
        public void RemoveQuery(WaterQuery query)
        {
            if (!QueryRegistry.TryGetValue(query, out var offsets)) return;
            
            var min = offsets.x;
            var size = offsets.y - min;

            QueryRegistry.Remove(query);

            var keys = QueryRegistry.Keys.ToArray();
            foreach (var key in keys)
            {
                if (QueryRegistry[key].x < min) continue;
                QueryRegistry[key] -= size;
            }

            ActiveSampleCount -= size;
            Debug.Log($"Removing {query.name} from cache(total active points now {ActiveSampleCount}).", query);
        }
        
        /// <summary>
        /// Method to get the data from the WaterSurfaceDataB native array and
        /// copy it to the outPos array based on the provided guid
        /// </summary>
        /// <param name="guid">Object Instance ID to get data for</param>
        /// <param name="outPos">Array to copy data to</param>
        public void GetData(WaterQuery query, ref Data.WaterSurface[] outPos)
        {
            Profiler.BeginSample("Get Gerstner Data");
            if (!QueryRegistry.TryGetValue(query, out var offsets)) return;
            
            WaterSurfaceDataRead.Slice(offsets.x, offsets.y - offsets.x).CopyTo(outPos);
            Profiler.EndSample();
        }
        
        #endregion
        
        #region Teardown

        private void OnDisable()
        {
            ChangeState(State.Cleanup);
            CleanUp();
            ChangeState(State.None);
        }

        private void CleanUp()
        {
            _physicsLoopHandle.Complete();
            _samplePositionsCache = Array.Empty<Data.WaterSample>();
            // cleanup native arrays if valid
            NativeArrayDispose(SamplePositions);
            NativeArrayDispose(WaterSurfaceDataWrite);
            NativeArrayDispose(WaterSurfaceDataRead);
            NativeArrayDispose(_waterBodyData);
            foreach (var modifier in WaterModifiers)
            {
                modifier.Cleanup();
            }
        }

        #endregion

        #region Jobs

        // bounding box check for each job
        // Gerstner Height C# Job
        [BurstCompile]
        internal struct BoundingBoxCheck : IJobParallelFor
        {
            // sample positions
            public NativeArray<Data.WaterSample> SamplePositions;
            
            // read only WaterBodyBoundsData array
            [ReadOnly] public NativeArray<WaterBodyData> WaterBodyBoundsData;

            // The code actually running on the job
            public void Execute(int i)
            {
                var sample = SamplePositions[i];

                // perform bounds check for each waterBody in WaterBodyBoundsData
                foreach (var waterBody in WaterBodyBoundsData)
                {
                    if (!waterBody.isEnabeld) continue;
                    
                    var positionOS = math.mul(math.fastinverse(waterBody.matrix), new float4(sample.Position.xyz, 1)).xyz;
                    if (!waterBody.bounds.Contains(positionOS)) continue;
                    
                    // if the sample is inside the bounds, set the waterBodyIndex and break
                    sample.WaterBodyID = waterBody.waterBodyInstanceID;
                    SamplePositions[i] = sample;
                }
            }
        }
        
        // IJobParallelFor for looping through SamplePositions and setting base WaterSurfaceData
        [BurstCompile]
        internal struct WaterSurfacePrep : IJobParallelFor
        {
            [ReadOnly] public NativeArray<WaterBodyData> WaterBodyData;
            [ReadOnly] public NativeArray<Data.WaterSample> SamplePositions;
            [WriteOnly] public NativeArray<Data.WaterSurface> WaterSurfaceData;

            public void Execute(int index)
            {
                var sample = SamplePositions[index];
                // find the matching waterBodyData based on the waterBodyID
                WaterBodyData data = default;
                for (int i = 0; i < WaterBodyData.Length; i++)
                {
                    if(WaterBodyData[i].waterBodyInstanceID != sample.WaterBodyID) continue;
                    data = WaterBodyData[i];
                    break;
                }

                Data.WaterSurface waterSurface = new Data.WaterSurface
                {
                    Position = new float3(sample.Position.x, data.matrix.c3.y, sample.Position.z),
                    GUID = (uint)data.waterBodyInstanceID,
                    Normal = new float3(0, 1, 0),
                    Depth = 20, // TODO: Get depth from waterBodyData?
                    Current = float2.zero
                };
                WaterSurfaceData[index] = waterSurface;
            }
        }
        
        // IJob for looping through SamplePositions and outputting sets bsased on WaterBodyID
        [BurstCompile]
        internal struct WaterBodyLookup : IJobFor
        {
            // sample positions
            [ReadOnly] public NativeArray<Data.WaterSample> SamplePositions;
            
            // read only WaterBodyBoundsData array
            public NativeArray<WaterBodyData> WaterBodyData;

            public void Execute(int index)
            {
                var sample = SamplePositions[index];
                var waterBodyID = sample.WaterBodyID;
                
                // if the waterBodyID is -1, it is not inside a waterBody, so skip
                if (waterBodyID == -1) return;
                
                // loop through WaterBodyOffsets and find the matching waterBodyID
                for (var waterBodyIndex = 0; waterBodyIndex < WaterBodyData.Length; waterBodyIndex++)
                {
                    if (WaterBodyData[waterBodyIndex].waterBodyInstanceID != waterBodyID) continue;

                    var data = WaterBodyData[waterBodyIndex];
                    
                    // if the waterBodyID matches, set the offset and break
                    var offset = data.offset;
                    offset.x = index < offset.x ? index : offset.x;
                    offset.y = index > offset.y ? index : offset.y;
                    data.offset = offset;
                    WaterBodyData[waterBodyIndex] = data;
                    break;
                }
            }
        }

        #endregion
        
        #region Utility

        // method to populate _waterBodyBoundData with the bounds of each waterBody
        public void PopulateWaterBodyData()
        {
            if (_waterBodyData.IsCreated)
            {
                if (_waterBodyData.Length != WaterManager.WaterBodies.Count)
                {
                    _waterBodyData.Dispose();
                    _waterBodyData =
                        new NativeArray<WaterBodyData>(WaterManager.WaterBodies.Count, Allocator.Persistent);
                }
            }
            else
            {
                _waterBodyData =
                    new NativeArray<WaterBodyData>(WaterManager.WaterBodies.Count, Allocator.Persistent);
            }

            var index = 0;
            foreach (var waterBody in WaterManager.WaterBodies)
            {
                _waterBodyData[index] = new WaterBodyData
                {
                    waterBodyInstanceID = waterBody.GetInstanceID(),
                    bounds = waterBody.GetBounds(),
                    offset = new int2(0, ProjectSettings.Quality.BuoyancySamples - 1),
                    matrix = waterBody.transform.localToWorldMatrix,
                    isEnabeld = waterBody.enabled,
                };
                index++;
            }
        }

        // Generic method for the above native array cleanup - TODO should be moved to a utility class
        public static void NativeArrayDispose<T>(NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }

        // method for returning a unique hue color for a given int, hard coded saturation, value and alpha
        public static Color GetColor(int i, float alpha = 0.5f)
        {
            var hue = (i * 0.00339887495f) % 1f;
            var col = Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.9f, 1f);
            col.a = alpha;
            return col;
        }

        #endregion

        #region Data
        
        public struct WaterBodyData
        {
            public int waterBodyInstanceID;
            public Bounds bounds;
            public int2 offset;
            public float4x4 matrix;
            public bool isEnabeld;
        }

        #endregion
        
        #region Debug

        private void OnDrawGizmos()
        {
            // draw the position of each SamplePosition
            if (_samplePositionsCache != null && _samplePositionsCache.Length > 0)
            {
                var colorOut = Color.red;
                colorOut.a = 0.25f;
                Gizmos.color = colorOut;
                for (var index = 0; index < _samplePositionsCache.Length; index++)
                {
                    var sample = _samplePositionsCache[index];
                    // skip if the sample is not valid, if the sample is not owned by a Query
                    if (sample.InstanceID == 0) continue;

                    // draw the sample position
                    Gizmos.color = sample.WaterBodyID == 0 ? colorOut : GetColor(sample.WaterBodyID, 0.8f);
                    Gizmos.DrawCube(sample.Position.xyz, Vector3.one * 0.3f);
                    
                    // draw the WaterSurfaceDataRead position
                    if (WaterSurfaceDataRead.IsCreated && sample.WaterBodyID != 0)
                    {
                        var waterSurface = WaterSurfaceDataRead[index];
                        Gizmos.color = Color.blue;
                        Gizmos.DrawCube(waterSurface.Position, Vector3.one * 0.3f);
                        // draw a line between the sample and the WaterSurfaceDataRead position
                        var lineColor = Color.white;
                        lineColor.a = 0.05f;
                        Gizmos.color = lineColor;
                        Gizmos.DrawLine(sample.Position.xyz, waterSurface.Position);
                    }
                }
            }
            
            // draw the position of each WaterSurfaceDataRead
            /*
            if (WaterSurfaceDataRead.IsCreated)
            {
                Gizmos.color = Color.blue;
                foreach (var waterSurface in WaterSurfaceDataRead)
                {
                    if(waterSurface.GUID != 0)
                        Gizmos.DrawCube(waterSurface.Position, Vector3.one * 0.2f);
                }
            }*/
            
        }

        protected override string DebugGUI()
        {
            var str = "";
            
            DebugModifiers(ref str);
            DebugQueries(ref str);

            return str;
        }

        private void DebugModifiers(ref string str)
        {
            str += $"<b>Water Modifiers({WaterModifiers.Count}):</b>";
            if (WaterModifiers is not {Count: > 0}) return;
            
            foreach (var system in WaterModifiers)
            {
                if(system != null)
                    str += $"\n- {system.GetType()}";
            }
        }

        private void DebugQueries(ref string str)
        {
            str += $"\n<b>Query Objects ({QueryRegistry.Count})</b>";
        }

        #endregion
        
    }
}