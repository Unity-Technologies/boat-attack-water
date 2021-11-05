using UnityEngine;
using UnityEngine.Rendering;

namespace WaterSystem
{
    [ExecuteAlways]
    public class DepthGenerator : MonoBehaviour
    {
        public static DepthGenerator Current;
        [HideInInspector, SerializeField] internal Texture2D depthTile;

        private static readonly int Depth = Shader.PropertyToID("_Depth");
        [HideInInspector, SerializeField] private Mesh mesh;
        [HideInInspector, SerializeField] private Material debugMaterial;
        [HideInInspector, SerializeField] private Shader shader;
        private Camera _depthCam;
        private Material _material;

        public int size = 250;
        public int tileRes = 1024;

        #if UNITY_EDITOR
        [ContextMenu("Capture Depth")]
        public void CaptureDepth()
        {
            DepthBaking.CaptureDepth(tileRes, size, transform);
            Current = this;
        }
        #endif

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