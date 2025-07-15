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
    [Range(0f, 1f)]
    public float brake;

    public KeyCode throttleKey;
    public KeyCode brakeKey;

    public AnimationCurve curve;

    [Range(-1f, 1f)] public float steerAngle;

    public bool isController;
    public bool showDebug = false;

    private Vector3 _downForce;
    private float _dragForce;

    private void Update()
    {
        steerAngle = Input.GetAxis("Horizontal");
        if (isController && Input.GetAxis("Vertical") >= 0f)
        {
            throttle = Input.GetAxis("Vertical");
            brake = 0f;
        }
        else if (isController && Input.GetAxis("Vertical") <= 0f)
        {
            brake = Input.GetAxis("Vertical");
            throttle = 0f;
        }
        else
        {
            throttle = Mathf.Lerp(throttle, Input.GetKey(throttleKey) ? 1f : 0f, curve.Evaluate(Time.fixedDeltaTime * 10f));
            throttle = Input.GetKeyUp(throttleKey) ? 0f : throttle;
            brake = Input.GetKey(brakeKey) ? 1f : 0f;
        }
    }

    private void FixedUpdate()
    {
        _downForce = AeroSys.GetDownforce(VehicleRB.linearVelocity);
    }

    public float GetBrakeInput()
    {
        return brake;
    }

    private void OnGUI()
    {
        if (!showDebug) return;
        
        GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 300));
        GUILayout.Label("=== Inputs ===");
        GUILayout.Label($"Throttle: {throttle}");
        GUILayout.Label($"Brake: {brake}");
        GUILayout.HorizontalSlider(steerAngle, -1f, 1f);
        GUILayout.EndArea();
        
    }
}