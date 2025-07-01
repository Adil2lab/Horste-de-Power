using UnityEngine;

[System.Serializable]
public class PacejkaCoefficients
{
    [Header("Pacejka Magic Formula Coefficients")]
    [Range(8f, 15f)]
    public float B_stiffnessFactor = 10f;
    
    [Range(1.3f, 1.7f)]
    public float C_shapeFactor = 1.5f;
    
    [Range(2000f, 4000f)]
    public float D_peakFactor = 3000f; // Maximum lateral force (N)
    
    [Range(-0.5f, 0.5f)]
    public float E_curvatureFactor = 0f;
}

[System.Serializable]
public class TireTemperatureSettings
{
    [Header("Temperature Model")]
    [Range(80f, 120f)]
    public float optimalTemp = 95f; // °C
    
    [Range(15f, 25f)]
    public float temperatureWindow = 20f; // Grip falloff range
    
    [Range(0.001f, 0.01f)]
    public float heatGenerationRate = 0.005f;
    
    [Range(0.1f, 0.5f)]
    public float coolingRate = 0.3f;
    
    public float ambientTemperature = 25f; // °C
    public float currentTemperature = 80f; // °C
}

[System.Serializable]
public class TireWearSettings
{
    [Header("Tire Wear Model")]
    [Range(0f, 100f)]
    public float currentWear = 0f; // Percentage
    
    [Range(0.001f, 0.01f)]
    public float baseWearRate = 0.003f;
    
    [Range(1f, 3f)]
    public float slipWearMultiplier = 2f;
    
    [Range(1f, 2f)]
    public float tempWearMultiplier = 1.5f;
    
    [Range(1f, 2f)]
    public float loadWearExponent = 1.2f;
    
    public float referenceLoad = 5000f; // N
}

[System.Serializable]
public class WheelPhysics
{
    [Header("Wheel Properties")]
    public float radius = 0.33f; // meters
    public float width = 0.305f; // meters
    public float mass = 12f; // kg including tire
    public float inertia = 1.5f; // kg⋅m²
    
    [Header("Contact Patch")]
    public float contactPatchArea = 300f; // cm²
    
    [Header("Grip Coefficients")]
    [Range(1.6f, 2.2f)]
    public float peakGripDry = 2.0f;
    
    [Range(0.8f, 1.4f)]
    public float peakGripWet = 1.2f;
    
    [Range(0f, 1f)]
    public float surfaceWetness = 0f; // 0 = dry, 1 = wet
}

public class F1WheelSystem : MonoBehaviour
{
    [Header("Wheel Settings")]
    public PacejkaCoefficients pacejka;
    public TireTemperatureSettings temperature;
    public TireWearSettings wear;
    public WheelPhysics physics;
    
    [Header("Runtime Values")]
    public float slipAngle = 0f; // radians
    public float slipRatio = 0f;
    public float normalForce = 5000f; // N
    public float angularVelocity = 0f; // rad/s
    
    [Header("Forces")]
    public Vector3 lateralForce = Vector3.zero;
    public Vector3 longitudinalForce = Vector3.zero;
    public Vector3 totalTireForce = Vector3.zero;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showGizmos = true;
    
    // Private variables
    private float gripMultiplier = 1f;
    private float effectiveGrip = 1f;
    private Rigidbody vehicleRigidbody;
    private Vector3 wheelVelocity;
    private float wheelRPM;
    
    void Start()
    {
        vehicleRigidbody = GetComponentInParent<Rigidbody>();
        if (vehicleRigidbody == null)
        {
            Debug.LogError("F1WheelSystem: No Rigidbody found in parent!");
        }
    }
    
    void FixedUpdate()
    {
        UpdateWheelPhysics();
        CalculateTireForces();
        UpdateTireTemperature();
        UpdateTireWear();
        ApplyForces();
    }
    
    void UpdateWheelPhysics()
    {
        if (vehicleRigidbody == null) return;
        
        // Get wheel velocity
        wheelVelocity = vehicleRigidbody.GetPointVelocity(transform.position);
        float forwardSpeed = Vector3.Dot(wheelVelocity, transform.forward);
        
        // Calculate wheel RPM and angular velocity
        wheelRPM = angularVelocity * 9.549f; // Convert rad/s to RPM
        float wheelSpeed = angularVelocity * physics.radius;
        
        // Calculate slip ratio: (wheel_speed - vehicle_speed) / max(wheel_speed, vehicle_speed)
        if (Mathf.Abs(forwardSpeed) > 0.1f || Mathf.Abs(wheelSpeed) > 0.1f)
        {
            slipRatio = (wheelSpeed - forwardSpeed) / Mathf.Max(Mathf.Abs(wheelSpeed), Mathf.Abs(forwardSpeed));
        }
        else
        {
            slipRatio = 0f;
        }
        
        // Calculate slip angle (simplified)
        Vector3 lateralVelocity = wheelVelocity - Vector3.Project(wheelVelocity, transform.forward);
        if (forwardSpeed > 1f) // Avoid division by zero at low speeds
        {
            slipAngle = Mathf.Atan2(lateralVelocity.magnitude, Mathf.Abs(forwardSpeed));
        }
        else
        {
            slipAngle = 0f;
        }
    }
    
