using UnityEngine;

[System.Serializable]
public class SuspensionSettings
{
    [Header("Spring Settings")]
    public float springConstant = 250f; // N/mm (converted to N/m in code)
    public float restLength = 0.3f; // meters
    public float maxCompression = 0.15f; // meters
    public float maxExtension = 0.1f; // meters
    
    [Header("Damper Settings")]
    public float dampingCoefficient = 5000f; // Ns/m
    public float bumpMultiplier = 1.0f; // compression damping multiplier
    public float reboundMultiplier = 1.2f; // extension damping multiplier
    
    [Header("Anti-Roll Bar")]
    public float antiRollBarStiffness = 100f; // Nm/degree
    public bool useAntiRollBar = true;
}

[System.Serializable]
public class WheelData
{
    public Transform wheelTransform;
    public Transform suspensionAnchor; // Upper attachment point
    public WheelCollider wheelCollider;
    public float currentCompression;
    public float compressionVelocity;
    public float lastCompression;
    public Vector3 suspensionForce;
    public float wheelVerticalDisplacement;
}

public class F1SuspensionSystem : MonoBehaviour
{
    [Header("Suspension Configuration")]
    public SuspensionSettings frontSuspension;
    public SuspensionSettings rearSuspension;
    
    [Header("Wheel References")]
    public WheelData frontLeft;
    public WheelData frontRight;
    public WheelData rearLeft;
    public WheelData rearRight;
    
    [Header("Vehicle Properties")]
    public Rigidbody vehicleRigidbody;
    public float frontTrackWidth = 2.0f; // meters
    public float rearTrackWidth = 1.4f; // meters
    public float wheelbase = 3.5f; // meters
    
    [Header("Debug Visualization")]
    public bool showSuspensionForces = true;
    public bool showCompressionDebug = true;
    public float forceVisualizationScale = 0.001f;
    
    private WheelData[] allWheels;
    private float lastFixedUpdateTime;
    
    void Start()
    {
        // Initialize wheel array for easier iteration
        allWheels = new WheelData[] { frontLeft, frontRight, rearLeft, rearRight };
        
        // Validate references
        ValidateReferences();
        
        // Initialize wheel data
        InitializeWheelData();
        
        lastFixedUpdateTime = Time.fixedTime;
    }
    
    void ValidateReferences()
    {
        if (vehicleRigidbody == null)
            vehicleRigidbody = GetComponent<Rigidbody>();
            
        foreach (var wheel in allWheels)
        {
            if (wheel.wheelCollider == null && wheel.wheelTransform != null)
                wheel.wheelCollider = wheel.wheelTransform.GetComponent<WheelCollider>();
        }
    }
    
    void InitializeWheelData()
    {
        foreach (var wheel in allWheels)
        {
            if (wheel.wheelCollider != null)
            {
                wheel.currentCompression = 0f;
                wheel.lastCompression = 0f;
                wheel.compressionVelocity = 0f;
            }
        }
    }
    
    void FixedUpdate()
    {
        float deltaTime = Time.fixedTime - lastFixedUpdateTime;
        lastFixedUpdateTime = Time.fixedTime;
        
        // Update each wheel's suspension
        UpdateWheelSuspension(frontLeft, frontSuspension, deltaTime);
        UpdateWheelSuspension(frontRight, frontSuspension, deltaTime);
        UpdateWheelSuspension(rearLeft, rearSuspension, deltaTime);
        UpdateWheelSuspension(rearRight, rearSuspension, deltaTime);
        
        // Apply anti-roll bar forces
        if (frontSuspension.useAntiRollBar)
            ApplyAntiRollBar(frontLeft, frontRight, frontSuspension, frontTrackWidth);
            
        if (rearSuspension.useAntiRollBar)
            ApplyAntiRollBar(rearLeft, rearRight, rearSuspension, rearTrackWidth);
        
        // Apply all suspension forces to the vehicle
        ApplySuspensionForces();
    }
    
    void UpdateWheelSuspension(WheelData wheel, SuspensionSettings settings, float deltaTime)
    {
        if (wheel.wheelCollider == null) return;
        
        WheelHit hit;
        bool isGrounded = wheel.wheelCollider.GetGroundHit(out hit);
        
        if (isGrounded)
        {
            // Calculate compression based on wheel collider
            float maxDistance = settings.restLength + settings.maxExtension;
            wheel.currentCompression = Mathf.Clamp(maxDistance - hit.distance, 
                -settings.maxExtension, settings.maxCompression);
        }
        else
        {
            // Wheel is in the air, apply maximum extension
            wheel.currentCompression = -settings.maxExtension;
        }
        
        // Calculate compression velocity
        if (deltaTime > 0)
        {
            wheel.compressionVelocity = (wheel.currentCompression - wheel.lastCompression) / deltaTime;
        }
        
        // Calculate spring force: F_spring = -k * (x - x_rest)
        float springForce = CalculateSpringForce(wheel.currentCompression, settings);
        
        // Calculate damper force: F_damper = -c * v_relative
        float damperForce = CalculateDamperForce(wheel.compressionVelocity, settings);
        
        // Total suspension force
        float totalForce = springForce + damperForce;
        
        // Store the force vector (always vertical in world space)
        wheel.suspensionForce = Vector3.up * totalForce;
        
        // Update wheel vertical displacement for anti-roll calculation
        wheel.wheelVerticalDisplacement = wheel.currentCompression;
        
        // Store last compression for next frame
        wheel.lastCompression = wheel.currentCompression;
    }
    
