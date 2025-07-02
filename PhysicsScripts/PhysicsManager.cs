using UnityEngine;
using System.Collections.Generic;

// Central physics coordinator
public class F1PhysicsManager : MonoBehaviour
{
    [Header("System References")]
    public F1EngineSystem engineSystem;
    public List<F1WheelSystem> wheelSystems = new List<F1WheelSystem>();
    public F1AerodynamicsSystem aeroSystem;
    
    [Header("Vehicle Properties")]
    public float mass = 740f; // kg minimum F1 weight
    public float wheelbase = 3.5f; // meters
    public float trackWidth = 2.0f; // meters
    public float centerOfGravityHeight = 0.4f; // meters
    public float frontAxlePosition = 1.7f; // meters from rear
    
    [Header("Load Distribution")]
    [Range(0.4f, 0.6f)]
    public float staticFrontLoadDistribution = 0.45f;
    
    private Rigidbody vehicleRb;
    private Vector3 velocity;
    private float speed;
    private float longitudinalAccel;
    private float lateralAccel;
    private float[] wheelLoads = new float[4];
    
    void Start()
    {
        vehicleRb = GetComponent<Rigidbody>();
        InitializeSystems();
    }
    
    void InitializeSystems()
    {
        // Set vehicle mass
        vehicleRb.mass = mass;
        
        // Calculate static wheel loads
        float staticFrontLoad = mass * 9.81f * staticFrontLoadDistribution;
        float staticRearLoad = mass * 9.81f * (1f - staticFrontLoadDistribution);
        
        // Initialize wheel systems
        for (int i = 0; i < wheelSystems.Count && i < 4; i++)
        {
            float baseLoad = i < 2 ? staticFrontLoad * 0.5f : staticRearLoad * 0.5f;
            wheelSystems[i].SetNormalForce(baseLoad);
        }
    }
    
    void FixedUpdate()
    {
        UpdateVehicleState();
        CalculateLoadTransfer();
        SynchronizeWheelAngularVelocities();
        UpdateSystemInputs();
    }
    
    void UpdateVehicleState()
    {
        velocity = vehicleRb.velocity;
        speed = velocity.magnitude;
        
        // Calculate accelerations
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);
        longitudinalAccel = Vector3.Dot(vehicleRb.acceleration, transform.forward);
        lateralAccel = Vector3.Dot(vehicleRb.acceleration, transform.right);
    }
    
    void CalculateLoadTransfer()
    {
        // Get downforce from aerodynamics
        float downforce = aeroSystem != null ? aeroSystem.GetTotalDownforce() : 0f;
        float totalVerticalLoad = (mass * 9.81f) + downforce;
        
        // Longitudinal load transfer
        float longitudinalTransfer = (longitudinalAccel * mass * centerOfGravityHeight) / wheelbase;
        float frontLoad = (totalVerticalLoad * staticFrontLoadDistribution) - longitudinalTransfer;
        float rearLoad = (totalVerticalLoad * (1f - staticFrontLoadDistribution)) + longitudinalTransfer;
        
        // Lateral load transfer
        float lateralTransferFront = (lateralAccel * frontLoad * centerOfGravityHeight) / trackWidth;
        float lateralTransferRear = (lateralAccel * rearLoad * centerOfGravityHeight) / trackWidth;
        
        // Calculate individual wheel loads
        wheelLoads[0] = (frontLoad * 0.5f) - lateralTransferFront; // Front left
        wheelLoads[1] = (frontLoad * 0.5f) + lateralTransferFront; // Front right
        wheelLoads[2] = (rearLoad * 0.5f) - lateralTransferRear;   // Rear left
        wheelLoads[3] = (rearLoad * 0.5f) + lateralTransferRear;   // Rear right
        
        // Apply loads to wheel systems
        for (int i = 0; i < wheelSystems.Count && i < 4; i++)
        {
            wheelSystems[i].SetNormalForce(Mathf.Max(0f, wheelLoads[i]));
        }
    }
    
    void SynchronizeWheelAngularVelocities()
    {
        if (engineSystem == null) return;
        
        // Get engine angular velocity
        float engineAngularVel = (engineSystem.GetCurrentRPM() * 2f * Mathf.PI) / 60f;
        
        // Calculate wheel angular velocities based on gear ratios
        int currentGear = engineSystem.GetCurrentGear();
        if (currentGear > 0 && !engineSystem.IsShifting())
        {
            float totalRatio = engineSystem.gearbox.gearRatios[currentGear - 1] * 
                              engineSystem.gearbox.finalDriveRatio * 
                              engineSystem.gearbox.primaryRatio;
            
            float wheelAngularVel = engineAngularVel / totalRatio;
            
            // Apply to driven wheels (assuming rear-wheel drive)
            if (wheelSystems.Count >= 4)
            {
                wheelSystems[2].SetAngularVelocity(wheelAngularVel); // Rear left
                wheelSystems[3].SetAngularVelocity(wheelAngularVel); // Rear right
                
                // Front wheels spin freely based on vehicle speed
                float frontWheelVel = speed / wheelSystems[0].physics.radius;
                wheelSystems[0].SetAngularVelocity(frontWheelVel);
                wheelSystems[1].SetAngularVelocity(frontWheelVel);
            }
        }
    }
    
    void UpdateSystemInputs()
    {
        // Update surface conditions across all systems
        float surfaceGrip = engineSystem.traction.surfaceGrip;
        float surfaceWetness = 1f - surfaceGrip; // Convert grip to wetness
        
        foreach (var wheel in wheelSystems)
        {
            wheel.SetSurfaceWetness(surfaceWetness);
        }
        
        // Update aerodynamics with current speed
        if (aeroSystem != null)
        {
            aeroSystem.UpdateAerodynamics(speed, GetRideHeight());
        }
    }
    
    float GetRideHeight()
    {
        // Calculate average ride height from suspension (simplified)
        return 0.06f; // 60mm default
    }
    
    // Public interface methods
    public float GetWheelLoad(int wheelIndex)
    {
        return wheelIndex < wheelLoads.Length ? wheelLoads[wheelIndex] : 0f;
    }
    
    public float GetTotalTractionForce()
    {
        float total = 0f;
        foreach (var wheel in wheelSystems)
        {
            total += wheel.GetTotalForce().magnitude;
        }
        return total;
    }
    
    public Vector3 GetVelocity() => velocity;
    public float GetSpeed() => speed;
    public float GetLongitudinalAccel() => longitudinalAccel;
    public float GetLateralAccel() => lateralAccel;
}

