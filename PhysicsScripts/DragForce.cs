using UnityEngine;

[System.Serializable]
public class AerodynamicSettings
{
    [Header("Aerodynamic Properties")]
    [Range(0.7f, 1.1f)]
    public float dragCoefficient = 0.9f;
    
    [Range(1.5f, 1.8f)]
    public float referenceArea = 1.65f; // m²
    
    [Range(1.0f, 1.3f)]
    public float airDensity = 1.225f; // kg/m³
    
    [Header("Environmental")]
    public float altitude = 0f;
    public float temperature = 20f;
}

[System.Serializable]
public class RollingResistanceSettings
{
    [Header("Rolling Resistance")]
    [Range(0.008f, 0.015f)]
    public float rollingCoefficient = 0.012f;
    
    [Header("Tire Properties")]
    public float tireRadius = 0.33f; // meters
    public float tireWidth = 0.305f; // meters
    public AnimationCurve temperatureEffect = AnimationCurve.Linear(0, 1, 150, 1.5f);
}

[System.Serializable]
public class BrakeSettings
{
    [Header("Brake Properties")]
    public float maxBrakeForce = 15000f; // N
    [Range(0.0f, 1.0f)]
    public float brakeBalance = 0.6f; // front bias
    
    [Header("Brake Temperature")]
    public float brakeTemp = 400f; // °C
    [Range(800f, 900f)]
    public float fadeStartTemp = 800f; // °C
    [Range(0.001f, 0.003f)]
    public float fadeFactor = 0.002f; // per °C
    
    [Header("Heat Generation")]
    public float brakeMass = 15f; // kg per brake
    public float specificHeat = 500f; // J/(kg·K)
    public float coolingRate = 0.1f;
    public float ambientTemp = 25f; // °C
}

public class F1CompleteDragSystem : MonoBehaviour
{
    [SerializeField] private AerodynamicSettings aeroSettings;
    [SerializeField] private RollingResistanceSettings rollingSettings;
    [SerializeField] private BrakeSettings brakeSettings;
    [SerializeField] private Rigidbody vehicleRigidbody;
    
    [Header("Input")]
    [Range(0f, 1f)]
    public float brakeInput = 0f;
    
    [Header("Debug")]
    public bool showDebugInfo = true;
    
    // Force components
    private Vector3 aeroDragForce;
    private Vector3 rollingResistanceForce;
    private Vector3 brakeDragForce;
    private Vector3 totalDragForce;
    
    // Runtime values
    private float currentSpeed;
    private float adjustedAirDensity;
    private float normalForce;
    private float brakeEffectiveness = 1f;
    private float[] wheelTemperatures = new float[4];
    
    void Start()
    {
        if (vehicleRigidbody == null)
            vehicleRigidbody = GetComponent<Rigidbody>();
            
        CalculateAirDensity();
        InitializeTireTemperatures();
    }
    
    void FixedUpdate()
    {
        CalculateForces();
        ApplyDragForces();
        UpdateBrakeTemperature();
    }
    
    void CalculateForces()
    {
        Vector3 velocity = vehicleRigidbody.velocity;
        currentSpeed = velocity.magnitude;
        
        if (currentSpeed < 0.1f)
        {
            aeroDragForce = rollingResistanceForce = brakeDragForce = Vector3.zero;
            return;
        }
        
        Vector3 velocityDirection = velocity.normalized;
        normalForce = vehicleRigidbody.mass * 9.81f; // Simplified normal force
        
        // 1. Aerodynamic Drag: F_drag = 0.5 * ρ * v² * A * C_D
        float aeroDragMagnitude = 0.5f * adjustedAirDensity * currentSpeed * currentSpeed * 
                                 aeroSettings.referenceArea * aeroSettings.dragCoefficient;
        aeroDragForce = -velocityDirection * aeroDragMagnitude;
        
        // 2. Rolling Resistance: F_rolling = C_rr * F_normal
        float avgTireTemp = GetAverageTireTemperature();
        float tempMultiplier = rollingSettings.temperatureEffect.Evaluate(avgTireTemp);
        float rollingMagnitude = rollingSettings.rollingCoefficient * normalForce * tempMultiplier;
        rollingResistanceForce = -velocityDirection * rollingMagnitude;
        
        // 3. Brake Drag
        if (brakeInput > 0.01f)
        {
            float frontBrakeForce = brakeInput * brakeSettings.maxBrakeForce * brakeSettings.brakeBalance * brakeEffectiveness;
            float rearBrakeForce = brakeInput * brakeSettings.maxBrakeForce * (1f - brakeSettings.brakeBalance) * brakeEffectiveness;
            float totalBrakeForce = frontBrakeForce + rearBrakeForce;
            
            brakeDragForce = -velocityDirection * totalBrakeForce;
        }
        else
        {
            brakeDragForce = Vector3.zero;
        }
        
        totalDragForce = aeroDragForce + rollingResistanceForce + brakeDragForce;
    }
    
