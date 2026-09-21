using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Fallback if TMPro is missing, but assuming TMPro is available
using TMPro; // TextMeshPro

namespace TelloUnityDemo.Architecture
{
    /// <summary>
    /// Displays drone telemetry on a World-Space Canvas in VR.
    /// </summary>
    public class VRHUD : MonoBehaviour
    {
        public DroneStateManager stateManager;

        [Header("UI Elements")]
        public TextMeshProUGUI batteryText;
        public TextMeshProUGUI altitudeSpeedText;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI connectionText;
        
        [Header("Colors")]
        public Color healthyColor = Color.green;
        public Color warningColor = Color.yellow;
        public Color dangerColor = Color.red;

        private void OnEnable()
        {
            if (stateManager != null)
                stateManager.OnStateUpdated += UpdateHUD;
        }

        private void OnDisable()
        {
            if (stateManager != null)
                stateManager.OnStateUpdated -= UpdateHUD;
        }

        private void UpdateHUD(DroneState state)
        {
            // Battery
            if (batteryText != null)
            {
                batteryText.text = $"Battery: {state.BatteryPercentage}%";
                if (state.BatteryPercentage > 50) batteryText.color = healthyColor;
                else if (state.BatteryPercentage > 20) batteryText.color = warningColor;
                else batteryText.color = dangerColor;
            }

            // Altitude & Speed
            if (altitudeSpeedText != null)
            {
                altitudeSpeedText.text = $"Alt: {state.Altitude}cm  |  Spd: {state.Speed}";
            }

            // Drone Status
            if (statusText != null)
            {
                if (state.IsFlying)
                {
                    statusText.text = "Status: FLYING";
                    statusText.color = healthyColor;
                }
                else
                {
                    statusText.text = "Status: LANDED";
                    statusText.color = warningColor;
                }
            }

            // Connection
            if (connectionText != null)
            {
                connectionText.text = $"Link: {state.ConnectionStatus} ({state.WifiStrength}%)";
                if (state.ConnectionStatus == "Connected") connectionText.color = healthyColor;
                else connectionText.color = dangerColor;
            }
        }
    }
}
