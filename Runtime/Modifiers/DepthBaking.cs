#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using WaterSystem.Physics;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace WaterSystem
{
    #region ScriptableRenderPass

    class DepthSave : ScriptableRenderPass
    {
        private Shader _shader;
        
        private RenderTexture depthTarget;
        private readonly Material _mat;

        public DepthSave(Shader shader, RenderTexture depthTarget)
        {
            profilingSampler = new ProfilingSampler(nameof(DepthSave));
            this.depthTarget = depthTarget;
            _mat = CoreUtils.CreateEngineMaterial(shader);
            renderPassEvent = RenderPassEvent.AfterRendering;
        }


        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_mat == null) return;

            CommandBuffer cmd = CommandBufferPool.Get();
            cmd.Blit(renderingData.cameraData.targetTexture, depthTarget, _mat, 0);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    #endregion
    
    public static class DepthBaking
    {
        private static Camera _depthCam;
        
        public static void CaptureDepth(WaterBody waterBody, Matrix4x4 matrix, LayerMask mask)
        {
            var package = PackageInfo.FindForAssembly(Assembly.GetAssembly(typeof(DepthBaking)));
            var depthCopyShader = AssetDatabase.LoadAssetAtPath<Shader>(
                $"{package.assetPath}/Runtime/Shaders/Utility/SceneDepth.shader");

            if (depthCopyShader == null)
            {
                Debug.LogError("Failed to load SceneDepth shader for baking.");
                return;
            }

            var tileSize = waterBody.shape.size;
            float texelSize = 1f;
            if(waterBody.TryGetModifier<Depth.DepthData>(out var depth))
                texelSize = depth.texelSize;
            
            CreateDepthCamera(matrix, tileSize, waterBody.shape.range, mask);

            // calculate the resolution of the tile
            var tileResolution = new int2((int) (tileSize.x * texelSize), (int) (tileSize.y * texelSize));
            
            // make power of two and clamp the resolution to a minimum of 8 and a maximum of 4096
            tileResolution = math.clamp(tileResolution, 4, 4096);
            tileResolution = math.ceilpow2(tileResolution);

            if (depthCopyShader == null) return;
            var buffer = RenderTexture.GetTemporary(tileResolution.x, tileResolution.y, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var bufferDepth = RenderTexture.GetTemporary(tileResolution.x, tileResolution.y, 0, RenderTextureFormat.R8);

            DepthSave pass = new DepthSave(depthCopyShader, bufferDepth);

            
            RenderPipelineManager.beginCameraRendering += (_, camera) =>
            {
                if (camera == _depthCam)
                    _depthCam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
            };

            _depthCam.targetTexture = buffer;
            _depthCam.Render();

            AsyncGPUReadback.Request(bufferDepth, 0, TextureFormat.R8, request => GetData(request, waterBody));
            
            if (buffer)
                RenderTexture.ReleaseTemporary(buffer);

            if (bufferDepth)
                RenderTexture.ReleaseTemporary(bufferDepth);

            if (_depthCam)
                Object.DestroyImmediate(_depthCam.gameObject);
        }
        
        private static void GetData(AsyncGPUReadbackRequest obj, WaterBody waterBody)
        {
            if (obj.hasError || waterBody == null)
            {
                Debug.LogError("Depth save failed.");
                return;
            }

            var data = obj.GetData<float>();

            var tex = new Texture2D(obj.width, obj.height, TextureFormat.R8, false);
            tex.SetPixelData(data, 0);
            tex.Apply();
            tex.name = $"{waterBody.name}_DepthTile";
            if(waterBody.TryGetModifier<Depth.DepthData>(out var depth))
                depth.DepthMap = tex;
        }

        static void SaveTile(byte[] data)
        {
            var activeScene = SceneManager.GetActiveScene();// DepthGenerator.Current.gameObject.scene; // TODO
            var sceneName = activeScene.name.Split('.')[0];
            var path = activeScene.path.Split('.')[0];
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var filename = "HELLO"; //$"{DepthGenerator.Current.gameObject.name}_DepthTile.png"; // TODO
            File.WriteAllBytes($"{path}/{filename}", data);
            
            AssetDatabase.Refresh();

            var import = (TextureImporter)AssetImporter.GetAtPath($"{path}/{filename}");
            var settings = new TextureImporterSettings();
            import.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.SingleChannel;
            settings.readable = true;
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            import.SetTextureSettings(settings);
            import.SaveAndReimport();

            //DepthGenerator.Current.depthTile = AssetDatabase.LoadAssetAtPath<Texture2D>($"{path}/{filename}");
        }

        static void CreateDepthCamera(Matrix4x4 matrix, float2 size, float2 range, LayerMask mask)
        {
            //Generate the camera
            if (_depthCam == null)
            {
                var go = new GameObject("depthCamera"); //create the cameraObject
                go.hideFlags = HideFlags.HideAndDontSave;
                _depthCam = go.AddComponent<Camera>();
            }
            
            _depthCam.enabled = false;
            _depthCam.orthographic = true;
            _depthCam.orthographicSize = size.x * 0.5f;

            var nearClip = 0.01f;
            _depthCam.nearClipPlane = nearClip;
            _depthCam.farClipPlane = range.x + range.y;
            
            _depthCam.allowHDR = false;
            _depthCam.allowMSAA = false;
            _depthCam.cameraType = CameraType.Game;
            _depthCam.cullingMask = mask;
            
            // tranform
            var t = _depthCam.transform;
            t.SetPositionAndRotation(matrix.MultiplyPoint(new Vector3(0, range.x, 0)), Quaternion.identity);
            t.up = Vector3.forward; //face the camera down

            // setup additional data
            var additionalCamData = _depthCam.GetUniversalAdditionalCameraData();
            additionalCamData.renderShadows = false;
            additionalCamData.requiresColorOption = CameraOverrideOption.Off;
            additionalCamData.requiresDepthOption = CameraOverrideOption.On;
        }
    }
}
#endif