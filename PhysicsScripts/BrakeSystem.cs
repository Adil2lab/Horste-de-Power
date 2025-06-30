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
public class WheelPhysics
{
    [Header("Wheel Properties")]
    public float wheelRadius = 0.33f; // meters
    public float wheelMass = 20f; // kg
    public float momentOfInertia = 2.2f; // kg⋅m² (calculated from mass and radius)
    
    [Header("Ground Contact")]
    public LayerMask groundLayers = -1;
    public float contactPatchLength = 0.2f; // meters
    public float contactPatchWidth = 0.3f; // meters
    public float maxGroundDistance = 1f; // max raycast distance
    
    [Header("Friction")]
    public AnimationCurve frictionCurve = AnimationCurve.Linear(0, 1, 1, 0.7f);
    public float staticFrictionCoeff = 1.8f;
    public float kineticFrictionCoeff = 1.2f;
    public float rollingResistance = 0.015f;
}

[System.Serializable]
public class WheelLockupSettings
{
    [Header("Lock-up Thresholds")]
    public float lockupSlipThreshold = 0.2f; // Slip ratio above which wheel locks (20%)
    public float unlockSlipThreshold = 0.15f; // Slip ratio below which wheel unlocks (15%)
    public float maxSlipRatio = 1.0f; // Maximum slip ratio (100% = complete lock)
    
    [Header("Lock-up Dynamics")]
    public float lockupGripMultiplier = 0.4f; // Grip reduction when locked (40% of normal)
    public float lockupTransitionSpeed = 5f; // How fast lock-up occurs
    public float unlockTransitionSpeed = 8f; // How fast wheel unlocks
    
    [Header("Surface Conditions")]
    public float dryGripCoefficient = 1.8f; // Maximum grip on dry surface
    public float wetGripCoefficient = 0.9f; // Maximum grip on wet surface
    public bool isWetSurface = false;
    
    [Header("Lock-up Effects")]
    public float flatSpotDamageRate = 0.001f; // Tire damage per second when locked
    public float lockupHeatMultiplier = 2.5f; // Extra heat generation when locked
    public float vibrationIntensity = 0.3f; // Controller/camera vibration when locked
}

[System.Serializable]
public class ABSSettings
{
    [Header("ABS Configuration")]
    public bool absEnabled = true;
    public float absActivationThreshold = 0.18f; // Slip ratio that triggers ABS
    public float absTargetSlip = 0.12f; // Target slip ratio for optimal braking
    
    [Header("ABS Cycling")]
    public float absCycleFrequency = 12f; // Hz - how fast ABS pulses
    public float absReleaseAmount = 0.3f; // How much brake pressure to release (30%)
    public float absReapplySpeed = 15f; // How fast to reapply brakes
    
    [Header("ABS Tuning")]
    public float frontABSAggressiveness = 1.0f; // Multiplier for front ABS sensitivity
    public float rearABSAggressiveness = 0.8f; // Multiplier for rear ABS sensitivity
}

[System.Serializable]
public class BrakeData
{
    [Header("References")]
    public Transform wheelTransform; // Physical wheel position
    public Transform brakeDiscTransform; // For visual effects
    public Transform wheelMeshTransform; // For wheel rotation animation
    public WheelPhysics wheelPhysics;
    
    [Header("Physics State")]
    public float angularVelocity = 0f; // rad/s
    public float wheelSpeed = 0f; // m/s (circumferential)
    public Vector3 contactPoint = Vector3.zero;
    public Vector3 contactNormal = Vector3.up;
    public bool hasGroundContact = false;
    public float groundDistance = 0f;
    public RaycastHit groundHit;
    
    [Header("Brake State")]
    public float currentTemperature;
    public float brakeForce;
    public float effectiveness;
    public float heatGeneration;
    public float heatDissipation;
    
    [Header("Lock-up State")]
    public bool isLocked = false;
    public bool wasLocked = false;
    public float slipRatio = 0f;
    public float lockupIntensity = 0f; // 0-1, how locked the wheel is
    public float currentGripCoefficient = 1.8f;
    public float flatSpotDamage = 0f; // Accumulated tire damage
    public float lockupTime = 0f; // How long wheel has been locked
    
