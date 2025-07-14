using System;
using UnityEngine;


public class F1VehicleController : MonoBehaviour
{
        [Header("References")]
        public F1AeroSystem AeroSys;
        public F1BrakingSystem BrakingSys;
        public F1EngineSystem EngineSys;
        public F1SuspensionSystem SuspensionSys;
        public F1WheelSystem[] WheelSys;
        public Rigidbody VehicleRB;
        
        [Header("Inputs"), Range(0f, 1f)]
        public float throttle;
        public float brake;
        
        [Range(-1f, 1f)]
        public float steerAngle;

        private void Update()
        {
                
        }

        private void FixedUpdate()
        {
                
        }
}