using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WaterSystem.Physics;

namespace WaterSystem
{
    /// <summary>
    /// WaterBody is a base class for all water bodies, it helps with registering and unregistering water bodies
    /// and do communication with the WaterManager and related systems.
    /// </summary>
    [ExecuteAlways]
    public abstract class WaterBody : MonoBehaviour
    {
        // Physics bounds of the water body
        [SerializeField, HideInInspector] private Bounds bounds;
        private Bounds _worldBounds;

        internal Dictionary<Type, IModifierData> Modifiers = new();

        // Water Shape
        public WaterShape shape = new WaterShape()
        {
            type = WaterShapeType.Plane,
            size = Vector2.one * 250,
            range = new Vector2(4, 10),
        };
        
        private void OnEnable()
        {
            Setup();
        }

        protected virtual void Setup()
        {
            if(BaseSystem.GetInstance<WaterManager>() != null) WaterManager.Register(this);
            
            // setup base bounds
            bounds = new Bounds(Vector3.zero, new Vector3(shape.size.x, 10, shape.size.y));
            
            SetupModifiers();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        protected virtual void Cleanup()
        {
            if(BaseSystem.GetInstance<WaterManager>()) WaterManager.Unregister(this);
        }

        /// <summary>
        /// Override this method to render the water body, for example the mesh, setup the materials, etc.
        /// </summary>
        /// <param name="cam"></param>
        public abstract void Render(Camera cam, ScriptableRenderer scriptableRenderer);
        
        /// <summary>
        /// Override this method to setup the water modifiers, for example Gerstner Waves, Flow, etc.
        /// </summary>
        protected abstract void SetupModifiers();
        
        // abstract method to return the types of water modifiers that this water body supports
        public abstract Type[] GetModifierTypes();
        
        // method to check if this water body supports the given type of water modifier
        public bool SupportsModifier<T>() where T : IModifierData
        {
            return Modifiers.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// Get a modifier of the given type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="modifier">returns the modifier if found, otherwise null</param>
        /// <returns></returns>
        public bool TryGetModifier<T>(out T modifier) where T : IModifierData
        {
            if (Modifiers.TryGetValue(typeof(T), out var data))
            {
                modifier = (T) data;
                return true;
            }

            modifier = default;
            return false;
        }
        
        // method to get the water body bound in world space
        public Bounds GetBounds()
        {
            return shape.GetBounds();
        }
        
        #region Data
        
        [Serializable]
        public struct WaterShape
        {
            public WaterShapeType type;
            public float2 size;
            public float Radius => size.x * 0.5f;
            public float2 range;
            private Bounds _boundsWS;
            
            public Bounds GetBounds()
            {
                if (_boundsWS == null)
                    _boundsWS = new Bounds();
                
                var totalRange = range.x + range.y;
                _boundsWS.center = new Vector3(0f,  range.x - (totalRange * 0.5f), 0f);
                _boundsWS.size = new Vector3(size.x, totalRange,  type == WaterShapeType.Circle ? size.x : size.y);
                
                return _boundsWS;
            }
        }
        
        [Serializable]
        public enum WaterShapeType
        {
            Infinite,
            Plane,
            Circle,
        }

        #endregion

        #region Debug

        public virtual void OnDrawGizmos()
        {
            DrawBoundsDebug();
        }
        
        private void DrawBoundsDebug()
        {
            var color = Color.green;
            color.a = 0.25f;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = color;
            var bounds = shape.GetBounds();
            Gizmos.DrawWireCube(bounds.center, bounds.extents * 2);
        }

        #endregion
    }
}