using System;
using UnityEngine;
using Unity.Barracuda;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;


public class DetectorYolo2 : MonoBehaviour, Detector
{
    public NNModel modelFile;
    public TextAsset labelsFile;

    private const int IMAGE_MEAN = 0;
    private const float IMAGE_STD = 255.0F;

    // ONNX model input and output name. Modify when switching models.
    //These aren't const values because they need to be easily edited on the component before play mode

    public string INPUT_NAME;
    public string OUTPUT_NAME;

    //This has to stay a const
    private const int _image_size = 416;
    public int IMAGE_SIZE { get => _image_size; }


    // Minimum detection confidence to track a detection
    public float MINIMUM_CONFIDENCE;

    private IWorker worker;

    public const int ROW_COUNT = 13;
    public const int COL_COUNT = 13;
    public const int BOXES_PER_CELL = 5;
    public const int BOX_INFO_FEATURE_COUNT = 5;

    //Update this!
    public int CLASS_COUNT;

    public const float CELL_WIDTH = 32;
    public const float CELL_HEIGHT = 32;
    private string[] labels;

    private float[] anchors = new float[]
    {
        // 1.08F, 1.19F, 3.42F, 4.41F, 6.63F, 11.38F, 9.42F, 5.11F, 16.62F, 10.52F  // yolov2-tiny-voc
        //0.57273F, 0.677385F, 1.87446F, 2.06253F, 3.33843F, 5.47434F, 7.88282F, 3.52778F, 9.77052F, 9.16828F // yolov2-tiny
        0.57273F, 0.677385F, 1.87446F, 2.06253F, 3.33843F, 5.47434F, 7.88282F, 3.52778F, 9.77052F, 9.16828F  // yolov2-tiny-food
        //0.57273F, 0.677385F, 1.87446F, 2.06253F, 3.33843F, 5.47434F, 7.88282F, 3.52778F, 9.77052F, 9.16828F // yolov3
    };


