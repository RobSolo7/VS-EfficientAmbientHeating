using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace EfficientAmbientHeating;

public class EfficientHeatingMod : ModSystem
{
    private const string ConfigFilename = "GreenhouseHeatingConfig.json";

    public GreenhouseHeatingConfig Config { get; private set; } = new();

    private ICoreServerAPI? sapi;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        LoadConfig(api);
        api.RegisterBlockEntityBehaviorClass("EfficientHeating", typeof(BehaviorEfficientHeating));
        new HeatingCommands(api, this).Register();
    }

    private void LoadConfig(ICoreServerAPI api)
    {
        try
        {
            Config = api.LoadModConfig<GreenhouseHeatingConfig>(ConfigFilename) ?? new();
        }
        catch
        {
            Config = new();
        }

        // Write back to disk so the file is created on first run.
        api.StoreModConfig(Config, ConfigFilename);
    }

    public void SaveConfig()
    {
        sapi?.StoreModConfig(Config, ConfigFilename);
    }
}
