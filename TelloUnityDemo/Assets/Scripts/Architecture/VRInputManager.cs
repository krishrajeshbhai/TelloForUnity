using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace TelloUnityDemo.Architecture
{
    /// <summary>
    /// Pure data structure representing the current state of all VR inputs.
    /// This keeps the DroneController decoupled from Unity XR specifics.
    /// </summary>
    public class VRInputState
    {
        // Controllers
        public Vector2 LeftStick;
        public Vector2 RightStick;
        public bool PrimaryButtonDown;
        public bool SecondaryButtonDown;
        public bool MenuButtonDown;

        // Hands
        public bool HandsTracked;
        public Vector3 LeftHandPosition;
        public Vector3 LeftHandForward;
        public bool LeftHandIsFist;
        
        public Vector3 RightHandPosition;
        public Vector3 RightHandForward;
        public bool RightHandIsFist;

        // Full Body
        public bool BodyTracked;
        public Vector3 HipPosition;
    }

    /// <summary>
    /// Captures input from Oculus Quest Platform (Controllers, Hand Tracking, Full Body).
    /// Acts as the VR Environment & Input Layer in the architecture.
    /// </summary>
    public class VRInputManager : MonoBehaviour
    {
        [Header("Hand Tracking")]
        public OVRHand leftHand;
        public OVRHand rightHand;
        public OVRSkeleton leftSkeleton;
        public OVRSkeleton rightSkeleton;

        [Header("Full Body Tracking")]
        public OVRBody bodyTracking;

        private InputDevice leftController;
        private InputDevice rightController;
        private bool controllersInitialized = false;

        private VRInputState currentState = new VRInputState();

        private void Update()
        {
            if (!controllersInitialized)
            {
                InitializeControllers();
            }

            UpdateControllerInput();
            UpdateHandTrackingInput();
            UpdateFullBodyInput();
        }

        public VRInputState GetInputState()
        {
            return currentState;
        }

        private void InitializeControllers()
        {
            var leftDevices = new List<InputDevice>();
            var rightDevices = new List<InputDevice>();

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
                leftDevices);

            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
                rightDevices);

            if (leftDevices.Count > 0 && rightDevices.Count > 0)
            {
                leftController = leftDevices[0];
                rightController = rightDevices[0];
                controllersInitialized = true;
                Debug.Log("[VRInputManager] VR Controllers initialized");
            }
        }

        private void UpdateControllerInput()
        {
            if (!controllersInitialized) return;

            // Sticks
            leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out currentState.LeftStick);
            rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out currentState.RightStick);

            // Buttons (Reading as 'down' this frame - using primary button state)
            // Note: Since we poll, we might want to capture the raw button state rather than 'down' event,
            // or let the DroneController handle debouncing. We'll pass the raw state.
            leftController.TryGetFeatureValue(CommonUsages.primaryButton, out currentState.PrimaryButtonDown);
            leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out currentState.SecondaryButtonDown);
            leftController.TryGetFeatureValue(CommonUsages.menuButton, out currentState.MenuButtonDown);
        }

        private void UpdateHandTrackingInput()
        {
            if (leftHand != null && rightHand != null)
            {
                currentState.HandsTracked = leftHand.IsTracked && rightHand.IsTracked;

                if (currentState.HandsTracked)
                {
                    currentState.LeftHandPosition = leftHand.transform.position;
                    currentState.LeftHandForward = leftHand.transform.forward;
                    currentState.LeftHandIsFist = IsHandFist(leftHand, leftSkeleton);

                    currentState.RightHandPosition = rightHand.transform.position;
                    currentState.RightHandForward = rightHand.transform.forward;
                    currentState.RightHandIsFist = IsHandFist(rightHand, rightSkeleton);
                }
            }
            else
            {
                currentState.HandsTracked = false;
            }
        }

        private void UpdateFullBodyInput()
        {
            if (bodyTracking != null)
            {
                currentState.BodyTracked = true; // Simplified check; depending on OVRBody we might check validity
                currentState.HipPosition = bodyTracking.transform.position;
            }
            else
            {
                currentState.BodyTracked = false;
            }
        }

        private bool IsHandFist(OVRHand hand, OVRSkeleton skeleton)
        {
            if (skeleton == null || !hand.IsTracked) return false;

            // Check if all fingers are curled (fist)
            return hand.GetFingerIsPinching(OVRHand.HandFinger.Index) &&
                   hand.GetFingerIsPinching(OVRHand.HandFinger.Middle) &&
                   hand.GetFingerIsPinching(OVRHand.HandFinger.Ring) &&
                   hand.GetFingerIsPinching(OVRHand.HandFinger.Pinky);
        }
    }
}