    void ApplyDragForces()
    {
        vehicleRigidbody.AddForce(totalDragForce, ForceMode.Force);
        
        if (showDebugInfo)
        {
            Debug.DrawRay(transform.position, aeroDragForce * 0.0001f, Color.red, 0.1f);
            Debug.DrawRay(transform.position + Vector3.up * 0.5f, rollingResistanceForce * 0.001f, Color.green, 0.1f);
            Debug.DrawRay(transform.position + Vector3.up * 1f, brakeDragForce * 0.0001f, Color.blue, 0.1f);
        }
    }
    
    void UpdateBrakeTemperature()
    {
        if (brakeInput > 0.01f)
        {
            // Heat generation: Q = F_brake * v_wheel * efficiency_loss
            float heatGeneration = brakeDragForce.magnitude * currentSpeed * 0.35f; // 35% heat loss
            float tempIncrease = heatGeneration / (brakeSettings.brakeMass * brakeSettings.specificHeat * 4f); // 4 brakes
            brakeSettings.brakeTemp += tempIncrease * Time.fixedDeltaTime;
        }
        
        // Heat dissipation
        float heatDissipation = brakeSettings.coolingRate * (brakeSettings.brakeTemp - brakeSettings.ambientTemp);
        brakeSettings.brakeTemp -= heatDissipation * Time.fixedDeltaTime;
        brakeSettings.brakeTemp = Mathf.Max(brakeSettings.ambientTemp, brakeSettings.brakeTemp);
        
        // Brake fade calculation
        if (brakeSettings.brakeTemp > brakeSettings.fadeStartTemp)
        {
            float fadeAmount = brakeSettings.fadeFactor * (brakeSettings.brakeTemp - brakeSettings.fadeStartTemp);
            brakeEffectiveness = Mathf.Max(0.1f, 1f - fadeAmount);
        }
        else
        {
            brakeEffectiveness = 1f;
        }
    }
    
    void CalculateAirDensity()
    {
        float tempKelvin = aeroSettings.temperature + 273.15f;
        float altitudeFactor = Mathf.Exp(-aeroSettings.altitude / 8400f);
        float temperatureFactor = 288.15f / tempKelvin;
        adjustedAirDensity = aeroSettings.airDensity * altitudeFactor * temperatureFactor;
    }
    
    void InitializeTireTemperatures()
    {
        for (int i = 0; i < wheelTemperatures.Length; i++)
        {
            wheelTemperatures[i] = 80f; // Starting tire temp
        }
    }
    
    float GetAverageTireTemperature()
    {
        float total = 0f;
        for (int i = 0; i < wheelTemperatures.Length; i++)
        {
            total += wheelTemperatures[i];
        }
        return total / wheelTemperatures.Length;
    }
    
    // Public accessors
    public Vector3 GetAeroDragForce() => aeroDragForce;
    public Vector3 GetRollingResistance() => rollingResistanceForce;
    public Vector3 GetBrakeDragForce() => brakeDragForce;
    public Vector3 GetTotalDragForce() => totalDragForce;
    public float GetBrakeTemperature() => brakeSettings.brakeTemp;
    public float GetBrakeEffectiveness() => brakeEffectiveness;
    
    public void SetBrakeInput(float input)
    {
        brakeInput = Mathf.Clamp01(input);
    }
    
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            CalculateAirDensity();
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 350, 200));
        GUILayout.Label($"Speed: {currentSpeed:F1} m/s ({currentSpeed * 3.6f:F1} km/h)");
        GUILayout.Label($"Aero Drag: {aeroDragForce.magnitude:F0} N");
        GUILayout.Label($"Rolling Resistance: {rollingResistanceForce.magnitude:F0} N");
        GUILayout.Label($"Brake Drag: {brakeDragForce.magnitude:F0} N");
        GUILayout.Label($"Total Drag: {totalDragForce.magnitude:F0} N");
        GUILayout.Label($"Brake Temp: {brakeSettings.brakeTemp:F0}°C");
        GUILayout.Label($"Brake Effectiveness: {brakeEffectiveness:F2}");
        GUILayout.Label($"Air Density: {adjustedAirDensity:F3} kg/m³");
        GUILayout.EndArea();
    }
}
