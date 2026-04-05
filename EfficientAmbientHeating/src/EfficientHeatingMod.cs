using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace EfficientAmbientHeating;

public class EfficientHeatingMod : ModSystem
{
    private const string ConfigFilename = "GreenhouseHeatingConfig.json";

    // Entity classes that represent burnable heaters in vanilla VS.
    // Any modded block using these same entity classes gets the bonus automatically.
    private static readonly string[] HeaterEntityClasses = ["Firepit", "Stove"];

    public GreenhouseHeatingConfig Config { get; private set; } = new();

    /// <summary>
    /// Set of block positions whose EfficientHeating behavior is currently
    /// providing a fuel efficiency bonus. Updated each second by the behavior tick.
    /// Read by BehaviorHeatedGreenhouseInfo to drive the crop tooltip.
    /// </summary>
    public HashSet<BlockPos> ActiveHeaterPositions { get; } = [];

    private ICoreServerAPI? sapi;

    public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        LoadConfig(api);
        api.RegisterBlockEntityBehaviorClass("EfficientHeating",       typeof(BehaviorEfficientHeating));
        api.RegisterBlockBehaviorClass    ("HeatedGreenhouseInfo",     typeof(BehaviorHeatedGreenhouseInfo));
        new HeatingCommands(api, this).Register();
    }

    /// <summary>
    /// After all assets are loaded, programmatically attach behaviors to matching blocks.
    /// This avoids the JSON-patch cross-domain resolution problem and automatically
    /// covers modded blocks that reuse vanilla entity/block classes.
    /// </summary>
    public override void AssetsLoaded(ICoreAPI api)
    {
        if (api.Side != EnumAppSide.Server) return;

        int heaterCount = 0;
        int cropCount   = 0;

        foreach (var block in api.World.Blocks)
        {
            if (block?.Code == null) continue;

            // --- Heaters: match by block entity class ---
            if (block.EntityClass != null && HeaterEntityClasses.Contains(block.EntityClass))
            {
                var existing = block.BlockEntityBehaviors ?? [];
                if (!existing.Any(b => b.Name == "EfficientHeating"))
                {
                    block.BlockEntityBehaviors = [.. existing, new BlockEntityBehaviorType { Name = "EfficientHeating" }];
                    heaterCount++;
                }
            }

            // --- Crops: match by block class name (BlockCrop covers all farmland crops) ---
            if (block.GetType().Name == "BlockCrop")
            {
                var existing = block.BlockBehaviors ?? [];
                if (!existing.Any(b => b.GetType().Name == "BehaviorHeatedGreenhouseInfo"))
                {
                    var newBehavior = new BehaviorHeatedGreenhouseInfo(block);
                    block.BlockBehaviors = [.. existing, newBehavior];
                    cropCount++;
                }
            }
        }

        Mod.Logger.Notification(
            $"[EfficientHeating] Attached heater behavior to {heaterCount} block variant(s), " +
            $"crop display to {cropCount} crop variant(s).");
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
