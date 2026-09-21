using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TelloLib;

namespace TelloUnityDemo.Architecture
{
    /// <summary>
    /// Pure data representation of the drone's status.
    /// </summary>
    public class DroneState
    {
        public int BatteryPercentage;
        public float Altitude; // in cm or m depending on FlyData.height
        public float Speed; // scalar speed
        public bool IsFlying;
        public int WifiStrength;
        public string ConnectionStatus; // e.g., "Disconnected", "Connecting", "Connected"
    }

    /// <summary>
    /// Telemetry Parser, State Storage, and Real-time Updates.
    /// Acts as the central hub for drone telemetry data.
    /// </summary>
    public class DroneStateManager : MonoBehaviour
    {
        // Real-time Updates Event
        public delegate void StateUpdatedHandler(DroneState newState);
        public event StateUpdatedHandler OnStateUpdated;

        // State Storage
        private DroneState currentState = new DroneState();
        private bool isDirty = false;

        private void OnEnable()
        {
            Tello.onUpdate += HandleTelloUpdate;
            Tello.onConnection += HandleTelloConnection;
        }

        private void OnDisable()
        {
            Tello.onUpdate -= HandleTelloUpdate;
            Tello.onConnection -= HandleTelloConnection;
        }

        private void HandleTelloConnection(Tello.ConnectionState newState)
        {
            currentState.ConnectionStatus = newState.ToString();
            isDirty = true;
        }

        private void HandleTelloUpdate(int cmdId)
        {
            // Telemetry Parser
            // Tello.state is updated by TelloLib before onUpdate is called for cmdId 86 (FlyData).
            // We'll just read from Tello.state directly to populate our clean State object.
            
            if (Tello.state != null)
            {
                currentState.BatteryPercentage = Tello.state.batteryPercentage;
                currentState.Altitude = Tello.state.height;
                currentState.Speed = Tello.state.flySpeed;
                currentState.IsFlying = Tello.state.flying;
                currentState.WifiStrength = Tello.state.wifiStrength;
                isDirty = true;
            }
        }

        private void Update()
        {
            // Only fire events on the main thread (since TelloLib callbacks might be from a background thread)
            if (isDirty)
            {
                isDirty = false;
                OnStateUpdated?.Invoke(currentState);
            }
        }

        public DroneState GetCurrentState()
        {
            return currentState;
        }
    }
}
