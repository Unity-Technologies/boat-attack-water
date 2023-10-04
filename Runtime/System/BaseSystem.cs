using System;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WaterSystem
{
    [DefaultExecutionOrder(-1), ExecuteAlways]
    public abstract class BaseSystem : MonoBehaviour
    {
        // dictionary of types and an instance of BaseSystem
        private static readonly Dictionary<Type, BaseSystem> TypeToInstance = new();
        
        // abstract method for BaseSystem initialization, should be used as a replacement for OnEnable
        protected abstract void Init();

        #region Instance Utils

        protected virtual void OnEnable()
        {
            // if type of this is not in the dictionary then add it otherwise destroy this
            if (!TypeToInstance.ContainsKey(GetType()))
            {
                TypeToInstance.Add(GetType(), this);
            }
            else if (TypeToInstance[GetType()] == null)
            {
                TypeToInstance[GetType()] = this;
            }
            else if(TypeToInstance[GetType()] != this)
            {
                CoreUtils.Destroy(this);
            }

            Init();
        }
        
        // static method to return the instance of the given type
        public static T GetInstance<T>() where T : BaseSystem
        {
            // if the type is in the dictionary then return the instance
            if (TypeToInstance.ContainsKey(typeof(T)))
            {
                return (T)TypeToInstance[typeof(T)];
            }
            // otherwise try to find the instance and add it to the dictionary
            var instance = FindObjectByTypeSafe<T>();
            if (instance == null) return null;
            TypeToInstance.Add(typeof(T), instance);
            return instance;
        }
        
        // method to clear the dictionary
        public static void Clear()
        {
            TypeToInstance.Clear();
        }
        
        // method to get all instances from the dictionary
        public static BaseSystem[] GetAllInstances()
        {
            return TypeToInstance.Values.ToArray();
        }
        
        #endregion
        
        #region Debug Utils

        
        // gets the debug string
        public string GetDebugString()
        {
            var returnStr = DebugGUI();
            if(returnStr == null) return DebugStringBase() + "\n";
            return DebugStringBase() + "\n" + returnStr + "\n";
        }
        
        // debug string base method
        private string DebugStringBase()
        {
            // debug the current and previous state
            return $"<size=30>{GetType().Name}</size>\n<size=16>State: {GetStateString(CurrentState)} <color=grey>Last: {_previousState}</color></size>";
        }
        
        // public virtual method for debug gui, override this to add debug gui for your system
        protected virtual string DebugGUI()
        {
            return null;
        }
        
        private string GetStateString(State state)
        {
            return state switch
            {
                State.None => $"<color=grey>{state}</color>",
                State.Setup => $"<color=yellow>{state}</color>",
                State.Ready => $"<color=green>{state}</color>",
                State.Cleanup => $"<color=red>{state}</color>",
                _ => state.ToString()
            };
        }

        #endregion
        
        #region State Machine

        // current state
        public State CurrentState { get; private set; }
        
        // previous state
        private State _previousState;

        // state change delegate
        public delegate void StateChange(State state);
        
        // state change event
        public event StateChange OnStateChange;

        public void ChangeState(State state)
        {
            // if current state is null then set current state to state
            //CurrentState ??= state;
            // if state is the same as previous state then return
            if (Equals(state, _previousState)) return;
            // set previous state to current state
            _previousState = CurrentState != _previousState ? CurrentState : _previousState;
            // set current state to state
            CurrentState = state;
            // debug log the current and previous states
            if(Debug.isDebugBuild)
                Debug.Log($"{GetType().Name} State change:{_previousState.ToString()}>{CurrentState.ToString()}");
            // invoke state change event
            OnStateChange?.Invoke(state);
        }

        public enum State
        {
            None,
            Setup,
            Ready,
            Cleanup,
        }
        
        #endregion

        #region Generic Utils
        
        // static method to find objects of type by given type and return the result
        public static T2 FindObjectByTypeSafe<T2>(FindObjectsInactive inactive = FindObjectsInactive.Exclude) where T2 : Object
        {
            // wrap below in unity version define
#if UNITY_2020_2_OR_NEWER
            return FindFirstObjectByType<T2>(inactive);
#else
            return FindObjectOfType<T2>();
#endif
        }
        
        public static T2[] FindObjectsByTypeSafe<T2>(FindObjectsInactive inactive = FindObjectsInactive.Exclude) where T2 : Object
        {
            // wrap below in unity version define
#if UNITY_2020_2_OR_NEWER
            return FindObjectsByType<T2>(inactive, FindObjectsSortMode.None);
#else
            return FindObjectsOfType<T2>();
#endif
        }

        #endregion
    }
}