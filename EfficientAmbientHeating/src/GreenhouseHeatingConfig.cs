namespace EfficientAmbientHeating;

public class GreenhouseHeatingConfig
{
    /// <summary>
    /// How many times longer fuel lasts when burning idle inside an enclosed room.
    /// 2.0 = fuel lasts twice as long. Must be > 1.0 to have any effect.
    /// </summary>
    public float BurnTimeMultiplier { get; set; } = 2.0f;
}