    [Header("ABS State")]
    public bool absActive = false;
    public float absPhase = 0f; // Current phase in ABS cycle
    public float absBrakeMultiplier = 1f; // Current brake force multiplier from ABS
    
    [Header("Visual Effects")]
    public ParticleSystem brakeGlowEffect; // Visual effect for hot brakes
    public ParticleSystem sparkEffect; // Sparks when overheating  
    public ParticleSystem smokeEffect; // Tire smoke when locked
    public ParticleSystem flatSpotEffect; // Sparks from flat spot
    public AudioSource lockupAudioSource; // Tire screech sound
    
    [Header("Wheel Animation")]
    public float wheelRotationSpeed = 0f;
    public float targetWheelRotation = 0f;
    public float currentWheelRotation = 0f;
}

public class F1BrakingSystem : MonoBehaviour
{
    [Header("Brake Configuration")]
    public BrakeSettings frontBrakes;
    public BrakeSettings rearBrakes;
    
    [Header("Lock-up System")]
    public WheelLockupSettings lockupSettings;
    public ABSSettings absSettings;
    
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
    public KeyCode absToggleKey = KeyCode.B;
    public string brakeInputAxis = "Fire1"; // For input manager
    
    [Header("Advanced Settings")]
    public bool useTemperatureSimulation = true;
    public bool useBrakeFade = true;
    public bool enableVisualEffects = true;
    public bool enableLockupSystem = true;
    public float airflowSpeedMultiplier = 1f; // Cooling factor based on speed
    
    [Header("Debug")]
    public bool showBrakeForces = true;
    public bool showTemperatureDebug = true;
    public bool showLockupDebug = true;
    public float forceVisualizationScale = 0.0001f;
    
    private BrakeData[] allBrakes;
    private float vehicleSpeed;
    private float lastUpdateTime;
    private Vector3 lastVelocity;
    private bool anyWheelLocked = false;
    private bool absSystemActive = false;
    
    // Temperature thresholds for visual effects
    private const float GLOW_TEMP_THRESHOLD = 400f;
    private const float SPARK_TEMP_THRESHOLD = 900f;
    private const float LOCKUP_SMOKE_THRESHOLD = 0.3f;
    
    void Start()
    {
        // Initialize brake array
        allBrakes = new BrakeData[] { frontLeftBrake, frontRightBrake, rearLeftBrake, rearRightBrake };
        
        // Validate references
        ValidateReferences();
        
        // Initialize brake data
        InitializeBrakeData();
        
        lastUpdateTime = Time.time;
        lastVelocity = vehicleRigidbody.velocity;
    }
    
    void ValidateReferences()
    {
        if (vehicleRigidbody == null)
            vehicleRigidbody = GetComponent<Rigidbody>();
            
        foreach (var brake in allBrakes)
        {
            if (brake.wheelTransform == null)
                Debug.LogWarning("Missing wheel transform reference in brake system!");
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
            brake.currentGripCoefficient = lockupSettings.isWetSurface ? 
                lockupSettings.wetGripCoefficient : lockupSettings.dryGripCoefficient;
            brake.absBrakeMultiplier = 1f;
            brake.angularVelocity = 0f;
        }
    }
    
    void Update()
    {
        // Handle input
        HandleBrakeInput();
        
        // Update visual effects
        if (enableVisualEffects)
            UpdateVisualEffects();
            
        // Update wheel animations
        UpdateWheelAnimations();
    }
    
    void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        
        // Calculate vehicle speed and acceleration
        vehicleSpeed = vehicleRigidbody.velocity.magnitude;
        Vector3 acceleration = (vehicleRigidbody.velocity - lastVelocity) / deltaTime;
        lastVelocity = vehicleRigidbody.velocity;
        
        // Update wheel physics
        UpdateWheelPhysics(deltaTime);
        
        // Update brake system
        UpdateBrakeForces(deltaTime);
        
        if (useTemperatureSimulation)
            UpdateBrakeTemperatures(deltaTime);
            
        if (useBrakeFade)
            UpdateBrakeFade();
            
