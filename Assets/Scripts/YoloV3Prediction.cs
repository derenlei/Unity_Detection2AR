using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class YoloV3Prediction : MonoBehaviour
{
        /// <summary>
        /// ((52 x 52) + (26 x 26) + 13 x 13)) x 3 = 10,647.
        /// </summary>
        private const int yoloV3BboxPredictionCount = 10_647;

        public float ImageWidth { get; set; }

        public float ImageHeight { get; set; }

        /// <summary>
        /// Bounding boxes raw prediction.
        /// </summary>
        public float[] BBoxes;

        /// <summary>
        /// Classes raw prediction.
        /// </summary>
        public float[] Classes;

        public IReadOnlyList<YoloV3Result> GetResults(string[] categories, float confThres = 0.5f, float iouThres = 0.5f)
        {
            if (BBoxes.Length != yoloV3BboxPredictionCount * 4)
            {
                throw new ArgumentException($"Bounding box prediction size is not correct. Expected {yoloV3BboxPredictionCount * 4}, got {BBoxes.Length}.", nameof(BBoxes));
            }

            if (Classes.Length != yoloV3BboxPredictionCount * categories.Length)
            {
                throw new ArgumentException($"Classes prediction size is not correct. Expected {yoloV3BboxPredictionCount * categories.Length}, got {Classes.Length}. You might want to check the {nameof(categories)}.", nameof(Classes));
            }

            // compute scale and pad factors
            float heightScale = 1f;
            float widthScale = 1f;
            float heightPad = 0f;
            float widthPad = 0f;
            if (ImageWidth < ImageHeight)
            {
                widthScale = ImageHeight / ImageWidth;
                widthPad = ImageWidth * (1 - widthScale) / 2f;
            }
            else if (ImageWidth > ImageHeight)
            {
                heightScale = ImageWidth / ImageHeight;
                heightPad = ImageHeight * (1 - heightScale) / 2f;
            }

            // process raw results
            List<float[]> results = new List<float[]>();
            for (int r = 0; r < yoloV3BboxPredictionCount; r++)
            {
                var scores = Classes.Skip(r * categories.Length).Take(categories.Length);

                // get the class' max confidence
                var conf = scores.Max();
                if (conf < confThres)
                {
                    continue; // if below conf threshold, skip it
                }

                var bboxAdj = Xywh2xyxy(BBoxes.Skip(r * 4).Take(4).ToArray());

                //[x1, y1, x2, y2, conf, c_0, c_1, ...]
                results.Add(bboxAdj.Concat(new[] { conf }).Concat(scores).ToArray());
            }

            // Non-maximum Suppression
            results = results.OrderByDescending(x => x[4]).ToList(); // sort by confidence
            List<YoloV3Result> resultsNms = new List<YoloV3Result>();

            int f = 0;
            while (f < results.Count)
            {
                var res = results[f];
                if (res == null)
                {
                    f++;
                    continue;
                }

                var conf = res[4];
                var classes_int = res.Skip(5).ToList().IndexOf(conf);
                string label = classes_int > -1 ? categories[classes_int] : "unknown";

                resultsNms.Add(new YoloV3Result(scaleCoords(res.Take(4).ToArray(), ImageHeight, ImageWidth, heightScale, widthScale, heightPad, widthPad), label, conf));
                results[f] = null;

                var iou = results.Select(bbox => bbox == null ? float.NaN : BoxIoU(res, bbox)).ToList();
                for (int i = 0; i < iou.Count; i++)
                {
                    if (float.IsNaN(iou[i])) continue;
                    if (iou[i] > iouThres)
                    {
                        results[i] = null;
                    }
                }
                f++;
            }

            return resultsNms;
        }

        /// <summary>
        /// Scale coordinates to page.
        /// </summary>
        /// <param name="bbox">[x1, y1, x2, y2]</param>
        private static float[] scaleCoords(float[] bbox, float imageHeight, float imageWidth, float heightScale, float widthScale, float heightPad, float widthPad)
        {
            float[] adjBbox = new float[4];
            adjBbox[0] = bbox[0] * imageWidth * widthScale + widthPad;
            adjBbox[1] = bbox[1] * imageHeight * heightScale + heightPad;
            adjBbox[2] = bbox[2] * imageWidth * widthScale + widthPad;
            adjBbox[3] = bbox[3] * imageHeight * heightScale + heightPad;
            return adjBbox;
        }

        /// <summary>
        /// Return intersection-over-union (Jaccard index) of boxes.
        /// <para>Both sets of boxes are expected to be in (x1, y1, x2, y2) format.</para>
        /// </summary>
        public static float BoxIoU(float[] boxes1, float[] boxes2)
        {
            static float box_area(float[] box)
            {
                return (box[2] - box[0]) * (box[3] - box[1]);
            }

            var area1 = box_area(boxes1);
            var area2 = box_area(boxes2);

            //Debug.Assert(area1 >= 0);
            //Debug.Assert(area2 >= 0);

            var dx = Math.Max(0, Math.Min(boxes1[2], boxes2[2]) - Math.Max(boxes1[0], boxes2[0]));
            var dy = Math.Max(0, Math.Min(boxes1[3], boxes2[3]) - Math.Max(boxes1[1], boxes2[1]));
            var inter = dx * dy;

            return inter / (area1 + area2 - inter);
        }

        /// <summary>
        /// Convert bounding box format from [x, y, w, h] to [x1, y1, x2, y2]
        /// <para>Box (center x, center y, width, height) to (x1, y1, x2, y2)</para>
        /// </summary>
        public static float[] Xywh2xyxy(float[] bbox)
        {
            var bboxAdj = new float[4];
            bboxAdj[0] = bbox[0] - bbox[2] / 2f;
            bboxAdj[1] = bbox[1] - bbox[3] / 2f;
            bboxAdj[2] = bbox[0] + bbox[2] / 2f;
            bboxAdj[3] = bbox[1] + bbox[3] / 2f;
            return bboxAdj;
        }
    }