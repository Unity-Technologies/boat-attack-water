using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using WaterSystem.Physics;

namespace WaterSystem
{
    [DefaultExecutionOrder(-100)]
    public static class SystemInitializer
    {
        // WaterSystemGameObject
        private static GameObject _gameObject;
        internal static bool Initialized;
        private const string _gameObjectName = "[WaterSystem]";

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        internal static void Init()
        {
            if(Initialized) return;
            
            if (_gameObject == null)
            {
                _gameObject = GameObject.Find(_gameObjectName);
                if (_gameObject == null)
                {
                    _gameObject = new GameObject(_gameObjectName);
                }
            }
            _gameObject.hideFlags = HideFlags.DontSave;
            AddSystems();
            Initialized = true;
        }
        
        
        private static void AddSystems()
        {
            BaseSystem.Clear();
            
            var types = new List<Type>();
            types.AddRange(new []{typeof(WaterManager), typeof(WaterPhysics)});
            
            var allTypes = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                allTypes.AddRange(assembly.GetTypes());
            }
            // remove all types that do not have a BaseType
            allTypes.RemoveAll(type => type.BaseType == null);
            // remove all types that are abstract
            allTypes.RemoveAll(type => type.IsAbstract);
            // remove all types that do not inherit from WaterModifier<,>
            allTypes.RemoveAll(type => !type.BaseType.IsGenericType || type.BaseType.GetGenericTypeDefinition() != typeof(WaterModifier<,>));
            // sort the list by name
            allTypes.Sort((t1, t2) => string.Compare(t1.FullName, t2.FullName, StringComparison.Ordinal));
            
            foreach (var type in allTypes)
            {
                if (!types.Contains(type))
                {
                    types.Add(type);
                }
            }

            var typesDebug = "Adding types:\n";
            // add all types to the gameobject
            foreach (var type in types)
            {
                AddSystem(type);
                typesDebug += $"{type}\n";
            }
            
            Debug.Log(typesDebug);
        }

        private static void AddSystem(Type type)
        {
            if(!_gameObject.TryGetComponent(type, out _))
                _gameObject.AddComponent(type);
        }
    }
}