namespace EfficientAmbientHeating;

public class GreenhouseHeatingConfig
{
    /// <summary>
    /// Fuel is consumed at (1 / BurnTimeMultiplier) of the normal rate when the heater
    /// is idle inside an enclosed room. Default 3.33 ≈ 0.3x consumption (fuel lasts ~3.33x longer).
    /// Must be > 1.0 to have any effect.
    /// </summary>
    public float BurnTimeMultiplier { get; set; } = 3.33f;
}
