using UnityEngine;

[System.Serializable]
public class ICESettings
{
    [Header("Engine Specifications")]
    public float maxRPM = 15000f;
    public float idleRPM = 1500f;
    public float redlineRPM = 14000f;
    
    [Header("Torque Curve Coefficients")]
    public float torqueA = -2e-10f;
    public float torqueB = 4e-6f;
    public float torqueC = -0.02f;
    public float torqueD = 400f;
    
    [Header("Engine Properties")]
    public float displacement = 1.6f; // Liters
    public float compressionRatio = 13.0f;
    public float efficiency = 0.35f; // 35% thermal efficiency
}

[System.Serializable]
public class ERSSettings
{
    [Header("MGU-K Properties")]
    public float maxDeployPower = 120000f; // 120kW
    public float maxRecoveryPower = 120000f;
    public float rotationalInertia = 0.15f; // kg⋅m²
    public float efficiency = 0.8f;
    
    [Header("Energy Limits")]
    public float maxEnergyPerLap = 4000000f; // 4MJ deployment
    public float maxRecoveryPerLap = 2000000f; // 2MJ recovery
    public float currentEnergy = 2000000f; // Current stored energy
    
    [Header("Deployment")]
    [Range(0f, 1f)]
    public float deploymentInput = 0f;
    public bool autoHarvest = true;
}

[System.Serializable]
public class FuelSettings
{
    [Header("Fuel System")]
    public float maxFuelCapacity = 110f; // kg
    public float currentFuel = 110f;
    public float baseFuelConsumption = 32f; // kg/hour at full load
    
    [Header("Fuel Flow")]
    public float maxFuelFlow = 100f; // kg/hour (regulated)
    public float currentFuelFlow = 0f;
}

[System.Serializable]
public class GearboxSettings
{
    [Header("Gear Ratios")]
    public float[] gearRatios = {3.2f, 2.4f, 1.9f, 1.5f, 1.3f, 1.1f, 0.95f, 0.8f};
    public float finalDriveRatio = 3.0f;
    public float primaryRatio = 1.8f;
    
    [Header("Gear Control")]
    public int currentGear = 1;
    public float gearShiftTime = 0.05f; // 50ms shift time
    public bool isShifting = false;
    
    private float shiftTimer = 0f;
}

public class F1EngineSystem : MonoBehaviour
{
    [Header("Engine Components")]
    public ICESettings ice;
    public ERSSettings ers;
    public FuelSettings fuel;
    public GearboxSettings gearbox;
    
    [Header("Input")]
    [Range(0f, 1f)]
    public float throttleInput = 0f;
    
    [Header("Output")]
    public float currentRPM = 1500f;
    public float engineTorque = 0f;
    public float enginePower = 0f;
    public float totalPower = 0f;
    public float wheelTorque = 0f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    private Rigidbody vehicleRigidbody;
    private float targetRPM;
    private float ersTorque = 0f;
    private float wheelSpeed = 0f;
    
    void Start()
    {
        vehicleRigidbody = GetComponent<Rigidbody>();
        if (vehicleRigidbody == null)
        {
            Debug.LogError("F1EngineSystem: No Rigidbody found!");
            enabled = false;
        }
    }
    
    void FixedUpdate()
    {
        UpdateEngineRPM();
        CalculateICEPower();
        UpdateERSSystem();
        UpdateFuelConsumption();
        UpdateGearbox();
        ApplyEngineForces();
    }
    
    void UpdateEngineRPM()
    {
        // Calculate wheel speed and engine RPM relationship
        wheelSpeed = vehicleRigidbody.velocity.magnitude;
        float wheelRPM = (wheelSpeed * 60f) / (2f * Mathf.PI * 0.33f); // 0.33m wheel radius
        
        if (gearbox.currentGear > 0 && !gearbox.isShifting)
        {
            float totalRatio = gearbox.gearRatios[gearbox.currentGear - 1] * 
                              gearbox.finalDriveRatio * gearbox.primaryRatio;
            targetRPM = wheelRPM * totalRatio;
        }
        else
        {
            targetRPM = ice.idleRPM;
        }
        
        // Apply throttle influence on RPM
        targetRPM = Mathf.Lerp(ice.idleRPM, targetRPM + (throttleInput * 2000f), throttleInput);
        targetRPM = Mathf.Clamp(targetRPM, ice.idleRPM, ice.maxRPM);
        
        // Smooth RPM changes
        currentRPM = Mathf.Lerp(currentRPM, targetRPM, Time.fixedDeltaTime * 10f);
    }
    
    void CalculateICEPower()
    {
        // Torque curve: torque = a*rpm³ + b*rpm² + c*rpm + d
        float rpm = currentRPM;
        engineTorque = ice.torqueA * rpm * rpm * rpm + 
                      ice.torqueB * rpm * rpm + 
                      ice.torqueC * rpm + 
                      ice.torqueD;
        
        // Apply throttle position
        engineTorque *= throttleInput;
        
        // Limit by fuel flow if at high RPM
        if (currentRPM > ice.redlineRPM * 0.9f)
        {
            float fuelFlowLimit = fuel.currentFuelFlow / fuel.maxFuelFlow;
            engineTorque *= fuelFlowLimit;
        }
        
        // Calculate power: P = torque * rpm / 9549
        enginePower = (engineTorque * currentRPM) / 9549f;
        enginePower = Mathf.Max(0f, enginePower);
    }
    
