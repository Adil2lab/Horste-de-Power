# F1 Game Physics Equations

## Aerodynamics

### Downforce Calculation
```
F_downforce = 0.5 * ρ * v² * A * C_L
```
Where:
- ρ = air density (1.225 kg/m³ at sea level)
- v = velocity (m/s)
- A = reference area (m²)
- C_L = lift coefficient (negative for downforce)

### Drag Force
```
F_drag = 0.5 * ρ * v² * A * C_D
```
Where:
- C_D = drag coefficient

### Dirty Air Effect
```
downforce_reduction = base_downforce * (1 - wake_factor * proximity_factor)
wake_factor = 0.1 to 0.4 (depending on following distance)
proximity_factor = max(0, (wake_distance - current_distance) / wake_distance)
```

### Ground Effect
```
ground_effect_multiplier = 1 + (ground_effect_coefficient / (ride_height + minimum_height))
adjusted_downforce = base_downforce * ground_effect_multiplier
```

## Tire Physics

### Pacejka Tire Model (Magic Formula)
```
F_y = D * sin(C * arctan(B * α - E * (B * α - arctan(B * α))))
```
Where:
- F_y = lateral force
- α = slip angle (radians)
- B = stiffness factor
- C = shape factor
- D = peak factor
- E = curvature factor

### Tire Temperature Model
```
T_new = T_current + (heat_generation - heat_dissipation) * dt
heat_generation = k_friction * |slip_ratio| * normal_force * velocity
heat_dissipation = k_cooling * (T_current - T_ambient)
```

### Temperature-Grip Relationship
```
grip_multiplier = gaussian_curve(temperature, optimal_temp, temperature_window)
grip_multiplier = exp(-0.5 * ((T - T_optimal) / σ)²)
```

### Tire Wear
```
wear_rate = base_wear * slip_factor * temperature_factor * load_factor
slip_factor = 1 + k_slip * |slip_ratio|
temperature_factor = 1 + k_temp * max(0, T - T_optimal)
load_factor = (normal_force / reference_load)^n
```

## Energy Recovery System (ERS)

### MGU-K Energy Recovery
```
E_recovered = efficiency * 0.5 * I * (ω_initial² - ω_final²)
P_recovery = min(max_recovery_power, E_recovered / dt)
```
Where:
- I = rotational inertia
- ω = angular velocity
- efficiency = 0.7-0.9

### ERS Deployment
```
P_deploy = min(available_energy / dt, max_deploy_power, requested_power)
torque_boost = P_deploy / ω_engine
```

### Energy Limits
```
max_deployment_per_lap = 4 MJ (F1 regulation)
max_recovery_per_lap = 2 MJ (F1 regulation)
max_power = 120 kW (F1 regulation)
```

## Suspension Dynamics

### Spring Force
```
F_spring = -k * (x - x_rest)
```
Where:
- k = spring constant (N/m)
- x = current position
- x_rest = rest position

### Damper Force
```
F_damper = -c * v_relative
```
Where:
- c = damping coefficient
- v_relative = relative velocity between sprung/unsprung mass

### Anti-roll Bar
```
F_arb = k_arb * (θ_left - θ_right)
```
Where:
- k_arb = anti-roll bar stiffness
- θ = wheel vertical displacement angle difference

## Braking System

### Brake Force Distribution
```
F_brake_front = brake_input * max_brake_force * brake_balance
F_brake_rear = brake_input * max_brake_force * (1 - brake_balance)
```

### Brake Temperature
```
T_brake_new = T_brake + (Q_generated - Q_dissipated) / (m_brake * c_brake)
Q_generated = F_brake * v_wheel * efficiency_loss
Q_dissipated = h * A_brake * (T_brake - T_ambient)
```

### Brake Fade
```
brake_effectiveness = max(0.1, 1 - fade_factor * max(0, T_brake - T_fade_start))
```

## Power Unit

### ICE Power Curve
```
P_ice = torque_curve(rpm) * rpm / 9549
torque_curve(rpm) = a * rpm³ + b * rpm² + c * rpm + d
```

### Fuel Consumption
```
fuel_flow = base_consumption * throttle_position * engine_load_factor
fuel_remaining = initial_fuel - ∫fuel_flow dt
```

### Weight Effect
```
total_mass = dry_weight + fuel_mass + driver_mass
acceleration = F_net / total_mass
```

