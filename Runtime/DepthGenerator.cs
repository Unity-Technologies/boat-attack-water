using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaterSystem
{
    [ExecuteAlways]
    [AddComponentMenu("URP Water System/Depth Generator")]
    public class DepthGenerator : MonoBehaviour
    {
        public static DepthGenerator Current;
        private static List<DepthGenerator> _generators = new List<DepthGenerator>();
        [SerializeField] internal Texture2D depthTile;

        private static readonly int Depth = Shader.PropertyToID("_Depth");
        [HideInInspector, SerializeField] private Mesh mesh;
        [HideInInspector, SerializeField] private Material debugMaterial;
        [HideInInspector, SerializeField] private Shader shader;
        private float[,] _depthValues;
        private Camera _depthCam;
        private Material _material;

        public int size = 250;
        public int tileRes = 1024;

        public float range = 20;
        public float offset = 4;
        public LayerMask mask = new LayerMask();

        private static readonly float maxDepth = -999f;

        #if UNITY_EDITOR
        [ContextMenu("Capture Depth")]
        public void CaptureDepth()
        {
            DepthBaking.CaptureDepth(tileRes, size, transform, mask, range, offset);
            Current = this;
        }
        #endif

        private void OnEnable()
        {
            if (depthTile == null)
            {
                #if UNITY_EDITOR
                var activeScene = gameObject.scene;
                var sceneName = activeScene.name.Split('.')[0];
                var path = activeScene.path.Split('.')[0];
                var file = $"{gameObject.name}_DepthTile.png";
                try
                {
                    depthTile = AssetDatabase.LoadAssetAtPath<Texture2D>($"{path}/{file}");
                    StoreDepthValues();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to load {GetType().Name} tile, please make sure it is generated:{e}");
                    throw;
                }
                #else
                Debug.LogWarning($"{GetType().Name} on gameobject {gameObject.name} is missing tile texture");
                #endif
            }
            if(!_generators.Contains(this))
                _generators.Add(this);
        }

        private void OnDestroy()
        {
            if (_generators.Contains(this))
                _generators.Remove(this);
        }

        public float GetDepth(float2 UVPos)
        {
            UVPos = math.clamp(UVPos, 0, 0.999f);
            var depth = 1 - _depthValues[(int)(UVPos.x * tileRes), (int)(UVPos.y * tileRes)];
            return -(depth * (range + offset)) + offset;
        }

        public float GetDepth(Vector3 position)
        {
            var UVPos = GetUVPositon(position);
            if (UVPos.x is > 1 or < 0 || UVPos.y is > 1 or < 0)
                return maxDepth;
            return GetDepth(UVPos);
        }

        private float2 GetUVPositon(Vector3 position) { return GetUVPositon(new float2(position.x, position.z)); }
        
        private float2 GetUVPositon(Vector2 position) { return GetUVPositon(position); }
        
        private float2 GetUVPositon(float2 position)
        {
            var goPos = transform.position;
            position.x -= goPos.x;
            position.y -= goPos.z;
            position *= 1f / size;
            position += 0.5f;
            
            return position.yx;
        }

        private void LateUpdate()
        {
            StoreDepthValues();
            
            if (shader && !_material) _material = CoreUtils.CreateEngineMaterial(shader);

            if (!depthTile || !_material) return;

            var matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(90, 0, 0), new Vector3(size, size, 0f));
            _material.SetTexture(Depth, depthTile);
            Graphics.DrawMesh(mesh, matrix, _material, 0);
        }

        private void StoreDepthValues()
        {
            if ((_depthValues == null || _depthValues.Length != tileRes * tileRes) && depthTile)
            {
                _depthValues = new float[tileRes, tileRes];
                
                var pixels = depthTile.GetPixels();
                for (var i = 0; i < depthTile.width; i++)
                {
                    for (var j = 0; j < depthTile.height; j++)
                    {
                        var pixel = pixels[(i * depthTile.width) + j];
                        _depthValues[i, j] = pixel.r;
                    }
                }
            }
        }

        public static float GetGlobalDepth(float3 samplePos)
        {
            var depth = maxDepth;
            foreach (var depthGenerator in _generators)
            {
                depth = depthGenerator.GetDepth(samplePos);
            }
            return depth;
        }

        private void OnDrawGizmosSelected()
        {
            #if UNITY_EDITOR
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, new Vector3(size, 0f, size));
            if (mesh && depthTile)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(90, 0, 0),
                    new Vector3(size, size, 0f));
                debugMaterial.mainTexture = depthTile;
                debugMaterial.SetPass(0);
                Graphics.DrawMeshNow(mesh, matrix);
            }

            if (_depthValues != null && _depthValues.Length != 0)
            {
                const float dist = 100f;
                const int count = 10;

                var pos = Camera.current.transform.position;
                
                for (var i = 0; i < count; i++)
                {
                    for (var j = 0; j < count; j++)
                    {
                        var flatPos = pos + new Vector3(i - count / 2, 0f, j - count / 2) * (dist / count);
                        flatPos.y = transform.position.y;
                        var UVPos = GetUVPositon(flatPos);
                        var depth = GetDepth(UVPos);
                        var alpha = Mathf.InverseLerp(dist/2f, 0f, Vector3.Distance(pos, flatPos));
                        GUIStyle style = new GUIStyle(EditorStyles.label);
                        style.normal.textColor = Handles.color = new Color(1f, 1f, 1f, alpha);
                        Handles.Label(flatPos, $"{depth:F4}\n(x:{UVPos.x:F4}, z:{UVPos.y:F4})", style);
                        Handles.DrawDottedLine(flatPos, flatPos + Vector3.up * depth, 5f);
                        Handles.DrawSolidDisc(flatPos + Vector3.up * depth, Vector3.up, 0.1f);
                    }
                }
            }
            #endif
        }
    }
}