    void UpdateERSSystem()
    {
        float angularVelocity = (currentRPM * 2f * Mathf.PI) / 60f;
        
        // ERS Deployment
        if (ers.deploymentInput > 0f && ers.currentEnergy > 0f)
        {
            float requestedPower = ers.deploymentInput * ers.maxDeployPower;
            float availablePower = ers.currentEnergy / Time.fixedDeltaTime;
            float deployPower = Mathf.Min(requestedPower, availablePower, ers.maxDeployPower);
            
            ersTorque = deployPower / angularVelocity;
            ers.currentEnergy -= deployPower * Time.fixedDeltaTime;
        }
        else
        {
            ersTorque = 0f;
        }
        
        // ERS Recovery (MGU-K during braking/coasting)
        if (ers.autoHarvest && throttleInput < 0.3f && currentRPM > 3000f)
        {
            // Simplified recovery calculation
            float recoveryPower = Mathf.Min(ers.maxRecoveryPower, ers.efficiency * enginePower * 0.1f);
            float energyRecovered = recoveryPower * Time.fixedDeltaTime;
            
            ers.currentEnergy = Mathf.Min(ers.currentEnergy + energyRecovered, ers.maxEnergyPerLap);
        }
        
        // Clamp energy
        ers.currentEnergy = Mathf.Clamp(ers.currentEnergy, 0f, ers.maxEnergyPerLap);
        
        // Total power calculation
        float ersPower = ersTorque * angularVelocity;
        totalPower = enginePower + ersPower;
    }
    
    void UpdateFuelConsumption()
    {
        // Fuel flow calculation
        float engineLoad = throttleInput * (currentRPM / ice.maxRPM);
        fuel.currentFuelFlow = fuel.baseFuelConsumption * engineLoad;
        fuel.currentFuelFlow = Mathf.Min(fuel.currentFuelFlow, fuel.maxFuelFlow);
        
        // Consume fuel
        float fuelConsumed = (fuel.currentFuelFlow / 3600f) * Time.fixedDeltaTime; // Convert to kg/s
        fuel.currentFuel = Mathf.Max(0f, fuel.currentFuel - fuelConsumed);
        
        // Reduce power if out of fuel
        if (fuel.currentFuel <= 0f)
        {
            engineTorque = 0f;
            enginePower = 0f;
        }
    }
    
    void UpdateGearbox()
    {
        // Handle gear shifting
        if (gearbox.isShifting)
        {
            gearbox.shiftTimer += Time.fixedDeltaTime;
            if (gearbox.shiftTimer >= gearbox.gearShiftTime)
            {
                gearbox.isShifting = false;
                gearbox.shiftTimer = 0f;
            }
            return;
        }
        
        // Calculate wheel torque
        if (gearbox.currentGear > 0)
        {
            float totalRatio = gearbox.gearRatios[gearbox.currentGear - 1] * 
                              gearbox.finalDriveRatio * gearbox.primaryRatio;
            wheelTorque = (engineTorque + ersTorque) * totalRatio;
        }
        else
        {
            wheelTorque = 0f;
        }
    }
    
    void ApplyEngineForces()
    {
        if (gearbox.isShifting) return;
        
        // Apply driving force to vehicle
        float wheelRadius = 0.33f;
        float drivingForce = wheelTorque / wheelRadius;
        
        // Apply force in forward direction
        Vector3 forceDirection = transform.forward;
        vehicleRigidbody.AddForce(forceDirection * drivingForce, ForceMode.Force);
    }
    
    // Public control methods
    public void SetThrottle(float input)
    {
        throttleInput = Mathf.Clamp01(input);
    }
    
    public void SetERSDeployment(float input)
    {
        ers.deploymentInput = Mathf.Clamp01(input);
    }
    
    public void ShiftUp()
    {
        if (!gearbox.isShifting && gearbox.currentGear < gearbox.gearRatios.Length)
        {
            gearbox.currentGear++;
            gearbox.isShifting = true;
        }
    }
    
    public void ShiftDown()
    {
        if (!gearbox.isShifting && gearbox.currentGear > 1)
        {
            gearbox.currentGear--;
            gearbox.isShifting = true;
        }
    }
    
    // Getters
    public float GetCurrentRPM() => currentRPM;
    public float GetTotalPower() => totalPower;
    public float GetFuelRemaining() => fuel.currentFuel;
    public float GetERSEnergy() => ers.currentEnergy;
    public int GetCurrentGear() => gearbox.currentGear;
    public bool IsShifting() => gearbox.isShifting;
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(Screen.width - 250, 10, 240, 300));
        GUILayout.Label("=== F1 ENGINE TELEMETRY ===");
        GUILayout.Label($"RPM: {currentRPM:F0} / {ice.maxRPM:F0}");
        GUILayout.Label($"Gear: {gearbox.currentGear}{(gearbox.isShifting ? " (Shifting)" : "")}");
        GUILayout.Label($"Throttle: {throttleInput * 100:F0}%");
        GUILayout.Label($"ICE Power: {enginePower:F0} kW");
        GUILayout.Label($"ERS Power: {(ersTorque * currentRPM * 2 * Mathf.PI / 60) / 1000:F0} kW");
        GUILayout.Label($"Total Power: {totalPower:F0} kW");
        GUILayout.Label($"Engine Torque: {engineTorque:F0} Nm");
        GUILayout.Label($"Wheel Torque: {wheelTorque:F0} Nm");
        GUILayout.Label($"Fuel: {fuel.currentFuel:F1} / {fuel.maxFuelCapacity:F0} kg");
        GUILayout.Label($"Fuel Flow: {fuel.currentFuelFlow:F1} kg/h");
        GUILayout.Label($"ERS Energy: {ers.currentEnergy / 1000000:F2} / {ers.maxEnergyPerLap / 1000000:F1} MJ");
        GUILayout.Label($"Speed: {wheelSpeed * 3.6f:F0} km/h");
        GUILayout.EndArea();
    }
}
