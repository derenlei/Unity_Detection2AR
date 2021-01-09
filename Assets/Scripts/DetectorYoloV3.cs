using System;
using UnityEngine;
using Unity.Barracuda;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace YoloV3
{
    public class DetectorYoloV3 : MonoBehaviour
{
    
    public NNModel modelFile;
    public TextAsset labelsFile;

    private const int IMAGE_MEAN = 0;
    private const float IMAGE_STD = 255.0F;

    // ONNX model input and output name. Modify when switching models.
    //These aren't const values because they need to be easily edited on the component before play mode

    //private const string INPUT_NAME = "yolov2-tiny/net1";
    //private const string OUTPUT_NAME = "yolov2-tiny/convolutional9/BiasAdd";
    //public string INPUT_NAME = "000_net";
    //public string OUTPUT_NAME = "023_convolutional";

    public string INPUT_NAME;
    public string OUTPUT_NAME_L;
    public string OUTPUT_NAME_M;

    //This has to stay a const
    public const int IMAGE_SIZE = 416;

    // Minimum detection confidence to track a detection
    public float MINIMUM_CONFIDENCE = 0.25f;

    private IWorker worker;

    // public const int ROW_COUNT_L = 13;
    // public const int COL_COUNT_L = 13;
    // public const int ROW_COUNT_M = 26;
    // public const int COL_COUNT_M = 26;
    public Dictionary<string, int> params_l = new Dictionary<string, int>(){{"ROW_COUNT", 13}, {"COL_COUNT", 13}, {"CELL_WIDTH", 32}, {"CELL_HEIGHT", 32}};
    public Dictionary<string, int> params_m = new Dictionary<string, int>(){{"ROW_COUNT", 26}, {"COL_COUNT", 26}, {"CELL_WIDTH", 16}, {"CELL_HEIGHT", 16}};
    public const int BOXES_PER_CELL = 3;
    public const int BOX_INFO_FEATURE_COUNT = 5;

    //Update this!
    public int CLASS_COUNT;

    // public const float CELL_WIDTH_L = 32;
    // public const float CELL_HEIGHT_L = 32;
    // public const float CELL_WIDTH_M = 16;
    // public const float CELL_HEIGHT_M = 16;
    private string[] labels;

    private float[] anchors = new float[]
    {
        // 1.08F, 1.19F, 3.42F, 4.41F, 6.63F, 11.38F, 9.42F, 5.11F, 16.62F, 10.52F  // yolov2-tiny-voc
        //0.57273F, 0.677385F, 1.87446F, 2.06253F, 3.33843F, 5.47434F, 7.88282F, 3.52778F, 9.77052F, 9.16828F // yolov2-tiny
        //0.57273F, 0.677385F, 1.87446F, 2.06253F, 3.33843F, 5.47434F, 7.88282F, 3.52778F, 9.77052F, 9.16828F  // yolov2-tiny-food
        //0.57273F, 0.677385F, 1.87446F, 2.06253F, 3.33843F, 5.47434F, 7.88282F, 3.52778F, 9.77052F, 9.16828F // yolov3
        10F, 14F,  23F, 27F,  37F, 58F,  81F, 82F,  135F, 169F,  344F, 319F // yolov3-tiny
    };


    public void Start()
    {
        this.labels = Regex.Split(this.labelsFile.text, "\n|\r|\r\n")
            .Where(s => !String.IsNullOrEmpty(s)).ToArray();
        var model = ModelLoader.Load(this.modelFile);
        // https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/Worker.html
        //var workerType = WorkerFactory.Type.ComputePrecompiled; // GPU
        var workerType = WorkerFactory.Type.CSharpBurst;  // CPU
        this.worker = WorkerFactory.CreateWorker(workerType, model);
    }

    


    public IEnumerator Detect(Color32[] picture, System.Action<IList<BoundingBox>> callback)
    {
        using (var tensor = TransformInput(picture, IMAGE_SIZE, IMAGE_SIZE))
        {
            var inputs = new Dictionary<string, Tensor>();
            inputs.Add(INPUT_NAME, tensor);
            yield return StartCoroutine(worker.StartManualSchedule(inputs));
            //worker.Execute(inputs);
            var output_l = worker.PeekOutput(OUTPUT_NAME_L);
            var output_m = worker.PeekOutput(OUTPUT_NAME_M);
            //Debug.Log("Output: " + output);
            var results_l = ParseOutputs(output_l, MINIMUM_CONFIDENCE, params_l);
            var results_m = ParseOutputs(output_m, MINIMUM_CONFIDENCE, params_m);
            var results = results_l.Concat(results_m).ToList();

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


    private IList<BoundingBox> ParseOutputs(Tensor yoloModelOutput, float threshold, Dictionary<string, int> parameters)
    {
        var boxes = new List<BoundingBox>();

        for (int cy = 0; cy < parameters["COL_COUNT"]; cy++)
        {
            for (int cx = 0; cx < parameters["ROW_COUNT"]; cx++)
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

                    var mappedBoundingBox = MapBoundingBoxToCell(cx, cy, box, bbd, parameters);
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
        //Debug.Log("ModelOutput " + modelOutput);
        return Sigmoid(modelOutput[0, x, y, channel + 4]);
    }


    private CellDimensions MapBoundingBoxToCell(int x, int y, int box, BoundingBoxDimensions boxDimensions, Dictionary<string, int> parameters)
    {
        return new CellDimensions
        {
            X = ((float)y + Sigmoid(boxDimensions.X)) * parameters["CELL_WIDTH"],
            Y = ((float)x + Sigmoid(boxDimensions.Y)) * parameters["CELL_HEIGHT"],
            Width = (float)Math.Exp(boxDimensions.Width) * anchors[6 + box * 2],
            Height = (float)Math.Exp(boxDimensions.Height) * anchors[6 + box * 2 + 1],
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


public class DimensionsBase
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Height { get; set; }
    public float Width { get; set; }
}


public class BoundingBoxDimensions : DimensionsBase { }

class CellDimensions : DimensionsBase { }


public class BoundingBox
{
    public BoundingBoxDimensions Dimensions { get; set; }

    public string Label { get; set; }

    public float Confidence { get; set; }

    // whether the bounding box already is used to raycast anchors
    public bool Used { get; set; }

    public Rect Rect
    {
        get { return new Rect(Dimensions.X, Dimensions.Y, Dimensions.Width, Dimensions.Height); }
    }

    public override string ToString()
    {
        return $"{Label}:{Confidence}, {Dimensions.X}:{Dimensions.Y} - {Dimensions.Width}:{Dimensions.Height}";
    }
}
}
