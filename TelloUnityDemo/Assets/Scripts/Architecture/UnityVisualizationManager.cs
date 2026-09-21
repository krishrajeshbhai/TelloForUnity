using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TelloUnityDemo.Architecture
{
    /// <summary>
    /// Integration Layer: Ties pure logic modules to Unity's Scene, Canvas, and Overlay rendering.
    /// Fulfills the "Unity Game Engine (Optional)" block in the architecture.
    /// </summary>
    public class UnityVisualizationManager : MonoBehaviour
    {
        [Header("References")]
        public VRHUD hud;
        public Camera xrCamera;
        public Canvas hudCanvas;
        
        [Header("HUD Overlay Settings")]
        public float hudDistance = 1.0f;
        public float hudHeight = -0.2f;

        private void Start()
        {
            if (hudCanvas != null && hudCanvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogWarning("[UnityVisualizationManager] HUD Canvas should be set to World Space!");
            }
        }

        private void LateUpdate()
        {
            // Simple Overlay Management: Keep the HUD slightly below eye level and in front of the user
            if (hudCanvas != null && xrCamera != null)
            {
                // Note: In a real advanced VR setup, this might be tied to a hand controller or use a smooth damp script.
                // For this architecture, we do a basic head-locked or body-locked follow.
                
                // Example of simple smooth follow:
                Vector3 targetPos = xrCamera.transform.position + xrCamera.transform.forward * hudDistance;
                targetPos.y += hudHeight;
                
                hudCanvas.transform.position = Vector3.Lerp(hudCanvas.transform.position, targetPos, Time.deltaTime * 5f);
                
                // Look at camera
                hudCanvas.transform.LookAt(xrCamera.transform);
                hudCanvas.transform.Rotate(0, 180, 0); // UI looks away by default
            }
        }
    }
}