    float CalculateSpringForce(float compression, SuspensionSettings settings)
    {
        // F_spring = -k * (x - x_rest)
        // Compression is positive when spring is compressed
        float springConstantNm = settings.springConstant * 1000f; // Convert N/mm to N/m
        return springConstantNm * compression;
    }
    
    float CalculateDamperForce(float compressionVelocity, SuspensionSettings settings)
    {
        // F_damper = -c * v_relative
        // Apply different multipliers for bump (compression) and rebound (extension)
        float multiplier = compressionVelocity > 0 ? settings.bumpMultiplier : settings.reboundMultiplier;
        return -settings.dampingCoefficient * compressionVelocity * multiplier;
    }
    
    void ApplyAntiRollBar(WheelData leftWheel, WheelData rightWheel, 
        SuspensionSettings settings, float trackWidth)
    {
        if (!settings.useAntiRollBar) return;
        
        // Calculate roll angle difference
        // θ represents the vertical displacement difference between wheels
        float displacementDifference = leftWheel.wheelVerticalDisplacement - rightWheel.wheelVerticalDisplacement;
        
        // Convert displacement difference to angle (small angle approximation)
        float rollAngle = Mathf.Atan2(displacementDifference, trackWidth) * Mathf.Rad2Deg;
        
        // F_arb = k_arb * (θ_left - θ_right)
        float antiRollForce = settings.antiRollBarStiffness * rollAngle;
        
        // Apply opposing forces to each wheel
        Vector3 arbForceLeft = Vector3.up * antiRollForce;
        Vector3 arbForceRight = Vector3.up * (-antiRollForce);
        
        // Add to existing suspension forces
        leftWheel.suspensionForce += arbForceLeft;
        rightWheel.suspensionForce += arbForceRight;
    }
    
    void ApplySuspensionForces()
    {
        if (vehicleRigidbody == null) return;
        
        // Apply forces at each wheel position
        foreach (var wheel in allWheels)
        {
            if (wheel.wheelTransform != null && wheel.suspensionForce.magnitude > 0.1f)
            {
                Vector3 forcePosition = wheel.wheelTransform.position;
                vehicleRigidbody.AddForceAtPosition(wheel.suspensionForce, forcePosition);
            }
        }
    }
    
    // Public methods for accessing suspension data
    public float GetWheelCompression(int wheelIndex)
    {
        if (wheelIndex >= 0 && wheelIndex < allWheels.Length)
            return allWheels[wheelIndex].currentCompression;
        return 0f;
    }
    
    public Vector3 GetWheelSuspensionForce(int wheelIndex)
    {
        if (wheelIndex >= 0 && wheelIndex < allWheels.Length)
            return allWheels[wheelIndex].suspensionForce;
        return Vector3.zero;
    }
    
    public void SetSuspensionSettings(bool isFront, SuspensionSettings newSettings)
    {
        if (isFront)
            frontSuspension = newSettings;
        else
            rearSuspension = newSettings;
    }
    
    // Debug visualization
    void OnDrawGizmos()
    {
        if (!showSuspensionForces && !showCompressionDebug) return;
        
        if (allWheels != null)
        {
            foreach (var wheel in allWheels)
            {
                if (wheel.wheelTransform == null) continue;
                
                Vector3 wheelPos = wheel.wheelTransform.position;
                
                if (showSuspensionForces)
                {
                    // Draw suspension force vector
                    Gizmos.color = Color.green;
                    Vector3 forceEnd = wheelPos + wheel.suspensionForce * forceVisualizationScale;
                    Gizmos.DrawLine(wheelPos, forceEnd);
                    Gizmos.DrawWireSphere(forceEnd, 0.05f);
                }
                
                if (showCompressionDebug)
                {
                    // Draw compression state
                    float compressionRatio = wheel.currentCompression / 0.15f; // Normalize to typical max compression
                    Gizmos.color = Color.Lerp(Color.blue, Color.red, compressionRatio + 0.5f);
                    Gizmos.DrawWireCube(wheelPos + Vector3.up * 0.2f, Vector3.one * 0.1f);
                    
                    // Draw rest position
                    Gizmos.color = Color.yellow;
                    SuspensionSettings settings = IsWheelFront(wheel) ? frontSuspension : rearSuspension;
                    Vector3 restPos = wheelPos + Vector3.up * settings.restLength;
                    Gizmos.DrawWireSphere(restPos, 0.03f);
                }
            }
        }
    }
    
    bool IsWheelFront(WheelData wheel)
    {
        return wheel == frontLeft || wheel == frontRight;
    }
    
    void OnGUI()
    {
        if (!showCompressionDebug) return;
        
        GUILayout.BeginArea(new Rect(10, 50, 300, 200));
        GUILayout.Label("=== F1 Suspension Debug ===");
        
        if (allWheels != null)
        {
            string[] wheelNames = { "Front Left", "Front Right", "Rear Left", "Rear Right" };
            for (int i = 0; i < allWheels.Length; i++)
            {
                var wheel = allWheels[i];
                GUILayout.Label($"{wheelNames[i]}:");
                GUILayout.Label($"  Compression: {wheel.currentCompression:F3}m");
                GUILayout.Label($"  Velocity: {wheel.compressionVelocity:F2}m/s");
                GUILayout.Label($"  Force: {wheel.suspensionForce.y:F0}N");
            }
        }
        
        GUILayout.EndArea();
    }
}