    public void Start()
    {
        this.labels = Regex.Split(this.labelsFile.text, "\n|\r|\r\n")
            .Where(s => !String.IsNullOrEmpty(s)).ToArray();
        var model = ModelLoader.Load(this.modelFile);
        // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/Worker.html
        //These checks all check for GPU before CPU as GPU is preferred if the platform + rendering pipeline support it

#if UNITY_IOS //Only IOS
        Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
        {
            //IOS 11 needed for ARKit, IOS 11 has Metal support only, therefore GPU can run
            var workerType = WorkerFactory.Type.ComputePrecompiled; // GPU
            this.worker = WorkerFactory.CreateWorker(workerType, model);
        }
        else
        {
            //If Metal support is dropped for some reason, fall back to CPU
            var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
            this.worker = WorkerFactory.CreateWorker(workerType, model);
        }

#elif UNITY_ANDROID //Only Android
        Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Vulkan)
        {
            //Vulkan on Android supports GPU
            //However, ARCore does not currently support Vulkan, when it does, this line will work
            var workerType = WorkerFactory.Type.ComputePrecompiled; // GPU
            this.worker = WorkerFactory.CreateWorker(workerType, model);
        }
        else
        {
            //If not vulkan, fall back to CPU
            var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
            this.worker = WorkerFactory.CreateWorker(workerType, model);
        }

#elif UNITY_WEBGL //Only WebGL
        Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
     //WebGL only supports CPU
    var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
    this.worker = WorkerFactory.CreateWorker(workerType, model);

#else //Any other platform
        Debug.Log("Graphics API: " + SystemInfo.graphicsDeviceType);
    // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/SupportedPlatforms.html
    //var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
      var workerType = WorkerFactory.Type.ComputePrecompiled; // GPU
      this.worker = WorkerFactory.CreateWorker(workerType, model);
#endif
    }


    public IEnumerator Detect(Color32[] picture, System.Action<IList<BoundingBox>> callback)
    {
        using (var tensor = TransformInput(picture, IMAGE_SIZE, IMAGE_SIZE))
        {
            var inputs = new Dictionary<string, Tensor>();
            inputs.Add(INPUT_NAME, tensor);
            yield return StartCoroutine(worker.StartManualSchedule(inputs));
            //worker.Execute(inputs);
            var output = worker.PeekOutput(OUTPUT_NAME);
            Debug.Log("Output: " + output);
            var results = ParseOutputs(output, MINIMUM_CONFIDENCE);
            var boxes = FilterBoundingBoxes(results, 5, MINIMUM_CONFIDENCE);
            callback(boxes);
        }
    }


    public static Tensor TransformInput(Color32[] pic, int width, int height)
    {
        float[] floatValues = new float[width * height * 3];

        for (int i = 0; i < pic.Length; ++i)
        {
            var color = pic[i];

            floatValues[i * 3 + 0] = (color.r - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 1] = (color.g - IMAGE_MEAN) / IMAGE_STD;
            floatValues[i * 3 + 2] = (color.b - IMAGE_MEAN) / IMAGE_STD;
        }

        return new Tensor(1, height, width, 3, floatValues);
    }


    private IList<BoundingBox> ParseOutputs(Tensor yoloModelOutput, float threshold)
    {
        var boxes = new List<BoundingBox>();

        for (int cy = 0; cy < COL_COUNT; cy++)
        {
            for (int cx = 0; cx < ROW_COUNT; cx++)
            {
                for (int box = 0; box < BOXES_PER_CELL; box++)
                {
                    var channel = (box * (CLASS_COUNT + BOX_INFO_FEATURE_COUNT));
                    var bbd = ExtractBoundingBoxDimensions(yoloModelOutput, cx, cy, channel);
                    float confidence = GetConfidence(yoloModelOutput, cx, cy, channel);

                    if (confidence < threshold)
                    {
                        continue;
                    }

                    float[] predictedClasses = ExtractClasses(yoloModelOutput, cx, cy, channel);
                    var (topResultIndex, topResultScore) = GetTopResult(predictedClasses);
                    var topScore = topResultScore * confidence;
                    Debug.Log("DEBUG: results: " + topResultIndex.ToString());

                    if (topScore < threshold)
                    {
                        continue;
                    }

                    var mappedBoundingBox = MapBoundingBoxToCell(cx, cy, box, bbd);
                    boxes.Add(new BoundingBox
                    {
                        Dimensions = new BoundingBoxDimensions
                        {
                            X = (mappedBoundingBox.X - mappedBoundingBox.Width / 2),
                            Y = (mappedBoundingBox.Y - mappedBoundingBox.Height / 2),
                            Width = mappedBoundingBox.Width,
                            Height = mappedBoundingBox.Height,
                        },
                        Confidence = topScore,
                        Label = labels[topResultIndex],
                        Used = false
                    });
                }
            }
        }

        return boxes;
    }


    private float Sigmoid(float value)
    {
        var k = (float)Math.Exp(value);

        return k / (1.0f + k);
    }


    private float[] Softmax(float[] values)
    {
        var maxVal = values.Max();
        var exp = values.Select(v => Math.Exp(v - maxVal));
        var sumExp = exp.Sum();

        return exp.Select(v => (float)(v / sumExp)).ToArray();
    }


    private BoundingBoxDimensions ExtractBoundingBoxDimensions(Tensor modelOutput, int x, int y, int channel)
    {
        return new BoundingBoxDimensions
        {
            X = modelOutput[0, x, y, channel],
            Y = modelOutput[0, x, y, channel + 1],
            Width = modelOutput[0, x, y, channel + 2],
            Height = modelOutput[0, x, y, channel + 3]
        };
    }


    private float GetConfidence(Tensor modelOutput, int x, int y, int channel)
    {
        // Debug.Log("ModelOutput " + modelOutput);
        return Sigmoid(modelOutput[0, x, y, channel + 4]);
    }


    private CellDimensions MapBoundingBoxToCell(int x, int y, int box, BoundingBoxDimensions boxDimensions)
    {
        return new CellDimensions
        {
            X = ((float)y + Sigmoid(boxDimensions.X)) * CELL_WIDTH,
            Y = ((float)x + Sigmoid(boxDimensions.Y)) * CELL_HEIGHT,
            Width = (float)Math.Exp(boxDimensions.Width) * CELL_WIDTH * anchors[box * 2],
            Height = (float)Math.Exp(boxDimensions.Height) * CELL_HEIGHT * anchors[box * 2 + 1],
        };
    }


    public float[] ExtractClasses(Tensor modelOutput, int x, int y, int channel)
    {
        float[] predictedClasses = new float[CLASS_COUNT];
        int predictedClassOffset = channel + BOX_INFO_FEATURE_COUNT;

        for (int predictedClass = 0; predictedClass < CLASS_COUNT; predictedClass++)
        {
            predictedClasses[predictedClass] = modelOutput[0, x, y, predictedClass + predictedClassOffset];
        }

        return Softmax(predictedClasses);
    }


    private ValueTuple<int, float> GetTopResult(float[] predictedClasses)
    {
        return predictedClasses
            .Select((predictedClass, index) => (Index: index, Value: predictedClass))
            .OrderByDescending(result => result.Value)
            .First();
    }


    private float IntersectionOverUnion(Rect boundingBoxA, Rect boundingBoxB)
    {
        var areaA = boundingBoxA.width * boundingBoxA.height;

        if (areaA <= 0)
            return 0;

        var areaB = boundingBoxB.width * boundingBoxB.height;

        if (areaB <= 0)
            return 0;

        var minX = Math.Max(boundingBoxA.xMin, boundingBoxB.xMin);
        var minY = Math.Max(boundingBoxA.yMin, boundingBoxB.yMin);
        var maxX = Math.Min(boundingBoxA.xMax, boundingBoxB.xMax);
        var maxY = Math.Min(boundingBoxA.yMax, boundingBoxB.yMax);

        var intersectionArea = Math.Max(maxY - minY, 0) * Math.Max(maxX - minX, 0);

        return intersectionArea / (areaA + areaB - intersectionArea);
    }


    private IList<BoundingBox> FilterBoundingBoxes(IList<BoundingBox> boxes, int limit, float threshold)
    {
        var activeCount = boxes.Count;
        var isActiveBoxes = new bool[boxes.Count];

        for (int i = 0; i < isActiveBoxes.Length; i++)
        {
            isActiveBoxes[i] = true;
        }

        var sortedBoxes = boxes.Select((b, i) => new { Box = b, Index = i })
                .OrderByDescending(b => b.Box.Confidence)
                .ToList();

        var results = new List<BoundingBox>();

        for (int i = 0; i < boxes.Count; i++)
        {
            if (isActiveBoxes[i])
            {
                var boxA = sortedBoxes[i].Box;
                results.Add(boxA);

                if (results.Count >= limit)
                    break;

                for (var j = i + 1; j < boxes.Count; j++)
                {
                    if (isActiveBoxes[j])
                    {
                        var boxB = sortedBoxes[j].Box;

                        if (IntersectionOverUnion(boxA.Rect, boxB.Rect) > threshold)
                        {
                            isActiveBoxes[j] = false;
                            activeCount--;

                            if (activeCount <= 0)
                                break;
                        }
                    }
                }

                if (activeCount <= 0)
                    break;
            }
        }
        return results;
    }
}
