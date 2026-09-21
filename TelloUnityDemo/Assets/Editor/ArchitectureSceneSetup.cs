#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using TelloUnityDemo.Architecture;
using TMPro;
using UnityEngine.UI;

namespace TelloUnityDemo.Editor
{
    public class ArchitectureSceneSetup : EditorWindow
    {
        [MenuItem("Architecture/Setup 'implemented' Scene")]
        public static void SetupScene()
        {
            // Try to open the scene first
            string scenePath = "Assets/Scenes/implemented.unity";
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            
            if (!scene.IsValid())
            {
                Debug.LogError($"[SceneSetup] Could not open scene at {scenePath}");
                return;
            }

            Debug.Log("[SceneSetup] Starting automated scene setup...");

            // 1. Create Architecture Manager
            GameObject archManager = new GameObject("DroneArchitecture");
            
            VRInputManager inputManager = archManager.AddComponent<VRInputManager>();
            DroneController droneController = archManager.AddComponent<DroneController>();
            DroneStateManager stateManager = archManager.AddComponent<DroneStateManager>();
            TelloUnityDemo.Architecture.Logger logger = archManager.AddComponent<TelloUnityDemo.Architecture.Logger>();

            // Link Architecture Manager components
            droneController.inputManager = inputManager;
            logger.stateManager = stateManager;

            // 2. Setup HUD Canvas
            GameObject canvasObj = new GameObject("VRHUD_Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            // Set Canvas size and position
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(800, 600);
            canvasRect.localScale = new Vector3(0.002f, 0.002f, 0.002f);
            canvasRect.position = new Vector3(0, 1.5f, 2f);

            VRHUD vrHud = canvasObj.AddComponent<VRHUD>();
            vrHud.stateManager = stateManager;

            UnityVisualizationManager visManager = canvasObj.AddComponent<UnityVisualizationManager>();
            visManager.hud = vrHud;
            visManager.hudCanvas = canvas;
            
            // Try to find Camera
            if (Camera.main != null)
            {
                visManager.xrCamera = Camera.main;
            }

            // Create Text Elements
            vrHud.batteryText = CreateTextElement("BatteryText", canvasObj.transform, new Vector2(0, 200));
            vrHud.altitudeSpeedText = CreateTextElement("AltSpeedText", canvasObj.transform, new Vector2(0, 100));
            vrHud.statusText = CreateTextElement("StatusText", canvasObj.transform, new Vector2(0, 0));
            vrHud.connectionText = CreateTextElement("ConnectionText", canvasObj.transform, new Vector2(0, -100));

            // Set initial text
            vrHud.batteryText.text = "Battery: --%";
            vrHud.altitudeSpeedText.text = "Alt: --cm | Spd: --";
            vrHud.statusText.text = "Status: WAITING";
            vrHud.connectionText.text = "Link: DISCONNECTED";

            // 3. Auto-Configure OVR References & Passthrough
            OVRHand[] hands = Object.FindObjectsOfType<OVRHand>();
            foreach (var hand in hands)
            {
                if (hand.gameObject.name.Contains("Left") || hand.gameObject.name.Contains("left"))
                {
                    inputManager.leftHand = hand;
                    inputManager.leftSkeleton = hand.GetComponent<OVRSkeleton>();
                }
                else if (hand.gameObject.name.Contains("Right") || hand.gameObject.name.Contains("right"))
                {
                    inputManager.rightHand = hand;
                    inputManager.rightSkeleton = hand.GetComponent<OVRSkeleton>();
                }
            }

            OVRBody body = Object.FindObjectOfType<OVRBody>();
            if (body != null)
            {
                inputManager.bodyTracking = body;
            }
            
            OVRManager ovrManager = Object.FindObjectOfType<OVRManager>();
            if (ovrManager != null)
            {
                ovrManager.isInsightPassthroughEnabled = true;
                
                OVRPassthroughLayer layer = ovrManager.GetComponent<OVRPassthroughLayer>();
                if (layer == null)
                {
                    layer = ovrManager.gameObject.AddComponent<OVRPassthroughLayer>();
                }
                layer.projectionSurfaceType = OVRPassthroughLayer.ProjectionSurfaceType.Reconstructed;
                layer.overlayType = OVROverlay.OverlayType.Underlay;
            }

            // Mark scene as dirty and SAVE IT so changes persist
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            
            Debug.Log("[SceneSetup] Scene setup completed! All OVR references and Passthrough settings have been auto-configured.");
        }

        private static TextMeshProUGUI CreateTextElement(string name, Transform parent, Vector2 anchoredPosition)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 72;
            tmp.color = Color.white;
            
            RectTransform rect = textObj.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(800, 100);
            
            return tmp;
        }
    }
}
#endif
