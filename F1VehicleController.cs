using System;
using UnityEngine;


public class F1VehicleController : MonoBehaviour
{
    [Header("References")] public F1AeroSystem AeroSys;
    public F1BrakingSystem BrakingSys;
    public F1EngineSystem EngineSys;
    public F1SuspensionSystem SuspensionSys;
    public F1WheelSystem[] WheelSys;
    public Rigidbody VehicleRB;

    [Header("Inputs"), Range(0f, 1f)] public float throttle;
    public float brake;

    public KeyCode throttleKey;
    public KeyCode brakeKey;

    [Range(-1f, 1f)] public float steerAngle;

    public bool isController;

    private Vector3 _downForce;
    private float _dragForce;

    private void Update()
    {
        steerAngle = Input.GetAxis("Horizontal");
        if (isController && Input.GetAxis("Vertical") >= 0f)
        {
            throttle = Input.GetAxis("Vertical");
        }
        else if (isController && Input.GetAxis("Vertical") < 0f)
        {
            brake = Input.GetAxis("Vertical");
        }
        else
        {
            throttle = Input.GetKey(throttleKey) ? 1f : 0f;
            brake = Input.GetKey(brakeKey) ? 1f : 0f;
        }
    }

    private void FixedUpdate()
    {
        _downForce = AeroSys.GetDownforce(VehicleRB.linearVelocity);
    }

    private void OnGUI()
    {
    }
}