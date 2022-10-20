using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace WaterSystem
{
    [DisallowMultipleComponent, ExecuteAlways]
    public class WaterEvent : MonoBehaviour
    {
        public List<WaterCollisionState> collisionEvents;
        public List<WaterSubmergedState> submergedEvents;
        [ReadOnly, HideInInspector]public WaterEventType currentState;
        private WaterEventType _previousState;
        
        private NativeArray<float3> _samplePosition;
        private float3[] _position = new float3[1];
        private float3[] _normal = new float3[1];
        private bool _prevSubmerged;
        private bool _submerged;

        private void OnEnable()
        {
            UpdateSamplePoint();
        }

        private void OnDisable()
        {
            GerstnerWavesJobs.RemoveSamplePoints(gameObject.GetInstanceID());
            Cleanup();
        }

        private void Update()
        {
            if(transform.hasChanged)
                UpdateSamplePoint();
            GerstnerWavesJobs.UpdateSamplePoints(ref _samplePosition, gameObject.GetInstanceID());
            GerstnerWavesJobs.GetData(gameObject.GetInstanceID(), ref _position, ref _normal);
            
            CheckState();
            
            if (!Application.isPlaying) return;

            foreach (var waterCollisionState in collisionEvents.Where(_ => _previousState != currentState))
            {
                waterCollisionState.Invoke(currentState);
            }
            foreach (var waterSubmergedState in submergedEvents.Where(_ => _prevSubmerged != _submerged))
            {
                waterSubmergedState.Invoke(currentState == WaterEventType.Submerged);
            }
        }

        private void UpdateSamplePoint()
        {
            if (!_samplePosition.IsCreated)
            {
                _samplePosition = new NativeArray<float3>(1, Allocator.Persistent);
            }
            _samplePosition[0] = transform.position;
        }

        private void CheckState()
        {
            _previousState = currentState;
            _prevSubmerged = _submerged;
            
            var facing = math.dot(_normal[0], math.normalize((float3)transform.position - _position[0]));
            _submerged = facing < 0.0f;

            if (_submerged)
            {
                currentState = _prevSubmerged ? WaterEventType.Submerged : WaterEventType.Entered;
            }
            else
            {
                currentState = _prevSubmerged ? WaterEventType.Exited : WaterEventType.None;
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if(_samplePosition.IsCreated)
                _samplePosition.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
            #if UNITY_EDITOR
            Color color;
            
            switch (currentState)
            {
                case WaterEventType.Submerged:
                    color = Color.blue;
                    break;
                case WaterEventType.Entered:
                    color = Color.green;
                    break;
                case WaterEventType.Exited:
                    color = Color.red;
                    break;
                default:
                    color = Color.white;
                    break;
            }
            Handles.color = color;
            var normal = _position[0] + _normal[0];
            
            Handles.DrawWireDisc(_position[0], _normal[0], 0.5f, 1f);
            Handles.DrawLine(_position[0], normal, 1f);
            #endif
        }
    }

    [Serializable] public class WaterCollisionState : UnityEvent<WaterEventType> { }
    [Serializable] public class WaterSubmergedState : UnityEvent<bool> { }

    public enum WaterEventType
    {
        None,
        Submerged,
        Entered,
        Exited
    }
}