    void CalculateTireForces()
    {
        // Calculate grip multiplier based on temperature
        CalculateTemperatureGripMultiplier();
        
        // Calculate effective grip based on surface conditions
        effectiveGrip = Mathf.Lerp(physics.peakGripDry, physics.peakGripWet, physics.surfaceWetness);
        effectiveGrip *= gripMultiplier;
        effectiveGrip *= (1f - wear.currentWear * 0.01f); // Wear reduces grip
        
        // Pacejka Magic Formula: F_y = D * sin(C * arctan(B * α - E * (B * α - arctan(B * α))))
        float Balpha = pacejka.B_stiffnessFactor * slipAngle;
        float arctanBalpha = Mathf.Atan(Balpha);
        float magicFormula = pacejka.D_peakFactor * Mathf.Sin(pacejka.C_shapeFactor * 
                            Mathf.Atan(Balpha - pacejka.E_curvatureFactor * (Balpha - arctanBalpha)));
        
        // Apply grip multiplier and normal force scaling
        float maxLateralForce = magicFormula * effectiveGrip * (normalForce / 5000f);
        
        // Calculate lateral force
        Vector3 lateralDirection = Vector3.Cross(transform.forward, Vector3.up).normalized;
        lateralForce = lateralDirection * maxLateralForce;
        
        // Calculate longitudinal force (simplified traction model)
        float maxLongitudinalForce = effectiveGrip * normalForce;
        float longitudinalForceMagnitude = Mathf.Clamp(slipRatio * maxLongitudinalForce * 10f, 
                                                      -maxLongitudinalForce, maxLongitudinalForce);
        longitudinalForce = transform.forward * longitudinalForceMagnitude;
        
        totalTireForce = lateralForce + longitudinalForce;
    }
    
    void CalculateTemperatureGripMultiplier()
    {
        // Gaussian curve for temperature-grip relationship
        // grip_multiplier = exp(-0.5 * ((T - T_optimal) / σ)²)
        float tempDifference = temperature.currentTemperature - temperature.optimalTemp;
        float sigma = temperature.temperatureWindow;
        gripMultiplier = Mathf.Exp(-0.5f * (tempDifference * tempDifference) / (sigma * sigma));
    }
    
    void UpdateTireTemperature()
    {
        // Heat generation: heat_generation = k_friction * |slip_ratio| * normal_force * velocity
        float velocity = wheelVelocity.magnitude;
        float heatGeneration = temperature.heatGenerationRate * Mathf.Abs(slipRatio) * 
                              normalForce * velocity;
        
        // Heat dissipation: heat_dissipation = k_cooling * (T_current - T_ambient)
        float heatDissipation = temperature.coolingRate * 
                               (temperature.currentTemperature - temperature.ambientTemperature);
        
        // Update temperature: T_new = T_current + (heat_generation - heat_dissipation) * dt
        temperature.currentTemperature += (heatGeneration - heatDissipation) * Time.fixedDeltaTime;
        temperature.currentTemperature = Mathf.Max(temperature.ambientTemperature, temperature.currentTemperature);
    }
    
    void UpdateTireWear()
    {
        // Wear calculation: wear_rate = base_wear * slip_factor * temperature_factor * load_factor
        float slipFactor = 1f + wear.slipWearMultiplier * Mathf.Abs(slipRatio);
        float temperatureFactor = 1f + wear.tempWearMultiplier * 
                                 Mathf.Max(0f, temperature.currentTemperature - temperature.optimalTemp) * 0.01f;
        float loadFactor = Mathf.Pow(normalForce / wear.referenceLoad, wear.loadWearExponent);
        
        float wearRate = wear.baseWearRate * slipFactor * temperatureFactor * loadFactor;
        wear.currentWear += wearRate * Time.fixedDeltaTime;
        wear.currentWear = Mathf.Clamp(wear.currentWear, 0f, 100f);
    }
    
    void ApplyForces()
    {
        if (vehicleRigidbody == null) return;
        
        // Apply tire forces to the vehicle
        vehicleRigidbody.AddForceAtPosition(totalTireForce, transform.position, ForceMode.Force);
        
        // Apply rolling resistance
        float rollingResistance = 0.012f * normalForce; // Coefficient from your document
        Vector3 rollingForce = -wheelVelocity.normalized * rollingResistance;
        vehicleRigidbody.AddForceAtPosition(rollingForce, transform.position, ForceMode.Force);
    }
    
    // Public methods for external control
    public void SetNormalForce(float force)
    {
        normalForce = Mathf.Max(0f, force);
    }
    
    public void SetAngularVelocity(float velocity)
    {
        angularVelocity = velocity;
    }
    
    public void SetSurfaceWetness(float wetness)
    {
        physics.surfaceWetness = Mathf.Clamp01(wetness);
    }
    
    public float GetGripLevel()
    {
        return effectiveGrip;
    }
    
    public float GetTireTemperature()
    {
        return temperature.currentTemperature;
    }
    
    public float GetTireWear()
    {
        return wear.currentWear;
    }
    
    public Vector3 GetTotalForce()
    {
        return totalTireForce;
    }
    
    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        
        // Draw tire contact patch
        Gizmos.color = Color.yellow;
        float contactRadius = Mathf.Sqrt(physics.contactPatchArea * 0.0001f / Mathf.PI); // Convert cm² to m²
        Gizmos.DrawWireSphere(transform.position, contactRadius);
        
        // Draw force vectors
        if (Application.isPlaying)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, lateralForce * 0.0001f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, longitudinalForce * 0.0001f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, totalTireForce * 0.0001f);
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        Vector2 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        Rect guiRect = new Rect(screenPos.x - 100, Screen.height - screenPos.y - 60, 200, 120);
        
        GUILayout.BeginArea(guiRect);
        GUILayout.Label($"Temp: {temperature.currentTemperature:F0}°C");
        GUILayout.Label($"Wear: {wear.currentWear:F1}%");
        GUILayout.Label($"Grip: {effectiveGrip:F2}");
        GUILayout.Label($"Slip Angle: {slipAngle * Mathf.Rad2Deg:F1}°");
        GUILayout.Label($"Slip Ratio: {slipRatio:F2}");
        GUILayout.Label($"Force: {totalTireForce.magnitude:F0}N");
        GUILayout.EndArea();
    }
}