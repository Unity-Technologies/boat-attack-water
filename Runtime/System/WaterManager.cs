using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Rendering;
using WaterSystem.Settings;

namespace WaterSystem
{
    [DefaultExecutionOrder(-50)]
    public class WaterManager : BaseSystem
    {
        public static List<WaterBody> WaterBodies = new List<WaterBody>();
        
        // temp data
        private static Mesh _unitQuad;
        
        // Render Passes
        private WaterBuffers _waterBufferPass;
        private WaterCaustics _waterCaustics;
        
        protected override void Init()
        {
            ChangeState(State.Setup);
            
            // if WaterProjectSettings instance is not null, run setup
            if (ProjectSettings.Instance != null) Setup();
        }

        /// <summary>
        /// Setup function to be called by OnEnable
        /// Waits for WaterProjectSettings to be initialized before continuing
        /// Collects all WaterBodies in the scene and adds them to the list
        /// Changes state to Ready
        /// </summary>
        /// <returns></returns>
        private void Setup()
        {
            // Validate WaterProjectSettings resources
            if (ProjectSettings.Instance._resources.ValidateResources() == false)
            {
                Debug.LogError("WaterProjectSettings resources are not valid, please check the resources and try again.");
                return;
            }
            
            // setup Rendering Debugger
            DebugTooling.Create();
            
            // collect all waterBodies in the scene and add them to the list
            WaterBodies.AddRange(FindObjectsByTypeSafe<WaterBody>());
            
            // setup render pipeline callback
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

            // setup resources
            Utilities.SetupMaterialProperties();
            if(_unitQuad == null)
                _unitQuad = Utilities.GenerateCausticsMesh(1f);
            
            ChangeState(State.Ready);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if(cam.cameraType == CameraType.Preview) return;
            
            //if(WaterBodies == null || WaterBodies.Count == 0) return;
            
            var urpCameraData = cam.GetUniversalAdditionalCameraData();
            
            // Planar Reflections (if enabled) TODO - figure out multi water body support, and also current version locked to 0 on y axis
            if (ProjectSettings.Quality.reflectionSettings.reflectionType == Data.ReflectionSettings.Type.PlanarReflection)
                PlanarReflections.Execute(context, cam);

            
            // loops through each water body in the list and call Render on each
            foreach (var waterBody in WaterBodies)
            {
                waterBody.Render(cam, urpCameraData.scriptableRenderer);
            }
            
            // Enqueue render passes
            _waterBufferPass ??= new WaterBuffers();
            urpCameraData.scriptableRenderer.EnqueuePass(_waterBufferPass);
            
            _waterCaustics ??= new WaterCaustics();
            urpCameraData.scriptableRenderer.EnqueuePass(_waterCaustics);
            
#if UNITY_2023_3_OR_NEWER || RENDER_GRAPH_ENABLED // RenderGraph
            // inject dummy pass later in the frame to keep resources alive
            urpCameraData.scriptableRenderer.EnqueuePass(new Utilities.DummyResourcePass(RenderPassEvent.AfterRenderingTransparents));
#endif
        }

        private void OnDisable()
        {
            ChangeState(State.Cleanup);
            Cleanup();
            ChangeState(State.None);
        }

        /// <summary>
        /// Cleanup function to be called by OnDisable
        /// </summary>
        private void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            
            WaterBodies?.Clear();
            
            // Cleanup Rendering Debugger
            DebugTooling.Dispose();
            
            // Destroy mesh
            if (_unitQuad != null)
            {
                CoreUtils.Destroy(_unitQuad);
                _unitQuad = null;
            }
            
            // Cleanup render passes
            _waterBufferPass = null;
            _waterCaustics.Cleanup();
            _waterCaustics = null;
        }
        
        //public static function to get the unitMesh
        public static Mesh GetUnitQuad()
        {
            return _unitQuad;
        }

        #region WaterBody Registration

        /// <summary>
        /// Register a waterBody with the WaterManager
        /// </summary>
        /// <param name="obj">The WaterBody to register</param>
        public static void Register(WaterBody obj)
        {
            // add waterBody to list and debug log a message
            if (!WaterBodies.Contains(obj)) WaterBodies.Add(obj);
            
            Debug.Log($"Registered {obj.name}<{obj.GetType()}> with WaterManager");
        }
        
        /// <summary>
        /// Unregister a waterBody from the WaterManager
        /// </summary>
        /// <param name="obj">The WaterBody to unregister</param>
        public static void Unregister(WaterBody obj)
        {
            // remove waterBody from list and debug log a message
            if (WaterBodies.Contains(obj)) WaterBodies.Remove(obj);

            Debug.Log($"Unregistered {obj.name}<{obj.GetType()}> with WaterManager");
        }
        
        // method to validata the list of water bodies and remove any null entries
        public static void ValidateWaterBodies()
        {
            // loop through each water body in the list
            for (var i = WaterBodies.Count - 1; i >= 0; i--)
            {
                // if the water body is null, remove it from the list
                if (WaterBodies[i] == null) WaterBodies.RemoveAt(i);
            }
        }
        
        #endregion

        protected override string DebugGUI()
        {
            var str = "";
            
            str += $"Project Settings: {ProjectSettings.Instance}\n";
            str += ProjectSettings.Instance._resources.ValidationString;
            DebugWaterBodies(ref str);
            
            return str;
        }
        
        private void DebugWaterBodies(ref string str)
        {
            str += $"\n<b>WaterBodies({WaterBodies.Count}):</b>";
            if (WaterBodies is not {Count: > 0}) return;
            
            foreach (var waterBody in WaterBodies)
            {
                str += $"\n- <b>{waterBody.name}({waterBody.GetInstanceID().ToString()})</b>(<i>{waterBody.GetType()}</i>)";
            }
        }

        private void OnGUI()
        {
            return;
            
            var str = "";
            // find all objects that inherit from BaseSystem and draw their debug string
            
            // loop through each instance of BaseSystem<> add their debug string to str
            foreach (var baseSystem in GetAllInstances())
            {
                str += baseSystem.GetDebugString() + "\n";
            }

            var content = new GUIContent(str);
            var style = GUI.skin.textArea;
            style.alignment = TextAnchor.UpperLeft;
            style.richText = true;
            style.fontSize = 18;
            // draw the debug string inside a grey box
            GUI.backgroundColor = new Color(0,0,0,0.75f);
            var rect = GUILayoutUtility.GetRect(content, GUI.skin.textArea);
            var quant = 100f;
            rect.position += Vector2.one * 10;
            rect.width = Mathf.CeilToInt(rect.width / quant) * quant;
            GUI.Box(rect, str, style);
        }
    }
}