## Vehicle Dynamics

### Longitudinal Acceleration
```
F_longitudinal = F_traction - F_drag - F_rolling_resistance
F_traction = min(engine_force, μ_longitudinal * F_normal)
F_rolling_resistance = C_rr * F_normal
```

### Lateral Acceleration
```
F_lateral = m * v² / r
F_available = μ_lateral * (F_normal + F_downforce)
max_cornering_speed = √(μ_lateral * (F_normal + F_downforce) * r / m)
```

### Weight Transfer

#### Longitudinal Weight Transfer
```
ΔF_z = (a_x * m * h_cg) / wheelbase
F_z_front = static_front_load + ΔF_z
F_z_rear = static_rear_load - ΔF_z
```

#### Lateral Weight Transfer
```
ΔF_z = (a_y * m * h_cg) / track_width
F_z_outside = static_load + ΔF_z
F_z_inside = static_load - ΔF_z
```

## Setup Parameters

### Ride Height Effect on Downforce
```
downforce_coefficient = base_coefficient * (reference_height / current_height)^n
n = 1.5 to 2.5 (typical range)
```

### Wing Angle to Downforce
```
C_L = C_L0 + k_wing * wing_angle
C_D = C_D0 + k_drag * wing_angle²
```

### Gear Ratios
```
wheel_rpm = engine_rpm / (primary_ratio * gear_ratio * final_drive)
vehicle_speed = wheel_rpm * wheel_circumference / 60
```

## Coefficient Explanations

### Aerodynamic Coefficients
- **C_L (Lift Coefficient)**: Negative values (-2.5 to -4.0 for F1). Higher magnitude = more downforce
- **C_D (Drag Coefficient)**: Typically 0.7-1.1 for F1. Balance with downforce
- **ρ (Air Density)**: 1.225 kg/m³ standard. Decreases with altitude/temperature
- **A (Reference Area)**: Frontal area ~1.5-1.8 m² for F1 cars

### Tire Model Coefficients (Pacejka)
- **B (Stiffness Factor)**: 8-15. Higher = sharper grip buildup
- **C (Shape Factor)**: 1.3-1.7. Controls curve shape near peak
- **D (Peak Factor)**: Maximum tire force (~2000-4000N lateral)
- **E (Curvature Factor)**: -0.5 to 0.5. Controls post-peak behavior

### Tire Temperature Constants
- **k_friction**: Heat generation rate (0.001-0.01)
- **k_cooling**: Heat dissipation rate (0.1-0.5)
- **σ (Temperature Window)**: Grip falloff rate (~15-25°C)
- **T_optimal**: Peak grip temperature (90-110°C)

### Suspension Constants
- **k (Spring Rate)**: 150-350 N/mm F1 front, 200-450 N/mm rear
- **c (Damping)**: 3000-8000 Ns/m typical range
- **k_arb (ARB Stiffness)**: 50-200 Nm/deg

### Brake Constants
- **h (Heat Transfer)**: 10-50 W/(m²·K) convection coefficient
- **c_brake (Specific Heat)**: 500 J/(kg·K) carbon-carbon brakes
- **T_fade_start**: 800-900°C fade temperature
- **fade_factor**: 0.001-0.003 per °C

### Engine Constants
- **base_consumption**: 30-35 kg/hour at full load
- **efficiency_loss**: 0.65-0.75 (35-25% lost as heat)

## Typical F1 Values

### Physical Dimensions
- **Dry weight**: 740 kg (regulated minimum)
- **Fuel capacity**: 110 kg maximum
- **Wheelbase**: 3400-3700 mm
- **Track width**: 2000 mm front, 1400 mm rear (max)
- **Ride height**: 40-80 mm front, 60-120 mm rear

### Performance Parameters
- **Max downforce**: 1500-2000 N at 100 km/h
- **Tire contact patch**: 300 cm² per tire
- **Brake disc temp range**: 300-1000°C operating
- **Engine RPM**: 10,500-15,000 RPM (regulated)
- **ERS power**: 120 kW deployment, 2 MJ recovery limit

### Material Properties
- **Carbon fiber density**: 1600 kg/m³
- **Tire μ (dry)**: 1.6-2.2 peak
- **Tire μ (wet)**: 0.8-1.4 peak
- **Rolling resistance**: 0.008-0.015 coefficient
