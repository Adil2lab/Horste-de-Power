using UnityEngine;

[System.Serializable]
public class BrakeSettings
{
    [Header("Brake Force")]
    public float maxBrakeForce = 12000f; // Maximum brake force in Newtons
    public float brakeBalance = 0.65f; // Front brake bias (0.6-0.7 typical for F1)
    
    [Header("Brake Temperature")]
    public float brakeMass = 5f; // kg (brake disc + caliper)
    public float specificHeat = 500f; // J/(kg·K) - carbon-carbon brakes
    public float brakeArea = 0.15f; // m² - brake disc surface area
    public float heatTransferCoeff = 25f; // W/(m²·K) - convection coefficient
    public float ambientTemperature = 20f; // °C
    public float initialTemperature = 20f; // °C
    
    [Header("Brake Fade")]
    public float fadeStartTemperature = 800f; // °C
    public float fadeFactor = 0.002f; // effectiveness loss per °C
    public float minEffectiveness = 0.1f; // minimum brake effectiveness (10%)
    
    [Header("Efficiency")]
    public float efficiencyLoss = 0.75f; // 75% of brake energy becomes heat
}

[System.Serializable]
public class BrakeData
{
    public WheelCollider wheelCollider;
    public Transform brakeDiscTransform; // For visual effects
    public float currentTemperature;
    public float brakeForce;
    public float effectiveness;
    public float heatGeneration;
    public float heatDissipation;
    public ParticleSystem brakeGlowEffect; // Visual effect for hot brakes
    public ParticleSystem sparkEffect; // Sparks when overheating
}

public class F1BrakingSystem : MonoBehaviour
{
    [Header("Brake Configuration")]
    public BrakeSettings frontBrakes;
    public BrakeSettings rearBrakes;
    
    [Header("Brake Components")]
    public BrakeData frontLeftBrake;
    public BrakeData frontRightBrake;
    public BrakeData rearLeftBrake;
    public BrakeData rearRightBrake;
    
    [Header("Vehicle References")]
    public Rigidbody vehicleRigidbody;
    public Transform centerOfMass;
    
    [Header("Input")]
    [Range(0f, 1f)]
    public float brakeInput = 0f; // 0-1 brake pedal input
    public KeyCode brakeKey = KeyCode.Space;
    public string brakeInputAxis = "Fire1"; // For input manager
    
    [Header("Advanced Settings")]
    public bool useTemperatureSimulation = true;
    public bool useBrakeFade = true;
    public bool enableVisualEffects = true;
    public float airflowSpeedMultiplier = 1f; // Cooling factor based on speed
    
    [Header("Debug")]
    public bool showBrakeForces = true;
    public bool showTemperatureDebug = true;
    public float forceVisualizationScale = 0.0001f;
    
    private BrakeData[] allBrakes;
    private float vehicleSpeed;
    private float lastUpdateTime;
    
    // Temperature thresholds for visual effects
    private const float GLOW_TEMP_THRESHOLD = 400f;
    private const float SPARK_TEMP_THRESHOLD = 900f;
    
    void Start()
    {
        // Initialize brake array
        allBrakes = new BrakeData[] { frontLeftBrake, frontRightBrake, rearLeftBrake, rearRightBrake };
        
        // Validate references
        ValidateReferences();
        
        // Initialize brake temperatures
        InitializeBrakeData();
        
        lastUpdateTime = Time.time;
    }
    
    void ValidateReferences()
    {
        if (vehicleRigidbody == null)
            vehicleRigidbody = GetComponent<Rigidbody>();
            
        foreach (var brake in allBrakes)
        {
            if (brake.wheelCollider == null)
                Debug.LogWarning("Missing WheelCollider reference in brake system!");
        }
    }
    
    void InitializeBrakeData()
    {
        foreach (var brake in allBrakes)
        {
            BrakeSettings settings = IsFrontBrake(brake) ? frontBrakes : rearBrakes;
            brake.currentTemperature = settings.initialTemperature;
            brake.effectiveness = 1f;
            brake.brakeForce = 0f;
            brake.heatGeneration = 0f;
            brake.heatDissipation = 0f;
        }
    }
    
    void Update()
    {
        // Handle input
        HandleBrakeInput();
        
        // Update visual effects
        if (enableVisualEffects)
            UpdateVisualEffects();
    }
    
    void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        
        // Calculate vehicle speed
        vehicleSpeed = vehicleRigidbody.velocity.magnitude;
        
        // Update brake system
        UpdateBrakeForces(deltaTime);
        
        if (useTemperatureSimulation)
            UpdateBrakeTemperatures(deltaTime);
            
        if (useBrakeFade)
            UpdateBrakeFade();
        
