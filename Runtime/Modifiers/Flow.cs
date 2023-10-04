using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace WaterSystem.Physics
{
    public class Flow : WaterModifier<Flow.FlowData, Flow.FlowDataStruct>
    {
        protected override void Init()
        {
            ChangeState(State.Setup);
            // do setup
            
            // listen to the state to change on this modifier
            OnStateChange += OnOnStateChange;
            
            ChangeState(State.Ready);
        }

        private void OnOnStateChange(State state)
        {
            if (state == State.Cleanup)
            {
                Cleanup();
            }
        }

        public override JobHandle EnqueueJob(ref NativeArray<Data.WaterSample> queryPositions, 
            ref NativeArray<Data.WaterSurface> waterSurface, ref JobHandle handle, NativeArray<WaterPhysics.WaterBodyData> waterBodyData, DataHashSet dataset)
        {
            // TODO - Calculate the flow
            return handle;
        }

        protected override FlowDataStruct GetJobData(FlowData input)
        {
            var data = new FlowDataStruct
            {
                BaseFlowVector = input.baseFlowVector
            };
            for (int i = 0; i < data.FlowDatabase.Length; i++)
            {
                data.FlowDatabase[i] = input.baseFlowVector;
            }

            return data;
        }
        
        #region Jobs

        [BurstCompile]
        private struct FlowJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> Position;
            
            public NativeArray<Data.WaterSurface> Output;

            // The code actually running on the job
            public void Execute(int i)
            {
                var surface = Output[i];
                // TODO - Calculate the flow                
                Output[i] = surface;
            }
        }

        #endregion

        #region Data

        [Serializable]
        public class FlowData : IModifierData
        {
            public float2 baseFlowVector = Vector2.up;
            
            public override void Hash(ref Hash128 hash)
            {
                hash.Append(baseFlowVector.GetHashCode());
            }
        }

        public struct FlowDataStruct : IJobData
        {
            public float2 BaseFlowVector;
            public NativeArray<float2> FlowDatabase;
            
            public void Dispose()
            {
                FlowDatabase.Dispose();
            }
        }

        #endregion
        
    }
}