// Enhanced aerodynamics system
public class F1AerodynamicsSystem : MonoBehaviour
{
    [Header("Aerodynamic Properties")]
    [Range(-4f, -2f)]
    public float liftCoefficient = -3.2f;
    
    [Range(0.7f, 1.1f)]
    public float dragCoefficient = 0.9f;
    
    public float referenceArea = 1.6f; // m²
    public float airDensity = 1.225f; // kg/m³
    
    [Header("Ground Effect")]
    public float groundEffectCoefficient = 0.5f;
    public float minimumRideHeight = 0.01f; // 10mm
    
    [Header("DRS Settings")]
    public bool drsEnabled = false;
    public float drsLiftReduction = 0.4f; // 40% downforce reduction
    public float drsDragReduction = 0.15f; // 15% drag reduction
    
    private float currentDownforce = 0f;
    private float currentDrag = 0f;
    private Rigidbody vehicleRb;
    
    void Start()
    {
        vehicleRb = GetComponent<Rigidbody>();
    }
    
    public void UpdateAerodynamics(float speed, float rideHeight)
    {
        // Base aerodynamic forces
        float dynamicPressure = 0.5f * airDensity * speed * speed * referenceArea;
        
        // Ground effect multiplier
        float groundEffectMultiplier = 1f + (groundEffectCoefficient / (rideHeight + minimumRideHeight));
        
        // DRS effects
        float effectiveLiftCoeff = liftCoefficient;
        float effectiveDragCoeff = dragCoefficient;
        
        if (drsEnabled)
        {
            effectiveLiftCoeff *= (1f - drsLiftReduction);
            effectiveDragCoeff *= (1f - drsDragReduction);
        }
        
        // Calculate forces
        currentDownforce = -effectiveLiftCoeff * dynamicPressure * groundEffectMultiplier;
        currentDrag = effectiveDragCoeff * dynamicPressure;
        
        // Apply forces
        if (vehicleRb != null)
        {
            Vector3 downforceVector = -transform.up * currentDownforce;
            Vector3 dragVector = -transform.forward * currentDrag;
            
            vehicleRb.AddForce(downforceVector + dragVector, ForceMode.Force);
        }
    }
    
    public float GetTotalDownforce() => currentDownforce;
    public float GetTotalDrag() => currentDrag;
    
    public void SetDRS(bool enabled)
    {
        drsEnabled = enabled;
    }
}

// Enhanced engine system integration
public partial class F1EngineSystem
{
    // Add method to get detailed engine state
    public EngineState GetEngineState()
    {
        return new EngineState
        {
            rpm = currentRPM,
            torque = engineTorque,
            power = enginePower,
            totalPower = totalPower,
            fuelRemaining = fuel.currentFuel,
            ersEnergy = ers.currentEnergy,
            currentGear = gearbox.currentGear,
            isShifting = gearbox.isShifting,
            throttlePosition = throttleInput,
            ersDeployment = ers.deploymentInput
        };
    }
    
    // Synchronize with wheel spin data
    public void UpdateWheelSpinFeedback(float avgWheelSpinRatio)
    {
        // Adjust traction control based on actual wheel measurements
        if (traction.tractionControlEnabled && avgWheelSpinRatio > traction.slipThreshold)
        {
            float reduction = traction.tractionControlAggressiveness * avgWheelSpinRatio;
            engineTorque *= (1f - reduction);
        }
    }
}

// Data structures for system communication
[System.Serializable]
public struct EngineState
{
    public float rpm;
    public float torque;
    public float power;
    public float totalPower;
    public float fuelRemaining;
    public float ersEnergy;
    public int currentGear;
    public bool isShifting;
    public float throttlePosition;
    public float ersDeployment;
}

[System.Serializable]
public struct WheelState
{
    public float temperature;
    public float wear;
    public float gripLevel;
    public float slipAngle;
    public float slipRatio;
    public Vector3 force;
    public float normalLoad;
}

// Enhanced wheel system integration
public partial class F1WheelSystem
{
    public WheelState GetWheelState()
    {
        return new WheelState
        {
            temperature = temperature.currentTemperature,
            wear = wear.currentWear,
            gripLevel = effectiveGrip,
            slipAngle = slipAngle,
            slipRatio = slipRatio,
            force = totalTireForce,
            normalLoad = normalForce
        };
    }
    
    // Method to update from external physics calculations
    public void UpdateExternalForces(float externalNormalForce, float externalAngularVel)
    {
        SetNormalForce(externalNormalForce);
        SetAngularVelocity(externalAngularVel);
    }
}
