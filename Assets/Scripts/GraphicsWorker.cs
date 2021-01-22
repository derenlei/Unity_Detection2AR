
using UnityEngine;
using System.Collections;
using Unity.Barracuda;

public class GraphicsWorker: MonoBehaviour
{

  public static IWorker GetWorker(Model model)
  {
    IWorker worker;
    #if UNITY_IOS //Only IOS
            Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                //IOS 11 needed for ARKit, IOS 11 has Metal support only, therefore GPU can run
                var workerType = WorkerFactory.Type.ComputePrecompiled; // GPU
                worker = WorkerFactory.CreateWorker(workerType, model);
            }
            else
            {
                //If Metal support is dropped for some reason, fall back to CPU
                var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
                worker = WorkerFactory.CreateWorker(workerType, model);
            }

    #elif UNITY_ANDROID //Only Android
            Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
            {
                //Vulkan on Android supports GPU
                //However, ARCore does not currently support Vulkan, when it does, this line will work
                var workerType = WorkerFactory.Type.ComputePrecompiled; // GPU
                worker = WorkerFactory.CreateWorker(workerType, model);
            }
            else
            {
                //If not vulkan, fall back to CPU
                var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
                worker = WorkerFactory.CreateWorker(workerType, model);
            }

    #elif UNITY_WEBGL //Only WebGL
            Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
         //WebGL only supports CPU
        var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
        worker = WorkerFactory.CreateWorker(workerType, model);

    #else //Any other platform
            Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
        // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/SupportedPlatforms.html
        //var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
          var workerType = WorkerFactory.Type.ComputePrecompiled; // GPU
          worker = WorkerFactory.CreateWorker(workerType, model);
    #endif

    return worker;
  }
}
