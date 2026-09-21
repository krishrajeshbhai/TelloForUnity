using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TelloUnityDemo.Architecture
{
    public enum ControlMode
    {
        Controller = 0,
        HandTracking = 1,
        FullBody = 2
    }

    /// <summary>
    /// Core logic for deciding what the drone should do based on pure VR Input.
    /// Connects VR Input -> Action Mapping -> Command Generation -> UDP Layer.
    /// </summary>
    public class DroneController : MonoBehaviour
    {
        [Header("References")]
        public VRInputManager inputManager;

        [Header("Settings")]
        public ControlMode currentMode = ControlMode.Controller;
        [Range(0f, 0.3f)] public float deadZone = 0.1f;
        
        // Cooldown for gestures to prevent spamming takeoff/land
        private float gestureCooldown = 0f;
        
        // State for gesture edges
        private bool wasLeftFist = false;
        private bool wasRightFist = false;

        // State for Full Body calibration
        private Vector3 bodyStartPosition;
        private bool bodyStartCaptured = false;

        private void Start()
        {
            // Connect to Tello SDK (UDP Layer)
            TelloLib.Tello.startConnecting();
        }

        private void OnDestroy()
        {
            TelloLib.Tello.stopConnecting();
        }

        private void Update()
        {
            if (gestureCooldown > 0) gestureCooldown -= Time.deltaTime;

            if (inputManager == null) return;
            
            VRInputState inputState = inputManager.GetInputState();
            
            // Check Emergency Keyboard Override (for safety during testing)
            if (Input.GetKeyDown(KeyCode.E))
            {
                EmergencyStop();
                return;
            }

            float lx = 0f, ly = 0f, rx = 0f, ry = 0f;

            switch (currentMode)
            {
                case ControlMode.Controller:
                    ProcessControllerInput(inputState, ref lx, ref ly, ref rx, ref ry);
                    break;
                case ControlMode.HandTracking:
                    ProcessHandTrackingInput(inputState, ref lx, ref ly, ref rx, ref ry);
                    break;
                case ControlMode.FullBody:
                    ProcessFullBodyInput(inputState, ref lx, ref ly, ref rx, ref ry);
                    break;
            }

            // Command Generator -> Sends validated axes to UDP Communication (TelloLib)
            TelloLib.Tello.controllerState.setAxis(lx, ly, rx, ry);
        }

        private void ProcessControllerInput(VRInputState state, ref float lx, ref float ly, ref float rx, ref float ry)
        {
            // Action Mapping: Buttons
            if (state.PrimaryButtonDown) TelloLib.Tello.takeOff();
            if (state.SecondaryButtonDown) TelloLib.Tello.land();
            if (state.MenuButtonDown) EmergencyStop();

            // Keyboard overrides
            if (Input.GetKeyDown(KeyCode.T)) TelloLib.Tello.takeOff();
            if (Input.GetKeyDown(KeyCode.L)) TelloLib.Tello.land();

            // Action Mapping: Sticks
            Vector2 leftStick = ApplyDeadZone(state.LeftStick);
            Vector2 rightStick = ApplyDeadZone(state.RightStick);

            lx = leftStick.x;   // Yaw
            ly = leftStick.y;   // Throttle
            rx = rightStick.x;  // Roll
            ry = rightStick.y;  // Pitch
        }

        private void ProcessHandTrackingInput(VRInputState state, ref float lx, ref float ly, ref float rx, ref float ry)
        {
            if (!state.HandsTracked)
            {
                // Safety: Stop drone if tracking lost
                return; 
            }

            // Action Mapping: Right Hand (Pitch/Roll)
            // Tilt right hand forward/back = Pitch
            ry = Mathf.Clamp(state.RightHandForward.z * 2f, -1f, 1f);
            // Tilt right hand left/right = Roll
            rx = Mathf.Clamp(state.RightHandForward.x * 2f, -1f, 1f);

            // Action Mapping: Left Hand (Throttle/Yaw)
            // Move left hand up/down = Throttle
            ly = Mathf.Clamp(state.LeftHandForward.y * 2f, -1f, 1f);
            // Tilt left hand left/right = Yaw
            lx = Mathf.Clamp(state.LeftHandForward.x * 2f, -1f, 1f);

            // Action Mapping: Gestures
            if (state.RightHandIsFist && !wasRightFist && gestureCooldown <= 0f)
            {
                Debug.Log("[DroneController] HAND: Takeoff gesture");
                TelloLib.Tello.takeOff();
                gestureCooldown = 2f;
            }

            if (state.LeftHandIsFist && !wasLeftFist && gestureCooldown <= 0f)
            {
                Debug.Log("[DroneController] HAND: Land gesture");
                TelloLib.Tello.land();
                gestureCooldown = 2f;
            }

            wasRightFist = state.RightHandIsFist;
            wasLeftFist = state.LeftHandIsFist;
        }

        private void ProcessFullBodyInput(VRInputState state, ref float lx, ref float ly, ref float rx, ref float ry)
        {
            if (!state.BodyTracked) return;

            if (!bodyStartCaptured)
            {
                bodyStartPosition = state.HipPosition;
                bodyStartCaptured = true;
                Debug.Log("[DroneController] BODY: Start position captured");
            }

            Vector3 delta = state.HipPosition - bodyStartPosition;

            // Lean forward/back = Pitch
            ry = Mathf.Clamp(delta.z * 3f, -1f, 1f);

            // Lean left/right = Roll
            rx = Mathf.Clamp(delta.x * 3f, -1f, 1f);

            // Squat = Throttle down, Jump = Throttle up
            ly = Mathf.Clamp(delta.y * 3f, -1f, 1f);

            // Action Mapping: Arms for takeoff/land
            if (state.HandsTracked && gestureCooldown <= 0f)
            {
                float leftHandHeight = state.LeftHandPosition.y;
                float rightHandHeight = state.RightHandPosition.y;
                float hipHeight = state.HipPosition.y;

                if (leftHandHeight > hipHeight + 0.3f && rightHandHeight > hipHeight + 0.3f)
                {
                    Debug.Log("[DroneController] BODY: Takeoff gesture");
                    TelloLib.Tello.takeOff();
                    gestureCooldown = 2f;
                }
                else if (leftHandHeight < hipHeight - 0.2f && rightHandHeight < hipHeight - 0.2f)
                {
                    Debug.Log("[DroneController] BODY: Land gesture");
                    TelloLib.Tello.land();
                    gestureCooldown = 2f;
                }
            }
        }

        private Vector2 ApplyDeadZone(Vector2 input)
        {
            if (input.magnitude < deadZone)
                return Vector2.zero;
            return input;
        }

        public void EmergencyStop()
        {
            Debug.Log("[DroneController] EMERGENCY STOP ACTIVATED");
            TelloLib.Tello.controllerState.setAxis(0, 0, 0, 0);
            TelloLib.Tello.land();
        }

        // Methods to change mode from UI/Unity integration layer
        public void SetMode(ControlMode mode)
        {
            currentMode = mode;
            bodyStartCaptured = false;
            Debug.Log($"[DroneController] Mode set to: {mode}");
        }
    }
}
