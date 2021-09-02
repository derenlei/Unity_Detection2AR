using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.XR;

namespace UnityEngine.XR.ARFoundation
{
    /// <summary>
    /// The ARPoseDriver component applies the current Pose value of an AR device to the transform of the GameObject.
    /// </summary>
    [HelpURL(HelpUrls.ApiWithNamespace + nameof(ARPoseDriver) + ".html")]
    public class ARPoseDriver : MonoBehaviour
    {
        internal struct NullablePose
        {
            internal Vector3? position;
            internal Quaternion? rotation;
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
#if UNITY_2020_1_OR_NEWER
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.TrackedDevice, devices);
            foreach (var device in devices)
            {
                if (device.characteristics.HasFlag(InputDeviceCharacteristics.TrackedDevice))
                {
                    CheckConnectedDevice(device, false);
                }
            }

            InputDevices.deviceConnected += OnInputDeviceConnected;
#endif // UNITY_UNITY_2020_1_OR_NEWER
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
#if UNITY_2020_1_OR_NEWER
            InputDevices.deviceConnected -= OnInputDeviceConnected;
#endif // UNITY_UNITY_2020_1_OR_NEWER
        }

        void Update() => PerformUpdate();

        void OnBeforeRender() => PerformUpdate();

        void PerformUpdate()
        {
            if (!enabled)
                return;

            var updatedPose = GetPoseData();

            if (updatedPose.position.HasValue)
                transform.localPosition = updatedPose.position.Value;
            if (updatedPose.rotation.HasValue)
                transform.localRotation = updatedPose.rotation.Value;
        }

#if UNITY_2020_1_OR_NEWER
        static internal InputDevice? s_InputTrackingDevice = null;

        void OnInputDeviceConnected(InputDevice device) => CheckConnectedDevice(device);

        void CheckConnectedDevice(InputDevice device, bool displayWarning = true)
        {
            var positionSuccess = false;
            var rotationSuccess = false;
            if (!(positionSuccess = device.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position)))
                positionSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraPosition, out position);
            if (!(rotationSuccess = device.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation)))
                rotationSuccess = device.TryGetFeatureValue(CommonUsages.colorCameraRotation, out rotation);

            if (positionSuccess && rotationSuccess)
            {
                if (s_InputTrackingDevice == null)
                {
                    s_InputTrackingDevice = device;
                }
                else
                {
                    Debug.LogWarning($"An input device {device.name} with the TrackedDevice characteristic was registered but the ARPoseDriver is already consuming data from {s_InputTrackingDevice.Value.name}.");
                }
            }
        }

#else
        static internal List<XR.XRNodeState> nodeStates = new List<XR.XRNodeState>();
#endif // UNITY_2020_1_OR_NEWER
        static internal NullablePose GetPoseData()
        {
            NullablePose resultPose = new NullablePose();

#if UNITY_2020_1_OR_NEWER
            if (s_InputTrackingDevice != null)
            {
                var pose = Pose.identity;
                var positionSuccess = false;
                var rotationSuccess = false;

                if (!(positionSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.centerEyePosition, out pose.position)))
                    positionSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.colorCameraPosition, out pose.position);
                if (!(rotationSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.centerEyeRotation, out pose.rotation)))
                    rotationSuccess = s_InputTrackingDevice.Value.TryGetFeatureValue(CommonUsages.colorCameraRotation, out pose.rotation);

                if (positionSuccess)
                    resultPose.position = pose.position;
                if (rotationSuccess)
                    resultPose.rotation = pose.rotation;

                if (positionSuccess || rotationSuccess)
                    return resultPose;
            }
#else
            XR.InputTracking.GetNodeStates(nodeStates);
            foreach (var nodeState in nodeStates)
            {
                if (nodeState.nodeType == XR.XRNode.CenterEye)
                {
                    var pose = Pose.identity;
                    var positionSuccess = nodeState.TryGetPosition(out pose.position);
                    var rotationSuccess = nodeState.TryGetRotation(out pose.rotation);

                    if (positionSuccess)
                        resultPose.position = pose.position;
                    if (rotationSuccess)
                        resultPose.rotation = pose.rotation;

                    return resultPose;
                }
            }
#endif // UNITY_2020_1_OR_NEWER
            return resultPose;
        }
    }
}
