using Unity.Collections;
using UnityEngine;

namespace WaterSystem.Physics
{
    public class SimpleBuoyantObject : WaterQuery
    {
        public Vector3 sampleOffset = Vector3.zero;
        private Data.WaterSurface _surface;
        private void Start()
        {
            QueryCount = 1;
        }

        private void Update()
        {
            var t = transform;
            var vec  = t.position;
            vec.y = _surface.Position.y;
            t.position = vec;
            var up = t.up;
            t.up = Vector3.Slerp(up, _surface.Normal, Time.deltaTime);
        }

        #region WaterQuery

        public override void SetQueryPositions(ref NativeSlice<Data.WaterSample> samplePositions)
        {
            var sample = samplePositions[0];
            sample.Position = transform.position + sampleOffset;
            sample.InstanceID = WaterQueryId;
            samplePositions[0] = sample;
        }
        
        public override void GetQueryResults(NativeSlice<Data.WaterSurface> surfaceResults)
        {
            _surface = surfaceResults[0];
        }
        
        #endregion
    }
}