using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using UnityPhysics = UnityEngine.Physics;

namespace WaterSystem.Physics
{
    public class Utilities
    {
        #region Constants

        public const float Dampner = 0.0025f;
        public const float WaterDensity = 1000;

        #endregion

        #region Properties
        
        private static float3[] _velocityPoints = new float3[1024];
        private static PhysicsForce[] _buoyancyForces = new PhysicsForce[1024];
        private static readonly PhysicsForce EmptyForce = default;

        #endregion

        #region Functions

        public static float GetVolumeFromVoxels(int voxelCount, float voxelResolution)
        {
            var voxelVolume = Mathf.Pow(voxelResolution, 3f) * voxelCount;
            return voxelVolume;
        }
        
        public static float3 GetLocalArchimedesForce(int voxelCount, float volume)
        {
            var archimedesForceMagnitude = WaterDensity * Mathf.Abs(UnityPhysics.gravity.y) * volume;
            return new float3(0, archimedesForceMagnitude, 0) / voxelCount;
        }
        
        public static PhysicsForce GetBuoyancyForce(Vector3[] points, NativeSlice<Data.WaterSurface> surfaceData, float voxelResolution, Rigidbody rigidbody, float3 localArchimedesForce)
        {
            var totalForce = new PhysicsForce(float3.zero, float3.zero);
            
            for (var index = 0; index < points.Length; index++)
            {
                var point = points[index];
                var force = BuoyancyForce(point, GetVelocityPoint(point, rigidbody), surfaceData[index],
                    voxelResolution, points.Length, rigidbody.mass, localArchimedesForce, ref totalForce.Submerged);
                
                totalForce.Force += force.Force;
                totalForce.Torque += math.cross((Vector3)force.Position - rigidbody.worldCenterOfMass, force.Force);
            }

            return totalForce;
        }

        private static PhysicsForce BuoyancyForce(float3 position, float3 velocity, Data.WaterSurface waterSurface,
            float voxelResolution, int voxelCount, float mass, float3 localArchimedesForce, ref float submergedAmount)
        {
            var sphereVoxelRadius = voxelResolution * 0.63f;
            var depth = waterSurface.Position.y - (position.y - sphereVoxelRadius);

            if (!(depth > 0f)) return EmptyForce;

            // submerged percentage 0-1
            var k = Mathf.InverseLerp(0, sphereVoxelRadius * 2f, depth);
            submergedAmount += k / voxelCount;

            var localDampingForce = Dampner * mass * -velocity;
            var force = localDampingForce + math.sqrt(k) * localArchimedesForce;
            
            return new PhysicsForce(force, position);
        }

        private static float3 GetVelocityPoint(float3 point, Rigidbody rigidbody)
        {
            return rigidbody.GetPointVelocity(point);
        }

        #endregion

        #region Data

        public struct PhysicsForce
        {
            public float3 Force;
            public float3 Position;
            public float3 Torque;
            public float Submerged;
            
            public PhysicsForce(float3 force, float3 position)
            {
                Force = force;
                Position = position;
                Torque = float3.zero;
                Submerged = 0f;
            }
        }

        #endregion
    }
}