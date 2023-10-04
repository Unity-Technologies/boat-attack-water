using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace WaterSystem.Physics
{
    [ExecuteAlways]
    public class DebugWaterCPU : WaterQuery
    {
        public int2 sampleDimensions = new int2(10, 10);
        public float sampleSpacing = 1f;
        public bool meshDebug;
        public Mesh debugMesh;
        public Material debugMaterial;
        // owned data
        // internal array of Sample Positions, in local space
        private Data.WaterSample[] samplePositions;
        // internal array of Sample Results
        private Data.WaterSurface[] sampleResults;

        // Update Query Count based on sample dimensions
        private void OnValidate()
        {
            samplePositions = GetSamplePositions();
        }

        public override void SetQueryPositions(ref NativeSlice<Data.WaterSample> positions)
        {
            // foreach sample position, translate it into worldspace then set the position in the positions array
            for (var i = 0; i < samplePositions.Length; i++)
            {
                var pos = samplePositions[i];
                pos.Position = transform.TransformPoint(pos.Position);
                pos.InstanceID = WaterQueryId;
                positions[i] = pos;
            }
        }

        private void Update()
        {
            samplePositions ??= GetSamplePositions();
            
            if (samplePositions.Length != sampleDimensions.x * sampleDimensions.y)
            {
                samplePositions = GetSamplePositions();
            }

            if (meshDebug && (debugMesh != null || debugMaterial != null) && sampleResults != null)
            {
                foreach (var waterSurface in sampleResults)
                {
                    // draw the mesh 1/4 the size, at the sample position and facing up along the normal
                    var scale = Vector3.one * 0.25f;
                    var pos = waterSurface.Position;
                    var rot = Quaternion.LookRotation(Vector3.forward, waterSurface.Normal);
                    var matrix = Matrix4x4.TRS(pos, rot, scale);
                    Graphics.DrawMesh(debugMesh, matrix, debugMaterial, 0);
                }
            }
        }

        public override void GetQueryResults(NativeSlice<Data.WaterSurface> surfaceResults)
        {
            sampleResults = surfaceResults.ToArray();
            // Do something with the positions
        }
        
        // OnGizmos draws the sample points in the editor as a 2D plane
        private void OnDrawGizmosSelected()
        {
            // draw the samplePositions but transform them into world space first
            if (samplePositions == null) return;
            foreach (var pos in samplePositions)
            {
                Gizmos.DrawSphere(transform.TransformPoint(pos.Position.xyz), 0.1f);
            }
        }
        
        // method to return an array of sample positions based on the sample dimensions and spacing, in local space
        private Data.WaterSample[] GetSamplePositions()
        {
            var halfDimensions = (float2)sampleDimensions / 2f - 0.5f;
            var offset = new float3(-halfDimensions.x, 0f, -halfDimensions.y) * sampleSpacing;
            var spacing = new float3(sampleSpacing, 0f, sampleSpacing);
            
            var samples = new Data.WaterSample[sampleDimensions.x * sampleDimensions.y];
            for (var x = 0; x < sampleDimensions.x; x++)
            {
                for (var y = 0; y < sampleDimensions.y; y++)
                {
                    var pos = offset + new float3(x, 0f, y) * spacing;
                    Data.WaterSample sample = new Data.WaterSample();
                    sample.Position = pos;
                    sample.InstanceID = WaterQueryId;
                    samples[x + y * sampleDimensions.x] = sample;
                }
            }
            QueryCount = samples.Length;
            return samples;
        }
        
    }
}