        if (enableLockupSystem)
        {
            UpdateWheelSlip(deltaTime);
            UpdateLockupSystem(deltaTime);
            
            if (absSettings.absEnabled)
                UpdateABSSystem(deltaTime);
        }
        
        // Apply brake forces
        ApplyBrakeForces(deltaTime);
        
        // Update system states
        UpdateSystemStates();
    }
    
    void UpdateWheelPhysics(float deltaTime)
    {
        foreach (var brake in allBrakes)
        {
            if (brake.wheelTransform == null) continue;
            
            // Ground detection via raycast
            Vector3 rayStart = brake.wheelTransform.position;
            Vector3 rayDirection = -brake.wheelTransform.up;
            
            brake.hasGroundContact = Physics.Raycast(rayStart, rayDirection, out brake.groundHit, 
                brake.wheelPhysics.maxGroundDistance, brake.wheelPhysics.groundLayers);
            
            if (brake.hasGroundContact)
            {
                brake.contactPoint = brake.groundHit.point;
                brake.contactNormal = brake.groundHit.normal;
                brake.groundDistance = brake.groundHit.distance;
            }
            else
            {
                brake.groundDistance = brake.wheelPhysics.maxGroundDistance;
            }
            
            // Calculate wheel speed from angular velocity
            brake.wheelSpeed = brake.angularVelocity * brake.wheelPhysics.wheelRadius;
        }
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
        
        // Toggle ABS
        if (Input.GetKeyDown(absToggleKey))
        {
            absSettings.absEnabled = !absSettings.absEnabled;
            Debug.Log($"ABS {(absSettings.absEnabled ? "Enabled" : "Disabled")}");
        }
        
        // Use the maximum input value
        brakeInput = Mathf.Max(keyInput, axisInput);
        brakeInput = Mathf.Clamp01(brakeInput);
    }
    
    void UpdateBrakeForces(float deltaTime)
    {
        // Calculate base brake forces
        float frontForce = brakeInput * frontBrakes.maxBrakeForce * frontBrakes.brakeBalance;
        float rearForce = brakeInput * rearBrakes.maxBrakeForce * (1f - frontBrakes.brakeBalance);
        
        // Apply brake effectiveness (fade)
        frontForce *= frontLeftBrake.effectiveness;
        rearForce *= rearLeftBrake.effectiveness;
        
        // Distribute forces to individual wheels and apply ABS/lockup modifiers
        frontLeftBrake.brakeForce = frontForce * 0.5f * frontLeftBrake.absBrakeMultiplier;
        frontRightBrake.brakeForce = frontForce * 0.5f * frontRightBrake.absBrakeMultiplier;
        rearLeftBrake.brakeForce = rearForce * 0.5f * rearLeftBrake.absBrakeMultiplier;
        rearRightBrake.brakeForce = rearForce * 0.5f * rearRightBrake.absBrakeMultiplier;
        
        // Apply lockup grip reduction
        if (enableLockupSystem)
        {
            foreach (var brake in allBrakes)
            {
                if (brake.isLocked)
                {
                    float gripReduction = Mathf.Lerp(1f, lockupSettings.lockupGripMultiplier, brake.lockupIntensity);
                    brake.brakeForce *= gripReduction;
                }
            }
        }
    }
    
    void UpdateWheelSlip(float deltaTime)
    {
        foreach (var brake in allBrakes)
        {
            if (!brake.hasGroundContact || vehicleSpeed < 0.1f)
            {
                brake.slipRatio = 0f;
                continue;
            }
            
            // Get vehicle velocity in wheel's forward direction
            Vector3 wheelForward = brake.wheelTransform.forward;
            float vehicleForwardSpeed = Vector3.Dot(vehicleRigidbody.velocity, wheelForward);
            
            // Calculate slip ratio: slip = (wheel_speed - vehicle_speed) / |vehicle_speed|
            if (Mathf.Abs(vehicleForwardSpeed) > 0.1f)
            {
                brake.slipRatio = Mathf.Abs((brake.wheelSpeed - vehicleForwardSpeed) / vehicleForwardSpeed);
                brake.slipRatio = Mathf.Clamp(brake.slipRatio, 0f, lockupSettings.maxSlipRatio);
            }
            else
            {
                brake.slipRatio = 0f;
            }
        }
    }
    
    void UpdateLockupSystem(float deltaTime)
    {
        anyWheelLocked = false;
        
        foreach (var brake in allBrakes)
        {
            brake.wasLocked = brake.isLocked;
            
            // Check for lockup condition
            if (!brake.isLocked && brake.slipRatio > lockupSettings.lockupSlipThreshold && brakeInput > 0.1f)
            {
                brake.isLocked = true;
                brake.lockupTime = 0f;
                
                // Trigger lockup sound
                if (brake.lockupAudioSource != null && !brake.lockupAudioSource.isPlaying)
                    brake.lockupAudioSource.Play();
            }
            // Check for unlock condition
            else if (brake.isLocked && (brake.slipRatio < lockupSettings.unlockSlipThreshold || brakeInput < 0.05f))
            {
                brake.isLocked = false;
                brake.lockupIntensity = 0f;
                
                // Stop lockup sound
                if (brake.lockupAudioSource != null && brake.lockupAudioSource.isPlaying)
                    brake.lockupAudioSource.Stop();
            }
            
            // Update lockup intensity
            if (brake.isLocked)
            {
                brake.lockupTime += deltaTime;
                brake.lockupIntensity = Mathf.MoveTowards(brake.lockupIntensity, 1f, 
                    lockupSettings.lockupTransitionSpeed * deltaTime);
                    
                // Accumulate flat spot damage
                brake.flatSpotDamage += lockupSettings.flatSpotDamageRate * brake.lockupIntensity * deltaTime;
                brake.flatSpotDamage = Mathf.Clamp01(brake.flatSpotDamage);
                
                anyWheelLocked = true;
            }
            else
            {
                brake.lockupIntensity = Mathf.MoveTowards(brake.lockupIntensity, 0f, 
                    lockupSettings.unlockTransitionSpeed * deltaTime);
                brake.lockupTime = 0f;
            }
            
            // Update grip coefficient based on surface and damage
            float baseGrip = lockupSettings.isWetSurface ? 
                lockupSettings.wetGripCoefficient : lockupSettings.dryGripCoefficient;
            brake.currentGripCoefficient = baseGrip * (1f - brake.flatSpotDamage * 0.2f); // 20% max grip loss from damage
        }
    }
    
    void UpdateABSSystem(float deltaTime)
    {
        absSystemActive = false;
        
        foreach (var brake in allBrakes)
        {
            bool isFront = IsFrontBrake(brake);
            float absThreshold = absSettings.absActivationThreshold * 
                (isFront ? absSettings.frontABSAggressiveness : absSettings.rearABSAggressiveness);
            
            // Check if ABS should activate
            if (brake.slipRatio > absThreshold && brakeInput > 0.1f && !brake.absActive)
            {
                brake.absActive = true;
                brake.absPhase = 0f;
                absSystemActive = true;
            }
            // Check if ABS should deactivate
            else if (brake.absActive && (brake.slipRatio < absSettings.absTargetSlip || brakeInput < 0.05f))
            {
                brake.absActive = false;
                brake.absBrakeMultiplier = 1f;
            }
            
            // Update ABS cycling
            if (brake.absActive)
            {
                absSystemActive = true;
                brake.absPhase += absSettings.absCycleFrequency * deltaTime;
                
                // Create ABS pulsing pattern
                float cycle = Mathf.Sin(brake.absPhase * 2f * Mathf.PI);
                if (cycle > 0f)
                {
                    // Brake release phase
                    brake.absBrakeMultiplier = Mathf.Lerp(brake.absBrakeMultiplier, 
                        1f - absSettings.absReleaseAmount, absSettings.absReapplySpeed * deltaTime);
                }
                else
                {
                    // Brake reapply phase
                    brake.absBrakeMultiplier = Mathf.Lerp(brake.absBrakeMultiplier, 
                        1f, absSettings.absReapplySpeed * deltaTime);
                }
                
                // Prevent lockup when ABS is active
                if (brake.isLocked)
                {
                    brake.isLocked = false;
                    brake.lockupIntensity = 0f;
                }
            }
            else
            {
                brake.absBrakeMultiplier = 1f;
            }
        }
    }
    
    void UpdateBrakeTemperatures(float deltaTime)
    {
        foreach (var brake in allBrakes)
        {
            BrakeSettings settings = IsFrontBrake(brake) ? frontBrakes : rearBrakes;
            
            // Calculate heat generation with lockup multiplier
            float lockupHeatMultiplier = 1f + (brake.lockupIntensity * lockupSettings.lockupHeatMultiplier);
            brake.heatGeneration = brake.brakeForce * Mathf.Abs(brake.wheelSpeed) * settings.efficiencyLoss * lockupHeatMultiplier;
            
            // Calculate heat dissipation
            float tempDifference = brake.currentTemperature - settings.ambientTemperature;
            float airflowFactor = 1f + (vehicleSpeed * airflowSpeedMultiplier * 0.01f);
            brake.heatDissipation = settings.heatTransferCoeff * settings.brakeArea * tempDifference * airflowFactor;
            
            // Update temperature
            float netHeat = brake.heatGeneration - brake.heatDissipation;
            float tempChange = netHeat / (settings.brakeMass * settings.specificHeat);
            
            brake.currentTemperature += tempChange * deltaTime;
            brake.currentTemperature = Mathf.Max(brake.currentTemperature, settings.ambientTemperature);
            brake.currentTemperature = Mathf.Min(brake.currentTemperature, 1200f);
        }
    }
    
    void UpdateBrakeFade()
    {
        foreach (var brake in allBrakes)
        {
            BrakeSettings settings = IsFrontBrake(brake) ? frontBrakes : rearBrakes;
            
            float tempOverFadeStart = Mathf.Max(0f, brake.currentTemperature - settings.fadeStartTemperature);
            brake.effectiveness = Mathf.Max(settings.minEffectiveness, 
                1f - settings.fadeFactor * tempOverFadeStart);
        }
    }
    
    void UpdateWheelAnimations()
    {
        foreach (var brake in allBrakes)
        {
            if (brake.wheelMeshTransform == null) continue;
            
            // Calculate rotation from angular velocity
            float rotationDelta = brake.angularVelocity * Mathf.Rad2Deg * Time.deltaTime;
            brake.currentWheelRotation += rotationDelta;
            
            // Apply rotation to wheel mesh
            brake.wheelMeshTransform.localRotation = Quaternion.Euler(brake.currentWheelRotation, 0, 0);
        }
    }
    
    void ApplyBrakeForces(float deltaTime)
    {
        foreach (var brake in allBrakes)
        {
            if (!brake.hasGroundContact || brake.wheelTransform == null) continue;
            
            // Calculate brake torque and apply to wheel angular velocity
            float brakeTorque = brake.brakeForce * brake.wheelPhysics.wheelRadius;
            float angularDeceleration = brakeTorque / brake.wheelPhysics.momentOfInertia;
            
            // Apply braking deceleration
            if (brake.isLocked)
            {
                // Locked wheel stops rotating
                brake.angularVelocity = Mathf.MoveTowards(brake.angularVelocity, 0f, angularDeceleration * deltaTime * 2f);
            }
            else
            {
                // Normal braking
                float targetAngularVel = Vector3.Dot(vehicleRigidbody.velocity, brake.wheelTransform.forward) / brake.wheelPhysics.wheelRadius;
                brake.angularVelocity = Mathf.MoveTowards(brake.angularVelocity, targetAngularVel, angularDeceleration * deltaTime);
            }
            
            // Calculate and apply friction force to vehicle
            Vector3 wheelForward = brake.wheelTransform.forward;
            Vector3 vehicleVelAtWheel = vehicleRigidbody.GetPointVelocity(brake.wheelTransform.position);
            float forwardVel = Vector3.Dot(vehicleVelAtWheel, wheelForward);
            
            // Calculate friction based on slip and grip
            float maxFriction = brake.currentGripCoefficient * brake.brakeForce;
            Vector3 frictionForce = -wheelForward * Mathf.Min(maxFriction, Mathf.Abs(forwardVel) * 1000f);
            
            // Apply force to vehicle
            vehicleRigidbody.AddForceAtPosition(frictionForce, brake.contactPoint);
        }
    }
    
    void UpdateSystemStates()
    {
        // Update overall system states for external systems (UI, telemetry, etc.)
        // This can be used by other systems to react to brake conditions
    }
    
    void UpdateVisualEffects()
    {
        foreach (var brake in allBrakes)
        {
            // Brake glow effect
            UpdateBrakeGlowEffect(brake);
            
            // Spark effects
            UpdateSparkEffects(brake);
            
            // Lockup smoke effect
            UpdateLockupSmokeEffect(brake);
            
            // Flat spot sparks
            UpdateFlatSpotEffect(brake);
            
            // Update brake disc appearance
            UpdateBrakeDiscAppearance(brake);
        }
    }
    
    void UpdateBrakeGlowEffect(BrakeData brake)
    {
        if (brake.brakeGlowEffect == null) return;
        
        var emission = brake.brakeGlowEffect.emission;
        if (brake.currentTemperature > GLOW_TEMP_THRESHOLD)
        {
            if (!brake.brakeGlowEffect.isPlaying)
                brake.brakeGlowEffect.Play();
                
            float glowIntensity = (brake.currentTemperature - GLOW_TEMP_THRESHOLD) / 400f;
            emission.rateOverTime = Mathf.Clamp(glowIntensity * 50f, 0f, 100f);
        }
        else
        {
            if (brake.brakeGlowEffect.isPlaying)
                brake.brakeGlowEffect.Stop();
        }
    }
    
    void UpdateSparkEffects(BrakeData brake)
    {
        if (brake.sparkEffect == null) return;
        
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
    
    void UpdateLockupSmokeEffect(BrakeData brake)
    {
        if (brake.smokeEffect == null) return;
        
        var emission = brake.smokeEffect.emission;
        if (brake.lockupIntensity > LOCKUP_SMOKE_THRESHOLD && vehicleSpeed > 2f)
        {
            if (!brake.smokeEffect.isPlaying)
                brake.smokeEffect.Play();
                
            emission.rateOverTime = brake.lockupIntensity * 100f;
        }
        else
        {
            if (brake.smokeEffect.isPlaying)
                brake.smokeEffect.Stop();
        }
    }
    
    void UpdateFlatSpotEffect(BrakeData brake)
    {
        if (brake.flatSpotEffect == null) return;
        
        if (brake.flatSpotDamage > 0.1f && vehicleSpeed > 5f)
        {
            if (!brake.flatSpotEffect.isPlaying)
                brake.flatSpotEffect.Play();
                
            var emission = brake.flatSpotEffect.emission;
            emission.rateOverTime = brake.flatSpotDamage * 30f;
        }
        else
        {
            if (brake.flatSpotEffect.isPlaying)
                brake.flatSpotEffect.Stop();
        }
    }
    
    void UpdateBrakeDiscAppearance(BrakeData brake)
    {
        if (brake.brakeDiscTransform == null) return;
        
        Renderer discRenderer = brake.brakeDiscTransform.GetComponent<Renderer>();
        if (discRenderer != null)
        {
            Color heatColor = GetHeatColor(brake.currentTemperature);
            discRenderer.material.SetColor("_EmissionColor", heatColor);
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
    
    public bool IsWheelLocked(int brakeIndex)
    {
        if (brakeIndex >= 0 && brakeIndex < allBrakes.Length)
            return allBrakes[brakeIndex].isLocked;
        return false;
    }
    
    public float GetWheelSlipRatio(int brakeIndex)
    {
        if (brakeIndex >= 0 && brakeIndex < allBrakes.Length)
            return allBrakes[brakeIndex].slipRatio;
        return 0f;
    }
    
    public bool IsABSActive()
    {
        return absSystemActive;
    }
    
    public float GetAverageBrakeTemp()
    {
        float total = 0f;
        foreach (var brake in allBrakes)
            total += brake.currentTemperature;
        return total / allBrakes.Length;
    }
    
    public float GetTotalFlatSpotDamage()
    {
        float total = 0f;
        foreach (var brake in allBrakes)
            total += brake.flatSpotDamage;
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
    
    public void SetSurfaceCondition(bool isWet)
    {
        lockupSettings.isWetSurface = isWet;
        foreach (var brake in allBrakes)
        {
            brake.currentGripCoefficient = isWet ? 
                lockupSettings.wetGripCoefficient : lockupSettings.dryGripCoefficient;
        }
    }
    
    public void ResetFlatSpotDamage()
    {
        foreach (var brake in allBrakes)
        {
            brake.flatSpotDamage = 0f;
        }
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!showBrakeForces || allBrakes == null) return;
        
        foreach (var brake in allBrakes)
        {
            if (brake.wheelCollider == null) continue;
            
            Vector3 wheelPos = brake.wheelCollider.transform.position;
            
            // Draw brake force vector
            Vector3 brakeForceVector = -brake.wheelCollider.transform.forward * brake.brakeForce * forceVisualizationScale;
            
            // Color based on state
            if (brake.isLocked)
                Gizmos.color = Color.red;
            else if (brake.absActive)
                Gizmos.color = Color.yellow;
            else if (brake.currentTemperature > 800f)
                Gizmos.color = Color.magenta;
            else if (brake.currentTemperature > 400f)
                Gizmos.color = Color.orange;
            else
                Gizmos.color = Color.blue;
            
            Gizmos.DrawLine(wheelPos, wheelPos + brakeForceVector);
            Gizmos.DrawWireSphere(wheelPos + brakeForceVector, 0.05f);
            
            // Draw slip indicator
            if (brake.slipRatio > 0.1f)
            {
                Gizmos.color = Color.Lerp(Color.green, Color.red, brake.slipRatio);
                Gizmos.DrawWireCube(wheelPos + Vector3.up * 0.5f, Vector3.one * 0.1f);
            }
        }
    }
    
    void OnGUI()
    {
        if (!showTemperatureDebug && !showLockupDebug) return;
        
        GUILayout.BeginArea(new Rect(10, 270, 400, 350));
        GUILayout.Label("=== F1 Braking System Debug ===");
        GUILayout.Label($"Brake Input: {brakeInput:F2} ({brakeInput * 100f:F0}%)");
        GUILayout.Label($"Vehicle Speed: {vehicleSpeed * 3.6f:F1} km/h");
        GUILayout.Label($"Brake Balance: {frontBrakes.brakeBalance:F2} ({frontBrakes.brakeBalance * 100f:F0}% front)");
        GUILayout.Label($"ABS: {(absSettings.absEnabled ? "ON" : "OFF")} {(absSystemActive ? "(ACTIVE)" : "")}");
        GUILayout.Label($"Surface: {(lockupSettings.isWetSurface ? "WET" : "DRY")}");
        GUILayout.Label($"Any Wheel Locked: {(anyWheelLocked ? "YES" : "NO")}");
        
        if (allBrakes != null)
        {
            string[] brakeNames = { "Front Left", "Front Right", "Rear Left", "Rear Right" };
            for (int i = 0; i < allBrakes.Length; i++)
            {
                var brake = allBrakes[i];
                string lockStatus = brake.isLocked ? "LOCKED" : (brake.absActive ? "ABS" : "OK");
                
                GUILayout.Label($"{brakeNames[i]} [{lockStatus}]:");
                if (showTemperatureDebug)
                {
                    GUILayout.Label($"  Temp: {brake.currentTemperature:F0}°C");
                    GUILayout.Label($"  Force: {brake.brakeForce:F0}N");
                    GUILayout.Label($"  Effectiveness: {brake.effectiveness:F2}");
                }
                if (showLockupDebug)
                {
                    GUILayout.Label($"  Slip: {brake.slipRatio:F3}");
                    GUILayout.Label($"  Lockup: {brake.lockupIntensity:F2}");
                    GUILayout.Label($"  Flat Spot: {brake.flatSpotDamage:F3}");
                    GUILayout.Label($"  ABS Mult: {brake.absBrakeMultiplier:F2}");
                }
            }
        }
        
        GUILayout.Label("\nControls:");
        GUILayout.Label($"Brake: {brakeKey} / {brakeInputAxis}");
        GUILayout.Label($"Toggle ABS: {absToggleKey}");
        
        GUILayout.EndArea();
    }
}
