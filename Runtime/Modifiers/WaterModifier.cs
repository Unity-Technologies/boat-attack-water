using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using WaterSystem;

// new class that inherits from BaseSystem<MonoBehaviour>
namespace WaterSystem.Physics
{
    // interface that is used to access watermodifier
    public interface IWaterModifier
    {
        public JobHandle EnqueueAllJobs(ref NativeArray<Data.WaterSample> queryPositions,
            ref NativeArray<Data.WaterSurface> waterSurface, ref JobHandle handle, NativeArray<WaterPhysics.WaterBodyData> waterBodyData, WaterBody waterBody);
        
        public void Cleanup() { }
    }
    
    /// <summary>
    /// Any feature that wants to affect the physics of objects in the water should inherit from this class.
    /// </summary>
    public abstract class WaterModifier<T1, T2> : BaseSystem, IWaterModifier where T1 : IModifierData where T2 : struct, IJobData
    {
        // dictionary to hold the water bodies that this modifier is affecting and the data for each water body
        protected Dictionary<WaterBody, DataHashSet> WaterBody = new Dictionary<WaterBody, DataHashSet>();
        
        public struct DataHashSet
        {
            public Hash128 DataHash;
            public int WaterBodyId;
            public T2 Data;
            
            public DataHashSet(int id, Hash128 hash128, T2 data)
            {
                DataHash = hash128;
                WaterBodyId = id;
                Data = data;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            // check if PhysicsSystems doesnt contain this instance, if it doesnt then add it
            if (!WaterPhysics.WaterModifiers.Contains(this))
            {
                Debug.Log($"Adding WaterModifier {GetType().Name} to physics systems");
                WaterPhysics.WaterModifiers.Add(this);
            }
        }
        
        public virtual void OnDisable()
        {
            // check if PhysicsSystems contains this instance, if it does then remove it
            if (WaterPhysics.WaterModifiers.Contains(this))
            {
                Debug.Log($"Removing WaterModifier {GetType().Name} from physics systems");
                WaterPhysics.WaterModifiers.Remove(this);
            }
        }

        public JobHandle EnqueueAllJobs(ref NativeArray<Data.WaterSample> queryPositions, 
            ref NativeArray<Data.WaterSurface> waterSurface, 
            ref JobHandle handle, NativeArray<WaterPhysics.WaterBodyData> waterBodyData, WaterBody waterBody)
        {
            JobHandle finalHandle = handle;
            
            // get the data from the water body that matches this modifierdata type
            if (waterBody.SupportsModifier<T1>() && waterBody.TryGetModifier<T1>(out var modifierData))
            {
                Profiler.BeginSample("Add/Update WaterModifier Data");
                var wbId = waterBody.GetInstanceID();
                // if the dictionary doesnt contain the water body then add it TODO dont update all the time if not needed
                if (!WaterBody.ContainsKey(waterBody))
                {
                    WaterBody.Add(waterBody, GetDataHashSet(wbId, modifierData));
                }
                else
                {
                    if (WaterBody[waterBody].DataHash != modifierData.GetHash())
                    {
                        WaterBody[waterBody].Data.Dispose();
                        
                        WaterBody[waterBody] = GetDataHashSet(wbId, modifierData, true);
                    }
                }
                Profiler.EndSample();
                
                // enqueue the job
                finalHandle = EnqueueJob(ref queryPositions, ref waterSurface, ref finalHandle, waterBodyData, WaterBody[waterBody]);
                // debug out that the above waterbody has been enqueued
                //Debug.Log($"Enqueued jobs for {GetType().Name} on {waterBody.name}");
            }
            return finalHandle;
        }
        
        /// <summary>
        /// EnqueueJob, this method take in a JobHandle and return a JobHandle, this is so that each system that modifies
        /// the water can be enqueued in the WaterPhysics system.
        /// </summary>
        /// <param name="queryPositions">This is the positions of the query points, these are in WorldSpace</param>
        /// <param name="waterSurface">This is the water surface data, this contains data that should be modified based on the system</param>
        /// <param name="handle">This is the JobHandle that is passed in from the WaterPhysicsSystem, this should be piped
        /// in as the first dependency to any jobs otherwise returned</param>
        /// <param name="data">This is the Job safe data for the systems modifier attached to the WaterBody currently executing</param>
        /// <returns>Returns a JobHandle that should be piped into the next system, or used as the final dependency</returns>
        public abstract JobHandle EnqueueJob(ref NativeArray<Data.WaterSample> queryPositions,
            ref NativeArray<Data.WaterSurface> waterSurface, ref JobHandle handle, 
            NativeArray<WaterPhysics.WaterBodyData> waterBodyData, DataHashSet data);

        /// <summary>
        /// TranslateData, this method takes in the data that is serialized on the WaterBody and translates it into
        /// a Job safe data structure that can be passed into the C# Job.
        /// </summary>
        /// <param name="input">The serialized data from the WaterBody that is to be translated into the Job safe data structure</param>
        /// <returns></returns>
        protected abstract T2 GetJobData(T1 input);
        
        // public abstract method to cleanup the modifier
        public void Cleanup()
        {
            // dispose of the data for each water body
            foreach (var waterBody in WaterBody)
            {
                waterBody.Value.Data.Dispose();
            }
            WaterBody.Clear();
        }
        
        // private method to generate DataHashSet from the modifier data
        private DataHashSet GetDataHashSet(int waterBodyID , T1 input, bool newHash = false)
        {
            return new DataHashSet(waterBodyID, input.GetHash(newHash), GetJobData(input));
        }
        
        protected override string DebugGUI()
        {
            var str = "";
            
            str += $"<b>Water Body Datasets: {WaterBody.Count}</b>\n";
            foreach (var waterBody in WaterBody)
            {
                if(waterBody.Key == null) continue; 
                str += $"{waterBody.Key.name} : {waterBody.Value.Data.GetType()}\n";
            }

            return str;
        }
    }
    
    /// <summary>
    /// IModifierData is a base interface that should hold the serialized data
    /// on the WaterBody that is needed for this system to function.
    /// </summary>
    public abstract class IModifierData
    {
        private Hash128 _dataHash;
        
        public abstract void Hash(ref Hash128 hash);
        
        public Hash128 GetHash(bool forceRecalculation = false)
        {
            if (_dataHash.isValid && !forceRecalculation) return _dataHash;
            _dataHash = new Hash128();
            Hash(ref _dataHash);
            return _dataHash;
        }
    }
    
    /// <summary>
    /// IJobData is a base interface that should hold the job safe data that is passed to the C# Job for calculation.
    /// </summary>
    public interface IJobData
    {
        public void Dispose();
    }
}