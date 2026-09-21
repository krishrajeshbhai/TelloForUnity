using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TelloLib;
using UnityEngine.XR;
using Oculus.Interaction;
using Oculus.Interaction.Input;

public class TelloController : SingletonMonoBehaviour<TelloController>
{
    private static bool isLoaded = false;
    private TelloVideoTexture telloVideoTexture;

    // ─── CONTROL MODE ────────────────────────────────────────────
    // 0 = Controller, 1 = Hand Tracking, 2 = Full Body
    public int controlMode = 0;

    // ─── VR CONTROLLER ───────────────────────────────────────────
    private InputDevice leftController;
    private InputDevice rightController;
    private bool controllersInitialized = false;

    [Header("VR Settings")]
    [Range(0f, 0.3f)]
    public float deadZone = 0.1f;

    // ─── HAND TRACKING ───────────────────────────────────────────
    [Header("Hand Tracking")]
    public OVRHand leftHand;
    public OVRHand rightHand;
    public OVRSkeleton leftSkeleton;
    public OVRSkeleton rightSkeleton;

    // Hand gesture state
    private bool leftFistLast = false;
    private bool rightFistLast = false;
    private float handControlCooldown = 0f;

    // ─── FULL BODY ───────────────────────────────────────────────
    [Header("Full Body")]
    public OVRBody bodyTracking;
    private Vector3 bodyStartPosition;
    private bool bodyStartCaptured = false;

    // ─── FLIP TYPES ──────────────────────────────────────────────
    public enum FlipType
    {
        FlipFront = 0, FlipLeft = 1, FlipBack = 2, FlipRight = 3,
        FlipForwardLeft = 4, FlipBackLeft = 5, FlipBackRight = 6, FlipForwardRight = 7,
    }

    public enum VideoBitRate
    {
        VideoBitRateAuto = 0, VideoBitRate1M = 1, VideoBitRate15M = 2,
        VideoBitRate2M = 3, VideoBitRate3M = 4, VideoBitRate4M = 5,
    }

    // ─────────────────────────────────────────────────────────────
    override protected void Awake()
    {
        if (!isLoaded)
        {
            DontDestroyOnLoad(this.gameObject);
            isLoaded = true;
        }
        base.Awake();

        Tello.onConnection += Tello_onConnection;
        Tello.onUpdate += Tello_onUpdate;
        Tello.onVideoData += Tello_onVideoData;

        if (telloVideoTexture == null)
            telloVideoTexture = FindObjectOfType<TelloVideoTexture>();
    }

    private void OnEnable()
    {
        if (telloVideoTexture == null)
            telloVideoTexture = FindObjectOfType<TelloVideoTexture>();
    }

    private void Start()
    {
        if (telloVideoTexture == null)
            telloVideoTexture = FindObjectOfType<TelloVideoTexture>();

        Tello.startConnecting();
    }

    void OnApplicationQuit()
    {
        Tello.stopConnecting();
    }

    // ─────────────────────────────────────────────────────────────
    //  MAIN UPDATE
    // ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (!controllersInitialized)
            InitializeControllers();

        if (handControlCooldown > 0)
            handControlCooldown -= Time.deltaTime;

        // Always handle emergency from keyboard
        if (Input.GetKeyDown(KeyCode.E))
            EmergencyLanding();

        // ── MODE 0: VR CONTROLLER ─────────────────────────────────
        if (controlMode == 0)
        {
            float lx = 0f, ly = 0f, rx = 0f, ry = 0f;

            // Takeoff / Land
            if (Input.GetKeyDown(KeyCode.T)) Tello.takeOff();
            if (Input.GetKeyDown(KeyCode.L)) Tello.land();

            if (controllersInitialized)
            {
                // VR Buttons - Left Primary = Takeoff, Left Secondary = Land
                bool primaryBtn = false;
                bool secondaryBtn = false;
                bool menuBtn = false;

                leftController.TryGetFeatureValue(CommonUsages.primaryButton, out primaryBtn);
                leftController.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryBtn);
                leftController.TryGetFeatureValue(CommonUsages.menuButton, out menuBtn);

                if (primaryBtn) Tello.takeOff();
                if (secondaryBtn) Tello.land();
                if (menuBtn) EmergencyLanding();

                // Left Stick = Throttle + Yaw
                Vector2 leftStick = Vector2.zero;
                leftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftStick);

