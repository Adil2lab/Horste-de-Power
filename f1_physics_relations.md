# F1 Physics System Component Relations

## Core Data Flow Architecture

```
EngineSystem → WheelSystem → Vehicle Physics
     ↓              ↓              ↓
   Power         Forces        Motion
```

## Primary Dependencies

### EngineSystem → WheelSystem
- **Angular Velocity Transfer**: `wheelRPM = currentRPM / totalRatio`
- **Torque Distribution**: `wheelTorque = (engineTorque + ersTorque) * gearRatio`
- **Slip Calculation**: `slipRatio = (wheelSpeed - vehicleSpeed) / max(speeds)`

### WheelSystem → EngineSystem  
- **RPM Feedback**: Engine RPM calculated from wheel speed when not spinning
- **Traction Limiting**: `effectiveWheelTorque = min(wheelTorque, tractionLimit)`
- **Load Transfer**: Normal force affects tire grip affects engine effectiveness

## Critical Coupling Points

### 1. Slip Ratio Synchronization
```csharp
// EngineSystem calculates from drivetrain
slipRatio = (drivenWheelRPM - actualWheelRPM) / max(RPMs)

// WheelSystem calculates from tire physics  
slipRatio = (wheelSpeed - vehicleSpeed) / max(speeds)
```

### 2. Traction Control Integration
```csharp
// EngineSystem applies TC
engineTorque *= (1 - tcAggressiveness * wheelSpinRatio)

// WheelSystem provides feedback
isWheelSpinning = wheelSpinRatio > slipThreshold
```

### 3. Temperature Cross-Effects
```csharp
// Tire heat affects grip
gripMultiplier = exp(-0.5 * ((temp - optimal) / window)²)

// Engine load affects tire heating via slip
heatGeneration = frictionRate * |slipRatio| * normalForce * velocity
```

## Data Exchange Requirements

### EngineSystem Inputs from WheelSystem
- `wheelSpinRatio` → Traction control
- `isWheelSpinning` → RPM calculation mode
- `availableTraction` → Torque limiting
- `effectiveGrip` → Power delivery adjustment

### WheelSystem Inputs from EngineSystem
- `angularVelocity` → Slip ratio calculation
- `wheelTorque` → Force generation
- `normalForce` → Grip scaling
- Vehicle `Rigidbody.velocity` → Slip angle calculation

## Mathematical Relationships

### Power Flow Chain
```
ICE Power → Gearbox → Wheels → Tires → Ground
P_ice = torque * rpm / 9549
P_wheel = P_ice * η_transmission
F_traction = min(P_wheel/v, μ * F_normal)
```

### Force Feedback Loop
```
Engine Torque → Wheel Forces → Vehicle Motion → Wheel Speed → Engine RPM
```

### Temperature Dynamics
```
Slip Energy → Tire Heat → Grip Reduction → More Slip → More Heat
Q = k * |slip| * F_normal * velocity
T_new = T + (Q_gen - Q_loss) * dt
μ_eff = μ_peak * temp_multiplier(T)
```

## Integration Architecture

### Shared Physics State
```csharp
public class SharedPhysicsState
{
    public float vehicleSpeed;
    public float normalForce;
    public Vector3 velocity;
    public float wheelSpinRatio;
    public float surfaceGrip;
}
```

### Update Sequence
1. **WheelSystem.UpdateWheelPhysics()** → Calculate current wheel state
2. **EngineSystem.UpdateWheelDynamics()** → Get wheel spin feedback  
3. **EngineSystem.CalculateICEPower()** → Generate base torque
4. **EngineSystem.ApplyTractionControl()** → Limit based on wheel state
5. **WheelSystem.CalculateTireForces()** → Apply limited torque to tires
6. **Both.ApplyForces()** → Update Rigidbody

## Critical Synchronization Points

### Frame-Perfect Updates
- Both systems must use same `Time.fixedDeltaTime`
- Wheel angular velocity must match engine output within tolerance
- Normal force distribution affects both tire grip and engine load

### State Consistency
- `slipRatio` calculations must use identical vehicle speed reference
- `normalForce` must account for aerodynamic downforce and weight transfer
- Temperature and wear states affect both power delivery and grip

## Performance Optimization Targets

### Computational Dependencies
- Engine torque calculation: O(1) polynomial evaluation
- Pacejka tire model: O(1) transcendental functions
- Temperature updates: O(1) differential equations
- Wear progression: O(1) multiplicative factors

### Memory Sharing
- Single `Rigidbody` reference for velocity queries
- Shared surface condition parameters
- Common time step for numerical integration

## Validation Checkpoints

### Physical Consistency
- Energy conservation: `P_in ≈ P_out + P_losses`
- Force equilibrium: `∑F_tires = ma_vehicle`
- Angular momentum: `τ_engine = I_wheel * α_wheel + τ_resistance`

### Behavioral Boundaries  
- RPM limits: `idle_rpm ≤ current_rpm ≤ max_rpm`
- Slip constraints: `0 ≤ slip_ratio ≤ practical_limit`
- Temperature bounds: `ambient ≤ tire_temp ≤ degradation_limit`