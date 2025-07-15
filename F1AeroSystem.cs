using UnityEngine;

// Renamed the settings class for clarity
[System.Serializable]
public class F1AeroSettings
{
    [Header("Aerodynamic Properties")]
    [Tooltip("How much drag the car produces. Typical F1 values are 0.7 to 1.1.")]
    [Range(0.7f, 1.1f)]
    public float dragCoefficient = 0.9f; // C_D

    [Tooltip("How much downforce the car produces. Typical F1 values are -2.5 to -4.0.")]
    [Range(-4.0f, -2.5f)]
    public float liftCoefficient = -3.5f; // C_L (Negative for downforce)

    [Tooltip("The frontal area of the car in square meters.")]
    [Range(1.5f, 1.8f)]
    public float referenceArea = 1.65f; // A

    [Header("Environmental")]
    [Tooltip("Current air density in kg/m³. Standard sea level is ~1.225.")]
    public float airDensity = 1.225f;
}

public class F1AeroSystem : MonoBehaviour
{
    [SerializeField] private F1AeroSettings aeroSettings;
    
    // This value will be calculated once and reused
    private float adjustedAirDensity;

    void Start()
    {
        // For simplicity, we assume constant air density for now.
        // You can add the altitude/temperature calculation back here if needed.
        adjustedAirDensity = aeroSettings.airDensity;
    }

    /// <summary>
    /// Calculates the aerodynamic drag force based on the vehicle's current velocity.
    /// Formula: F_drag = 0.5 * ρ * v² * A * C_D
    /// </summary>
    /// <param name="velocityVector">The current velocity of the vehicle's Rigidbody.</param>
    /// <returns>The drag force vector, opposing velocity.</returns>
    public Vector3 GetDragForce(Vector3 velocityVector)
    {
        if (velocityVector.magnitude < 0.1f)
        {
            return Vector3.zero;
        }

        float speed = velocityVector.magnitude;
        Vector3 direction = velocityVector.normalized;

        float dragMagnitude = 0.5f * adjustedAirDensity * speed * speed * aeroSettings.referenceArea * aeroSettings.dragCoefficient;
        
        // Drag force acts in the opposite direction of velocity
        return -direction * dragMagnitude;
    }
    
    /// <summary>
    /// Calculates the aerodynamic downforce.
    /// Formula: F_downforce = 0.5 * ρ * v² * A * C_L
    /// </summary>
    /// <param name="velocityVector">The current velocity of the vehicle's Rigidbody.</param>
    /// <returns>The downforce vector, acting downwards on the car.</returns>
    public Vector3 GetDownforce(Vector3 velocityVector)
    {
        float speed = velocityVector.magnitude;

        // The lift coefficient is negative to produce downforce
        float downforceMagnitude = 0.5f * adjustedAirDensity * speed * speed * aeroSettings.referenceArea * aeroSettings.liftCoefficient;

        // Downforce acts downwards relative to the car's orientation
        return transform.up * downforceMagnitude;
    }
}