                // Right Stick = Pitch + Roll
                Vector2 rightStick = Vector2.zero;
                rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightStick);

                leftStick = ApplyDeadZone(leftStick);
                rightStick = ApplyDeadZone(rightStick);

                lx = leftStick.x;   // Yaw
                ly = leftStick.y;   // Throttle
                rx = rightStick.x;  // Roll
                ry = rightStick.y;  // Pitch
            }

            Tello.controllerState.setAxis(lx, ly, rx, ry);
        }

        // ── MODE 1: HAND TRACKING ─────────────────────────────────
        else if (controlMode == 1)
        {
            float lx = 0f, ly = 0f, rx = 0f, ry = 0f;

            if (leftHand != null && rightHand != null &&
                leftHand.IsTracked && rightHand.IsTracked)
            {
                // ── RIGHT HAND controls Pitch + Roll ──────────────
                // Get right hand position
                Vector3 rightPos = rightHand.transform.position;
                Vector3 rightForward = rightHand.transform.forward;

                // Tilt right hand forward/back = Pitch
                ry = Mathf.Clamp(rightForward.z * 2f, -1f, 1f);

                // Tilt right hand left/right = Roll
                rx = Mathf.Clamp(rightForward.x * 2f, -1f, 1f);

                // ── LEFT HAND controls Throttle + Yaw ────────────
                Vector3 leftPos = leftHand.transform.position;
                Vector3 leftForward = leftHand.transform.forward;

                // Move left hand up/down = Throttle
                ly = Mathf.Clamp(leftForward.y * 2f, -1f, 1f);

                // Tilt left hand left/right = Yaw
                lx = Mathf.Clamp(leftForward.x * 2f, -1f, 1f);

                // ── GESTURES ──────────────────────────────────────
                // Right Fist = Takeoff
                // Left Fist = Land
                bool rightFist = IsHandFist(rightHand, rightSkeleton);
                bool leftFist = IsHandFist(leftHand, leftSkeleton);

                if (rightFist && !rightFistLast && handControlCooldown <= 0f)
                {
                    Debug.Log("HAND: Takeoff gesture");
                    Tello.takeOff();
                    handControlCooldown = 2f; // prevent spam
                }

                if (leftFist && !leftFistLast && handControlCooldown <= 0f)
                {
                    Debug.Log("HAND: Land gesture");
                    Tello.land();
                    handControlCooldown = 2f;
                }

                rightFistLast = rightFist;
                leftFistLast = leftFist;
            }
            else
            {
                // Hands not tracked — stop drone
                Debug.LogWarning("Hands not tracked — drone stopped");
            }

            Tello.controllerState.setAxis(lx, ly, rx, ry);
        }

        // ── MODE 2: FULL BODY ─────────────────────────────────────
        else if (controlMode == 2)
        {
            float lx = 0f, ly = 0f, rx = 0f, ry = 0f;

            if (bodyTracking != null)
            {
                // Capture starting position once
                if (!bodyStartCaptured)
                {
                    bodyStartPosition = GetHipPosition();
                    bodyStartCaptured = true;
                    Debug.Log("Full Body: Start position captured");
                }

                Vector3 currentHip = GetHipPosition();
                Vector3 delta = currentHip - bodyStartPosition;

                // Lean forward/back = Pitch
                ry = Mathf.Clamp(delta.z * 3f, -1f, 1f);

                // Lean left/right = Roll
                rx = Mathf.Clamp(delta.x * 3f, -1f, 1f);

                // Raise both hands = Takeoff
                // Lower both hands = Land
                float leftHandHeight = leftHand != null ? leftHand.transform.position.y : 0f;
                float rightHandHeight = rightHand != null ? rightHand.transform.position.y : 0f;
                float hipHeight = currentHip.y;

                if (leftHandHeight > hipHeight + 0.3f &&
                    rightHandHeight > hipHeight + 0.3f &&
                    handControlCooldown <= 0f)
                {
                    Debug.Log("BODY: Takeoff gesture");
                    Tello.takeOff();
                    handControlCooldown = 2f;
                }

                if (leftHandHeight < hipHeight - 0.2f &&
                    rightHandHeight < hipHeight - 0.2f &&
                    handControlCooldown <= 0f)
                {
                    Debug.Log("BODY: Land gesture");
                    Tello.land();
                    handControlCooldown = 2f;
                }

                // Squat = Throttle down, Jump = Throttle up
                ly = Mathf.Clamp(delta.y * 3f, -1f, 1f);
            }

            Tello.controllerState.setAxis(lx, ly, rx, ry);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  HAND GESTURE DETECTION
    // ─────────────────────────────────────────────────────────────
    bool IsHandFist(OVRHand hand, OVRSkeleton skeleton)
    {
        if (skeleton == null) return false;
        if (!hand.IsTracked) return false;

        // Check if all fingers are curled (fist)
        return hand.GetFingerIsPinching(OVRHand.HandFinger.Index) &&
               hand.GetFingerIsPinching(OVRHand.HandFinger.Middle) &&
               hand.GetFingerIsPinching(OVRHand.HandFinger.Ring) &&
               hand.GetFingerIsPinching(OVRHand.HandFinger.Pinky);
    }

    // ─────────────────────────────────────────────────────────────
    //  FULL BODY HELPERS
    // ─────────────────────────────────────────────────────────────
    Vector3 GetHipPosition()
    {
        if (bodyTracking == null) return Vector3.zero;

        // Get hip bone from OVRBody
        if (bodyTracking == null)
            return Vector3.zero;


        return bodyTracking.transform.position;
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────
    void InitializeControllers()
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
            Debug.Log("VR Controllers initialized");
        }
    }

    Vector2 ApplyDeadZone(Vector2 input)
    {
        if (input.magnitude < deadZone)
            return Vector2.zero;
        return input;
    }

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC — Called from UI Buttons
    // ─────────────────────────────────────────────────────────────

    // Call these from your UI tab buttons
    public void SetModeController()
    {
        controlMode = 0;
        bodyStartCaptured = false;
        Debug.Log("Mode: VR Controller");
    }

    public void SetModeHandTracking()
    {
        controlMode = 1;
        bodyStartCaptured = false;
        Debug.Log("Mode: Hand Tracking");
    }

    public void SetModeFullBody()
    {
        controlMode = 2;
        bodyStartCaptured = false;
        Debug.Log("Mode: Full Body");
    }

    public void TakeOff() => Tello.takeOff();
    public void Land() => Tello.land();

    public void EmergencyLanding()
    {
        Debug.Log("EMERGENCY LANDING ACTIVATED");
        Tello.controllerState.setAxis(0, 0, 0, 0);
        Tello.land();
    }

    // ─────────────────────────────────────────────────────────────
    //  TELLO CALLBACKS
    // ─────────────────────────────────────────────────────────────
    private void Tello_onUpdate(int cmdId)
    {
        Debug.Log("Tello_onUpdate : " + Tello.state);
    }

    private void Tello_onConnection(Tello.ConnectionState newState)
    {
        if (newState == Tello.ConnectionState.Connected)
        {
            Tello.queryAttAngle();
            Tello.setMaxHeight(50);
            Tello.setPicVidMode(1);
            Tello.setVideoBitRate((int)VideoBitRate.VideoBitRateAuto);
            Tello.requestIframe();
        }
    }

    private void Tello_onVideoData(byte[] data)
    {
        if (telloVideoTexture != null)
            telloVideoTexture.PutVideoData(data);
    }
}