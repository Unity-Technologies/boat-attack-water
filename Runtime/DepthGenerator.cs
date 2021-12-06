using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaterSystem
{
    [ExecuteAlways]
    public class DepthGenerator : MonoBehaviour
    {
        public static DepthGenerator Current;
        [SerializeField] internal Texture2D depthTile;

        private static readonly int Depth = Shader.PropertyToID("_Depth");
        [HideInInspector, SerializeField] private Mesh mesh;
        [HideInInspector, SerializeField] private Material debugMaterial;
        [HideInInspector, SerializeField] private Shader shader;
        private Camera _depthCam;
        private Material _material;

        public int size = 250;
        public int tileRes = 1024;
        public LayerMask mask = new LayerMask();

        #if UNITY_EDITOR
        [ContextMenu("Capture Depth")]
        public void CaptureDepth()
        {
            DepthBaking.CaptureDepth(tileRes, size, transform, mask);
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
        }

        private void LateUpdate()
        {
            if (shader && !_material) _material = CoreUtils.CreateEngineMaterial(shader);

            if (!depthTile || !_material) return;

            var matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(90, 0, 0), new Vector3(size, size, 0f));
            _material.SetTexture(Depth, depthTile);
            Graphics.DrawMesh(mesh, matrix, _material, 0);
        }

        private void OnDrawGizmosSelected()
        {
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
        }
    }
}