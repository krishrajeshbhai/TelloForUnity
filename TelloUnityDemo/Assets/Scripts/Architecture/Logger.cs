using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

namespace TelloUnityDemo.Architecture
{
    /// <summary>
    /// Records flight data and events for analysis.
    /// </summary>
    public class Logger : MonoBehaviour
    {
        public DroneStateManager stateManager;
        
        private string logFilePath;
        private bool isLogging = false;

        private void Start()
        {
            // Optional: Setup path on Quest
            string directory = Application.persistentDataPath + "/FlightLogs";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            logFilePath = directory + "/Log_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";
            
            // Write CSV Header
            File.WriteAllText(logFilePath, "Time,Connection,Battery,Altitude,Speed,IsFlying\n");

            if (stateManager != null)
            {
                stateManager.OnStateUpdated += LogState;
                isLogging = true;
                LogEvent("Logger Started");
            }
            else
            {
                Debug.LogWarning("[Logger] DroneStateManager not assigned.");
            }
        }

        private void OnDestroy()
        {
            if (stateManager != null)
            {
                stateManager.OnStateUpdated -= LogState;
            }
            LogEvent("Logger Stopped");
        }

        private void LogState(DroneState state)
        {
            if (!isLogging) return;

            string logLine = $"{DateTime.Now.ToString("HH:mm:ss.fff")},{state.ConnectionStatus},{state.BatteryPercentage},{state.Altitude},{state.Speed},{state.IsFlying}\n";
            File.AppendAllText(logFilePath, logLine);
        }

        public void LogEvent(string eventMessage)
        {
            if (!isLogging) return;
            string logLine = $"{DateTime.Now.ToString("HH:mm:ss.fff")},EVENT: {eventMessage}\n";
            File.AppendAllText(logFilePath, logLine);
            Debug.Log($"[Logger] {eventMessage}");
        }
    }
}