        // Apply brake forces
        ApplyBrakeForces();
    }
    
    void HandleBrakeInput()
    {
        // Get brake input from multiple sources
        float keyInput = Input.GetKey(brakeKey) ? 1f : 0f;
        float axisInput = 0f;
        
        try
        {
            axisInput = Input.GetAxis(brakeInputAxis);
        }
        catch
        {
            // Input axis not configured, use key input only
        }
        
        // Use the maximum input value
        brakeInput = Mathf.Max(keyInput, axisInput);
        brakeInput = Mathf.Clamp01(brakeInput);
    }
    
    void UpdateBrakeForces(float deltaTime)
    {
        // Calculate brake forces based on input and brake balance
        // F_brake_front = brake_input * max_brake_force * brake_balance
        // F_brake_rear = brake_input * max_brake_force * (1 - brake_balance)
        
        float frontForce = brakeInput * frontBrakes.maxBrakeForce * frontBrakes.brakeBalance;
        float rearForce = brakeInput * rearBrakes.maxBrakeForce * (1f - frontBrakes.brakeBalance);
        
        // Apply effectiveness (brake fade)
        frontForce *= frontLeftBrake.effectiveness;
        rearForce *= rearLeftBrake.effectiveness;
        
        // Distribute forces to individual wheels
        frontLeftBrake.brakeForce = frontForce * 0.5f;
        frontRightBrake.brakeForce = frontForce * 0.5f;
        rearLeftBrake.brakeForce = rearForce * 0.5f;
        rearRightBrake.brakeForce = rearForce * 0.5f;
    }
    
    void UpdateBrakeTemperatures(float deltaTime)
    {
        foreach (var brake in allBrakes)
        {
            if (brake.wheelCollider == null) continue;
            
            BrakeSettings settings = IsFrontBrake(brake) ? frontBrakes : rearBrakes;
            
            // Get wheel velocity
            float wheelRPM = brake.wheelCollider.rpm;
            float wheelVelocity = Mathf.Abs(wheelRPM * 2f * Mathf.PI * brake.wheelCollider.radius / 60f);
            
            // Calculate heat generation: Q_generated = F_brake * v_wheel * efficiency_loss
            brake.heatGeneration = brake.brakeForce * wheelVelocity * settings.efficiencyLoss;
            
            // Calculate heat dissipation: Q_dissipated = h * A_brake * (T_brake - T_ambient)
            float tempDifference = brake.currentTemperature - settings.ambientTemperature;
            float airflowFactor = 1f + (vehicleSpeed * airflowSpeedMultiplier * 0.01f); // Increased cooling at speed
            brake.heatDissipation = settings.heatTransferCoeff * settings.brakeArea * tempDifference * airflowFactor;
            
            // Update temperature: T_brake_new = T_brake + (Q_generated - Q_dissipated) / (m_brake * c_brake)
            float netHeat = brake.heatGeneration - brake.heatDissipation;
            float tempChange = netHeat / (settings.brakeMass * settings.specificHeat);
            
            brake.currentTemperature += tempChange * deltaTime;
            
            // Clamp temperature to reasonable bounds
            brake.currentTemperature = Mathf.Max(brake.currentTemperature, settings.ambientTemperature);
            brake.currentTemperature = Mathf.Min(brake.currentTemperature, 1200f); // Maximum realistic brake temp
        }
    }
    
    void UpdateBrakeFade()
    {
        foreach (var brake in allBrakes)
        {
            BrakeSettings settings = IsFrontBrake(brake) ? frontBrakes : rearBrakes;
            
            // Calculate brake fade: brake_effectiveness = max(0.1, 1 - fade_factor * max(0, T_brake - T_fade_start))
            float tempOverFadeStart = Mathf.Max(0f, brake.currentTemperature - settings.fadeStartTemperature);
            brake.effectiveness = Mathf.Max(settings.minEffectiveness, 
                1f - settings.fadeFactor * tempOverFadeStart);
        }
    }
    
    void ApplyBrakeForces()
    {
        foreach (var brake in allBrakes)
        {
            if (brake.wheelCollider != null)
            {
                // Apply brake torque to wheel collider
                float brakeTorque = brake.brakeForce * brake.wheelCollider.radius;
                brake.wheelCollider.brakeTorque = brakeTorque;
            }
        }
    }
    
    void UpdateVisualEffects()
    {
        foreach (var brake in allBrakes)
        {
            // Brake glow effect
            if (brake.brakeGlowEffect != null)
            {
                var emission = brake.brakeGlowEffect.emission;
                if (brake.currentTemperature > GLOW_TEMP_THRESHOLD)
                {
                    if (!brake.brakeGlowEffect.isPlaying)
                        brake.brakeGlowEffect.Play();
                        
                    // Scale emission based on temperature
                    float glowIntensity = (brake.currentTemperature - GLOW_TEMP_THRESHOLD) / 400f;
                    emission.rateOverTime = Mathf.Clamp(glowIntensity * 50f, 0f, 100f);
                }
                else
                {
                    if (brake.brakeGlowEffect.isPlaying)
                        brake.brakeGlowEffect.Stop();
                }
            }
            
            // Spark effect for overheating
            if (brake.sparkEffect != null)
            {
                if (brake.currentTemperature > SPARK_TEMP_THRESHOLD && brakeInput > 0.5f)
                {
                    if (!brake.sparkEffect.isPlaying)
                        brake.sparkEffect.Play();
                }
                else
                {
                    if (brake.sparkEffect.isPlaying)
                        brake.sparkEffect.Stop();
                }
            }
            
            // Update brake disc color/material if available
            if (brake.brakeDiscTransform != null)
            {
                Renderer discRenderer = brake.brakeDiscTransform.GetComponent<Renderer>();
                if (discRenderer != null)
                {
                    // Change emission color based on temperature
                    Color heatColor = GetHeatColor(brake.currentTemperature);
                    discRenderer.material.SetColor("_EmissionColor", heatColor);
                }
            }
        }
    }
    
    Color GetHeatColor(float temperature)
    {
        if (temperature < 200f) return Color.black;
        if (temperature < 400f) return Color.Lerp(Color.black, Color.red * 0.3f, (temperature - 200f) / 200f);
        if (temperature < 600f) return Color.Lerp(Color.red * 0.3f, Color.red, (temperature - 400f) / 200f);
        if (temperature < 800f) return Color.Lerp(Color.red, Color.yellow, (temperature - 600f) / 200f);
        return Color.Lerp(Color.yellow, Color.white, Mathf.Min((temperature - 800f) / 300f, 1f));
    }
    
    bool IsFrontBrake(BrakeData brake)
    {
        return brake == frontLeftBrake || brake == frontRightBrake;
    }
    
    // Public API methods
    public float GetBrakeTemperature(int brakeIndex)
    {
        if (brakeIndex >= 0 && brakeIndex < allBrakes.Length)
            return allBrakes[brakeIndex].currentTemperature;
        return 0f;
    }
    
    public float GetBrakeEffectiveness(int brakeIndex)
    {
        if (brakeIndex >= 0 && brakeIndex < allBrakes.Length)
            return allBrakes[brakeIndex].effectiveness;
        return 1f;
    }
    
    public float GetAverageBrakeTemp()
    {
        float total = 0f;
        foreach (var brake in allBrakes)
            total += brake.currentTemperature;
        return total / allBrakes.Length;
    }
    
    public void SetBrakeBalance(float newBalance)
    {
        frontBrakes.brakeBalance = Mathf.Clamp01(newBalance);
    }
    
    public void EmergencyBrake()
    {
        brakeInput = 1f;
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!showBrakeForces) return;
        
        if (allBrakes != null)
        {
            foreach (var brake in allBrakes)
            {
                if (brake.wheelCollider == null) continue;
                
                Vector3 wheelPos = brake.wheelCollider.transform.position;
                
                // Draw brake force vector
                Vector3 brakeForceVector = -brake.wheelCollider.transform.forward * brake.brakeForce * forceVisualizationScale;
                
                // Color based on temperature
                if (brake.currentTemperature > 800f)
                    Gizmos.color = Color.red;
                else if (brake.currentTemperature > 400f)
                    Gizmos.color = Color.yellow;
                else
                    Gizmos.color = Color.blue;
                
                Gizmos.DrawLine(wheelPos, wheelPos + brakeForceVector);
                Gizmos.DrawWireSphere(wheelPos + brakeForceVector, 0.05f);
            }
        }
    }
    
    void OnGUI()
    {
        if (!showTemperatureDebug) return;
        
        GUILayout.BeginArea(new Rect(10, 270, 350, 250));
        GUILayout.Label("=== F1 Braking System Debug ===");
        GUILayout.Label($"Brake Input: {brakeInput:F2} ({brakeInput * 100f:F0}%)");
        GUILayout.Label($"Vehicle Speed: {vehicleSpeed * 3.6f:F1} km/h");
        GUILayout.Label($"Brake Balance: {frontBrakes.brakeBalance:F2} ({frontBrakes.brakeBalance * 100f:F0}% front)");
        
        if (allBrakes != null)
        {
            string[] brakeNames = { "Front Left", "Front Right", "Rear Left", "Rear Right" };
            for (int i = 0; i < allBrakes.Length; i++)
            {
                var brake = allBrakes[i];
                GUILayout.Label($"{brakeNames[i]}:");
                GUILayout.Label($"  Temp: {brake.currentTemperature:F0}°C");
                GUILayout.Label($"  Force: {brake.brakeForce:F0}N");
                GUILayout.Label($"  Effectiveness: {brake.effectiveness:F2} ({brake.effectiveness * 100f:F0}%)");
            }
        }
        
        GUILayout.EndArea();
    }
}
