using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public static class WaterUtility
{
    public static bool CanRender(GameObject water, Camera camera)
    {
        if (camera.cameraType == CameraType.Preview)
            return false;

        if (camera.orthographic || camera.fieldOfView < 5)
            return false;

        if ((camera.cullingMask & (1 << water.layer)) == 0)
            return false;

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            return StageUtility.IsGameObjectRenderedByCamera(water, camera);
        }
#endif
        return true;
    }
}
