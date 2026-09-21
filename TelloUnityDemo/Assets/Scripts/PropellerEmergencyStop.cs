using UnityEngine;
using TelloLib;

public class PropellerEmergencyStop : MonoBehaviour
{
    bool motorsStopped = false;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            StopAllPropellers();
        }
    }

    void StopAllPropellers()
    {
        Debug.Log("EMERGENCY MOTOR STOP");

        motorsStopped = true;

        // stop all movement commands
        Tello.controllerState.setAxis(0, 0, 0, 0);

        // emergency stop / forced landing
        Tello.land();
    }
}