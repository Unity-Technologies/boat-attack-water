using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace WaterSystem.Physics
{
    public class VoxelBuoyantObject : WaterQuery
    {
        public new Rigidbody rigidbody;
        public float voxelSpacing = 1f;
        
        [SerializeField, HideInInspector] private float voxelSpacingOld = 0f;
        [SerializeField] private float3[] voxels;
        private Vector3[] voxelsWS;
        private NativeArray<float3> _voxelsWorld;
        private NativeSlice<Data.WaterSurface> _surfaceResults;

        [SerializeField] private float volume;
        [SerializeField] private float3 localArchimedesForce;
        private Utilities.PhysicsForce _buoyantForce;

        private float _baseDrag;
        private float _baseAngularDrag;
        
        private const int MaxVoxels = 4096;

        private void OnValidate() // TODO clean hack this up
        {
            if (Mathf.Abs(voxelSpacing - voxelSpacingOld) > math.EPSILON)
            {
                voxels = SliceIntoVoxels(transform, rigidbody, voxelSpacing, out volume, out localArchimedesForce);
                voxelSpacingOld = voxelSpacing;
            }
        }

        private void Start()
        {
            QueryCount = voxels.Length;
            voxelsWS = new Vector3[voxels.Length];
            _baseDrag = rigidbody.drag;
            _baseAngularDrag = rigidbody.angularDrag;
        }

        private void FixedUpdate()
        {
            if (_surfaceResults is not {Length: > 0}) return;
            _buoyantForce = Utilities.GetBuoyancyForce(voxelsWS, _surfaceResults, voxelSpacing, rigidbody,
                localArchimedesForce);
            rigidbody.AddForce(_buoyantForce.Force);
            rigidbody.AddTorque(_buoyantForce.Torque);
        }

        private void Update()
        {
            LocalToWorld();
            UpdateDrag();
        }

        #region WaterQuery

        public override void SetQueryPositions(ref NativeSlice<Data.WaterSample> samplePositions)
        {
            if(voxels == null) return;
            // foreach voxel position, translate it into worldspace then set the position in the positions array
            for (var i = 0; i < voxelsWS.Length; i++)
            {
                samplePositions[i] = new Data.WaterSample
                {
                    Position = voxelsWS[i],
                    InstanceID = WaterQueryId
                };
            }
        }

        public override void GetQueryResults(NativeSlice<Data.WaterSurface> surfaceResults)
        {
            _surfaceResults = surfaceResults;
            
            for (int i = 0; i < _surfaceResults.Length; i++)
            {
                var surfaceResult = _surfaceResults[i];
                surfaceResult.Position = float3.zero;
                _surfaceResults[i] = surfaceResult;
            }
        }
        
        #endregion

        #region Voxelization
        
        private static float3[] SliceIntoVoxels(Transform transform, Rigidbody rigidbody, float voxelSpacing, out float volume, out float3 localArchimedesForce)
        {
            // make a list for holding position generated in a grid pattern within the bounds
            var voxelList = new List<float3>();
            // get local colliders
            var colliders = transform.GetComponentsInChildren<Collider>();

            Bounds voxelBounds;
            // if there are no colliders then skip
            if (colliders.Length > 0)
            {
                var voxelBoundsArray = new Bounds[colliders.Length];
                voxelBounds = colliders[0].bounds;
                voxelBoundsArray[0] = colliders[0].bounds;
                for (var index = 1; index < colliders.Length; index++)
                {
                    var col = colliders[index];
                    var bounds = col.bounds;
                    voxelBoundsArray[index] = bounds;
                    voxelBounds.Encapsulate(bounds);
                }

                var boundSize = voxelBounds.size.magnitude + math.distance(voxelBounds.center, transform.position);

                if (boundSize < voxelSpacing)
                {
                    voxelSpacing = boundSize;
                }

                var startOffset = 0f;
                // figure out if boundSize is closer to a multiple of voxelSpacing or multiple of half voxelSpacing
                var halfVoxelSpacing = voxelSpacing * 0.5f;
                var halfRemainder = boundSize % halfVoxelSpacing;
                var remainder = boundSize % voxelSpacing;
                if (halfRemainder < remainder)
                {
                    startOffset = halfVoxelSpacing;
                }
                var voxelCount = math.ceil(boundSize / voxelSpacing);
                var quantizedMaxDistance = voxelCount * voxelSpacing;
                
                if(math.pow(voxelCount, 3) > MaxVoxels)
                    throw new Exception("Too many voxels! Reduce voxel spacing");

                for (var x = startOffset; x < quantizedMaxDistance; x += voxelSpacing)
                {
                    for (var y = startOffset; y < quantizedMaxDistance; y += voxelSpacing)
                    {
                        for (var z = startOffset; z < quantizedMaxDistance; z += voxelSpacing)
                        {
                            var pos = new float3(x, y, z);
                            pos -= quantizedMaxDistance * 0.5f; // offset by half the bounds size
                            if (IsPointInColliders(transform.TransformPoint(pos), colliders.ToArray()))
                            {
                                voxelList.Add(pos);
                            }
                        }
                    }
                }
                
                // set the rigidbody center of mass to the center of the voxel bounds
                rigidbody.centerOfMass = voxelBounds.center;
            }

            if (voxelList.Count == 0)
            {
                // add a single voxel in the center of the bounds
                voxelList.Add(float3.zero);
            }
            
            // get the volume
            volume = Utilities.GetVolumeFromVoxels(voxelList.Count, voxelSpacing);
            // get the local archimedes force
            localArchimedesForce = Utilities.GetLocalArchimedesForce(voxelList.Count, volume);
            // set the voxel array
            return voxelList.ToArray();
        }

        private static bool IsPointInColliders(float3 point, Collider[] colliders)
        {
            foreach (var col in colliders)
            {
                var colPoint = col.ClosestPoint(point);
                if (!(math.distance(colPoint, point) < math.EPSILON)) continue;
                return true;
            }

            return false;
        }

        #endregion

        #region Utilities
        
        private void UpdateDrag()
        {
            rigidbody.drag = _baseDrag + _baseDrag * (_buoyantForce.Submerged * 10f);
            rigidbody.angularDrag = _baseAngularDrag + _baseAngularDrag * (_buoyantForce.Submerged * 4f);
        }
        
        private void LocalToWorld()
        {
#if UNITY_2022_2_OR_NEWER
            for (var i = 0; i < voxels.Length; i++)
            {
                voxelsWS[i] = voxels[i];
            }
            transform.TransformPoints(voxelsWS);
#else
            for (var i = 0; i < voxels.Length; i++)
            {
                voxelsWS[i] = transform.TransformPoint(voxels[i]);
            }
#endif
        }
        
        #endregion
        
        private void OnDrawGizmosSelected()
        {
            // draw the voxel positions
            Gizmos.matrix = transform.localToWorldMatrix;
            var color = Color.green;
            color.a = 0.25f;
            Gizmos.color = color;
            if (voxels != null)
            {
                foreach (var voxel in voxels)
                {
                    Gizmos.DrawWireCube(voxel, voxelSpacing * Vector3.one);
                }
            }
        }
    }
}