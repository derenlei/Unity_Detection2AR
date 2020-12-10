# Unity_Detection2AR
<strong>A simple solution to incorporate object localization into conventional computer vision object detection algorithms. </strong>

<strong>IDEA: </strong> There aren't that many open source real-time 3D object detection. This is an example of using "more popular" 2D object detection and then localize it with a few feature points. It uses recently released [Barracuda](https://docs.unity3d.com/Packages/com.unity.barracuda@1.0/manual/index.html) for object detection and ARFoundation for AR. It works both on IOS and Android devices.

![demo](Sample/demo.gif)

## Requirements
    "com.unity.barracuda": "1.0.3",
    "com.unity.xr.arfoundation": "4.0.8",
    "com.unity.xr.arkit": "4.0.8",
    "com.unity.xr.arcore": "4.0.8"

## Usage
It is developed in Unity 2019.4.9 and requires product ready Barracuda with updated AR packages. The preview Barracuda versions seems unstable and may not work.
* Open the project in Unity (Versions > 2019.4.9).
* In `Edit -> Player Settings -> Other XR Plug-in Management`, make sure Initialize XR on Startup and Plug-in providers are marked to enable ARCamera.
* Make sure that Detector has ONNX Model file and Labels file set.
* For Android, check the Minimum API Level at `Project Settings -> Player -> Others Settings -> Minimum API Level`. it requires at least Android 7.0 'Nougat' (API Level 24).
* For Android, also enable Auto Graphics API. [See Issue](https://github.com/derenlei/Unity_Detection2AR/issues/3#issuecomment-727286451)
* In `File -> Build settings` choose Detect and hit Build and run.
* For IOS, fix team setting in `Signing & Capabilities`.

## Current Model
The current uploaded model is Yolo version 2 (tiny) and trained on [FOOD100 dataset](http://foodcam.mobi/dataset100.html) through darknet. A good example of the training tool is [here](https://github.com/bennycheung/Food100_YOLO_Tools). Ideally, it can detect 100 categories of dishes.

![Image](Sample/predictions.jpg)

## Use your own Model
1. Convert your model into the ONNX format. If it is trained through Darknet, convert it into <strong>frozen</strong> tensorflow model first, then ONNX.
2. Upload the model and label to `Assets/Models`. Use inspector to check the model input and output names and modify the associated variables in [here](https://github.com/derenlei/Unity_Detection2AR/blob/development/Assets/Scripts/Detector.cs#L18-L19) and [here](https://github.com/derenlei/Unity_Detection2AR/blob/development/Assets/Scripts/Detector.cs#L33) in `Assets/Scripts/Detector.cs`.

## Acknowledgement
Partial code borrowed from [TFClassify-Unity-Barracuda](https://github.com/Syn-McJ/TFClassify-Unity-Barracuda) and [arfoundation-samples](https://github.com/Unity-Technologies/arfoundation-samples).
