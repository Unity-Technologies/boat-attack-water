using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace WaterSystem.Physics
{
    public class Depth : WaterModifier<Depth.DepthData, Depth.JobData>
    {
        #region ModifierSpecific

        protected override void Init()
        {
            ChangeState(State.Setup);
            // do setup
            ChangeState(State.Ready);
        }

        public override JobHandle EnqueueJob(ref NativeArray<Data.WaterSample> queryPositions, 
            ref NativeArray<Data.WaterSurface> waterSurface, ref JobHandle handle,
            NativeArray<WaterPhysics.WaterBodyData> waterBodyData, DataHashSet data)
        {
            // TODO - implement depth jobs to calm the waves, currently just pass through.
            return handle;
        }

        protected override JobData GetJobData(DepthData input)
        {
            // TODO - just return a new data for now
            return new JobData();
        }

        #endregion

        #region Data

        [Serializable]
        public class DepthData : IModifierData
        {
            // LayerMask to control which objects are rendered into the depth map
            public LayerMask renderLayer = new LayerMask();
            // Texel density of the depth map, this maps 1:1 with a unity unit
            public float texelSize = 1;
            // The depth map in texture form for rendering into the WaterFX buffers
            private Texture2D _depthMap;
            // The depth map in byte form for saving to disk/serializing
            [SerializeField] private byte[] depthMapBytes;
            [SerializeField] private int2 depthMapSize;
            
            public Texture2D DepthMap
            {
                get
                {
                    if (_depthMap != null) return _depthMap;
                    if (depthMapBytes == null || depthMapBytes.Length == 0)
                        return null;
                    BytesToDepthMap();

                    return _depthMap;
                }
                set
                {
                    _depthMap = value;
                    depthMapSize = new int2(_depthMap.width, _depthMap.height);
                    DepthMapToBytes();
                }
            }
            
            public void Initialize(WaterBody waterBody, Matrix4x4 localToWorldMatrix)
            {
                if(depthMapBytes != null && depthMapBytes.Length > 0) BytesToDepthMap();
#if UNITY_EDITOR // TODO - should be able to bake depth in player
                DepthBaking.CaptureDepth(waterBody, localToWorldMatrix, renderLayer);
#endif
            }
            
            private void DepthMapToBytes()
            {
                depthMapBytes = _depthMap.GetRawTextureData();
            }
            
            private void BytesToDepthMap()
            {
                if (_depthMap == null)
                    _depthMap = new Texture2D(depthMapSize.x, depthMapSize.y, TextureFormat.R8, false);
                _depthMap.LoadRawTextureData(depthMapBytes);
                _depthMap.Apply();
            }

            public override void Hash(ref Hash128 hash)
            {
                hash.Append(texelSize);
                hash.Append(depthMapBytes);
                hash.Append(renderLayer.value);
            }
        }

        public struct JobData : IJobData
        {
            public NativeArray<float> DepthValues;
            public MapData _mapData;

            public void Dispose()
            {
                DepthValues.Dispose();
            }
            
            public struct MapData
            {
                public readonly int TileRes;
                public readonly int Size;
                public readonly half Range;
                public readonly half Offset;
                public readonly float3 PositionWS;

                public MapData(float3 position, int size, int tileRes, half range, half offset)
                {
                    PositionWS = position;
                    Size = size;
                    TileRes = tileRes;
                    Offset = offset;
                    Range = range;
                }
            }
        }

        #endregion
    }
}