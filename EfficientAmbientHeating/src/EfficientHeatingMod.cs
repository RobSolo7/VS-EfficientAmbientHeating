using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace EfficientAmbientHeating;

public class EfficientHeatingMod : ModSystem
{
    private const string ConfigFilename = "GreenhouseHeatingConfig.json";

    // Entity classes that represent burnable heaters in vanilla VS.
    // Any modded block using these same entity classes gets the bonus automatically.
    private static readonly string[] HeaterEntityClasses = ["Firepit", "Stove"];

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

    /// <summary>
    /// After all assets (vanilla + modded) are fully loaded, programmatically attach
    /// EfficientHeating to every block whose entity class is a known heater type.
    /// This replaces JSON patching and works automatically with modded heaters that
    /// reuse vanilla entity classes.
    /// </summary>
    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server) return;

        int count = 0;
        foreach (var block in api.World.Blocks)
        {
            if (block?.EntityClass == null) continue;
            if (!HeaterEntityClasses.Contains(block.EntityClass)) continue;

            // Guard against duplicate registration (e.g. if a JSON patch also applied it).
            var existing = block.BlockEntityBehaviors ?? [];
            if (existing.Any(b => b.Name == "EfficientHeating")) continue;

            block.BlockEntityBehaviors = [.. existing, new BlockEntityBehaviorType { Name = "EfficientHeating" }];
            count++;
        }

        Mod.Logger.Notification($"[EfficientHeating] Attached behavior to {count} block variant(s).");
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

        api.StoreModConfig(Config, ConfigFilename);
    }

    public void SaveConfig()
    {
        sapi?.StoreModConfig(Config, ConfigFilename);
    }
}
