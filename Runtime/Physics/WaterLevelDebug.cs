using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace WaterSystem.Physics
{
    [ExecuteAlways]
    public class WaterLevelDebug : MonoBehaviour
    {
        public int2 arrayCount = new int2(1, 1);
        public float arraySpacing = 1f;
        
        private NativeArray<float3> samplePositions;
        private float3[] positions;

        private void OnValidate()
        {
            UpdateSamplePoints();
        }

        private void OnEnable()
        {
            UpdateSamplePoints();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            GerstnerWavesJobs.RemoveSamplePoints(gameObject.GetInstanceID());
            if(samplePositions.IsCreated)
                samplePositions.Dispose();
        }

        private void Update()
        {
            UpdateSamplePoints();
            GerstnerWavesJobs.UpdateSamplePoints(ref samplePositions, gameObject.GetInstanceID());
            GerstnerWavesJobs.GetData(gameObject.GetInstanceID(), ref positions);
            
        }

        private void UpdateSamplePoints()
        {
            if (!samplePositions.IsCreated)
            {
                samplePositions = new NativeArray<float3>(arrayCount.x * arrayCount.y, Allocator.Persistent);
            }
            if (samplePositions.Length != arrayCount.x * arrayCount.y)
            {
                GerstnerWavesJobs.RemoveSamplePoints(gameObject.GetInstanceID());
                samplePositions.Dispose();
                samplePositions = new NativeArray<float3>(arrayCount.x * arrayCount.y, Allocator.Persistent);
            }

            float3 pos = 0;
            for (var i = 0; i < arrayCount.x; i++)
            {
                pos.x = arraySpacing * i - arraySpacing * (arrayCount.x-1) * 0.5f;
                for (var j = 0; j < arrayCount.y; j++)
                {
                    pos.z = arraySpacing * j - arraySpacing * (arrayCount.y-1) * 0.5f;
                    samplePositions[i * arrayCount.y + j] = transform.TransformPoint(pos);
                }
            }
            
            if (positions == null || positions.Length != samplePositions.Length)
                positions = new float3[samplePositions.Length];
        }
        
        public float3 GetHeight(int index, float3 samplePos)
        {
            var pos = positions[index];
            //var depth = DepthGenerator.GetGlobalDepth(samplePos);
            //pos.x = samplePos.x;
            //pos.z = samplePos.z;
            //pos.y *= math.saturate(Ocean.Instance.settingsData._waveDepthProfile.Evaluate(1-math.saturate(-depth / 20f)));
            //pos.y += Ocean.Instance.transform.position.y;
            return pos;
        }
        
        private void OnDrawGizmos()
        {
            var colA = new Color(1f, 1f, 1f, 0.025f);
            var colB = new Color(1f, 1f, 1f, 0.75f);
            
            for (var index = 0; index < samplePositions.Length; index++)
            {
                var samplePos = samplePositions[index];
                var finalPos = GetHeight(index, samplePos);
                Gizmos.color = colA;
                Gizmos.DrawSphere(samplePos, 0.1f);
                Gizmos.DrawLine(samplePos, finalPos);
                Gizmos.color = colB;
                Gizmos.DrawSphere(finalPos, 0.1f);
            }
        }
    }
}
