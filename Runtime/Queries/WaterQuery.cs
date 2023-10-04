using Unity.Collections;
using UnityEngine;

namespace WaterSystem.Physics
{
    public abstract class WaterQuery : MonoBehaviour, IWaterQuery
    {
        public int WaterQueryId => GetInstanceID();

        public NativeArray<Data.WaterSample> samplePoints;
        
        private int queryCount;
        private int previousQueryCount;

        // public get/set for the query count
        public int QueryCount
        {
            get
            {
                return queryCount;
            }
            set
            {
                if (value != queryCount)
                {
                    previousQueryCount = queryCount;
                    queryCount = value;
                    BaseSystem.GetInstance<WaterPhysics>().AddQuery(this);
                }
            }
        }
        
        public virtual void OnEnable()
        {
            // remove this query from the WaterPhysics instance
            if(queryCount > 0)
                BaseSystem.GetInstance<WaterPhysics>().AddQuery(this);
        }
        
        // This method sets the Query Count from the input parameter
        private void SetQueryCount(int count)
        {
            if (count != queryCount)
            {
                previousQueryCount = queryCount;
                queryCount = count;
                BaseSystem.GetInstance<WaterPhysics>().AddQuery(this);
            }
        }
        
        // This method returns the Query Count
        private int GetQueryCount()
        {
            return queryCount;
        }

        private void OnDisable()
        {
            // remove this query from the WaterPhysics instance
            BaseSystem.GetInstance<WaterPhysics>().RemoveQuery(this);
        }

        private void OnDestroy()
        {
            BaseSystem.GetInstance<WaterPhysics>().RemoveQuery(this);
        }

        // This method is used to set the world space positions of the query points.
        public abstract void SetQueryPositions(ref NativeSlice<Data.WaterSample> samplePositions);
        
        // This method is used to get the world space positions of the resulting query points.
        public abstract void GetQueryResults(NativeSlice<Data.WaterSurface> surfaceResults);
    }
    
    public interface IWaterQuery
    {
        public int WaterQueryId { get; }
    }
}