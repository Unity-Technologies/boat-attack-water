#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem
{
    #region ScriptableRenderPass

    class DepthSave : ScriptableRenderPass
    {
        private Shader _shader;
        
        private RenderTexture depthTarget;
        private readonly Material _mat;
        private readonly ProfilingSampler _profiler = new("DepthSave");

        public DepthSave(Shader shader, RenderTexture depthTarget)
        {
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
            using (new ProfilingScope(cmd, _profiler)) // makes sure we have profiling ability
            {
                cmd.Blit(renderingData.cameraData.targetTexture, depthTarget, _mat, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    #endregion
    
    public static class DepthBaking
    {
        public static void CaptureDepth(int tileResolution, int size, Transform objTransform)
        {
            var depthCopyShader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.verasl.water-system/Runtime/Shaders/Utility/SceneDepth.shadergraph");

            if (depthCopyShader == null)
            {
                Debug.LogError("Failed to load SceneDepth shader for baking.");
                return;
            }

            CreateDepthCamera(out var depthCam, objTransform.position, size);

            if (depthCopyShader == null) return;
            var buffer = RenderTexture.GetTemporary(tileResolution, tileResolution, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var bufferDepth = RenderTexture.GetTemporary(tileResolution, tileResolution, 0, RenderTextureFormat.R8);

            DepthSave pass = new DepthSave(depthCopyShader, bufferDepth);

            
            RenderPipelineManager.beginCameraRendering += (context, camera) =>
            {
                if (camera == depthCam)
                    depthCam.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(pass);
            };

            depthCam.targetTexture = buffer;
            depthCam.Render();

            AsyncGPUReadback.Request(bufferDepth, 0, TextureFormat.R8, GetData);
            
            if (buffer)
                RenderTexture.ReleaseTemporary(buffer);

            if (bufferDepth)
                RenderTexture.ReleaseTemporary(bufferDepth);

            if (depthCam)
                Object.DestroyImmediate(depthCam.gameObject);
        }
        
        private static void GetData(AsyncGPUReadbackRequest obj)
        {
            if (obj.hasError)
            {
                Debug.LogError("Depth save failed.");
                return;
            }

            var data = obj.GetData<float>();

            var tex = new Texture2D(obj.width, obj.height, TextureFormat.R8, true);
            tex.SetPixelData(data, 0);
            tex.Apply();
            SaveTile(tex.EncodeToPNG());
            
            Object.DestroyImmediate(tex);
        }

        static void SaveTile(byte[] data)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            var sceneName = activeScene.name.Split('.')[0];
            var path = activeScene.path.Split('.')[0];
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            var filename = $"{DepthGenerator.Current.gameObject.name}_DepthTile.png";
            File.WriteAllBytes($"{path}/{filename}", data);
            
            AssetDatabase.Refresh();

            var import = (TextureImporter)AssetImporter.GetAtPath($"{path}/{filename}");
            var settings = new TextureImporterSettings();
            import.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.SingleChannel;
            settings.singleChannelComponent = TextureImporterSingleChannelComponent.Red;
            import.SetTextureSettings(settings);
            import.SaveAndReimport();

            DepthGenerator.Current.depthTile = AssetDatabase.LoadAssetAtPath<Texture2D>($"{path}/{filename}");
        }

        static void CreateDepthCamera(out Camera camera, Vector3 position, float size)
        {
            //Generate the camera
            var go = new GameObject("depthCamera") { hideFlags = HideFlags.HideAndDontSave }; //create the cameraObject
            Camera cam = go.AddComponent<Camera>();

            // setup camera props
            cam.enabled = false;
            cam.orthographic = true;
            //cam.depthTextureMode = DepthTextureMode.Depth;
            cam.orthographicSize = size * 0.5f; //hardcoded = 1k area - TODO
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20 + 1; // settingsData._waterMaxVisibility + depthExtra;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.cameraType = CameraType.Game;

            // tranform
            var t = cam.transform;
            var depthExtra = 4.0f;
            t.position = position + Vector3.up * depthExtra;
            t.up = Vector3.forward; //face the camera down

            // setup additional data
            var additionalCamData = cam.GetUniversalAdditionalCameraData();
            additionalCamData.renderShadows = false;
            additionalCamData.requiresColorOption = CameraOverrideOption.Off;
            additionalCamData.requiresDepthOption = CameraOverrideOption.On;
            camera = cam;
        }
    }
}
#endif