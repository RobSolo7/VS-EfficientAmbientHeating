using System;
using System.Reflection;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace EfficientAmbientHeating;

/// <summary>
/// Attach this behavior to any block entity that burns fuel (firepit, stoves, etc.).
/// When the heater is idle inside an enclosed room, the fuel burn rate is reduced
/// by the configured multiplier. Returns to normal burn speed during cooking or smelting.
/// </summary>
public class BehaviorEfficientHeating : BlockEntityBehavior
{
    private ICoreServerAPI? sapi;
    private long tickListenerId;

    // Cached reflection references resolved once at Initialize time.
    // Using reflection keeps the behavior generic so it works on vanilla and modded heaters.
    private FieldInfo? fuelBurnTimeField;
    private FieldInfo? maxFuelBurnTimeField;

    // Tracks whether this heater is currently providing the efficiency bonus,
    // so the plant display behavior can detect it without re-scanning each frame.
    private bool isProvidingBonus;

    public BehaviorEfficientHeating(BlockEntity blockentity) : base(blockentity) { }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        if (api.Side != EnumAppSide.Server) return;

        sapi = (ICoreServerAPI)api;

        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        fuelBurnTimeField    = Blockentity.GetType().GetField("fuelBurnTime",    flags);
        maxFuelBurnTimeField = Blockentity.GetType().GetField("maxFuelBurnTime", flags);

        if (fuelBurnTimeField == null)
        {
            sapi.Logger.Warning("[EfficientHeating] Could not find 'fuelBurnTime' field on {0}. " +
                "Efficiency bonus will not apply to this block.", Blockentity.GetType().Name);
            return;
        }

        tickListenerId = api.Event.RegisterGameTickListener(OnServerTick, 1000);
    }

    private void OnServerTick(float dt)
    {
        if (fuelBurnTimeField == null || sapi == null) return;

        float burnTime = (float)(fuelBurnTimeField.GetValue(Blockentity) ?? 0f);
        bool burning      = burnTime > 0f;
        bool inRoom       = burning && IsInValidRoom();
        bool cooking      = inRoom  && IsActivelyUsed();

        var mod = sapi.ModLoader.GetModSystem<EfficientHeatingMod>();
        float multiplier  = mod?.Config?.BurnTimeMultiplier ?? 2f;

        bool shouldProvide = burning && inRoom && !cooking && multiplier > 1f;

        // Update the module-level registry so plant blocks can read it.
        if (mod != null)
        {
            if (shouldProvide)
                mod.ActiveHeaterPositions.Add(Blockentity.Pos);
            else
                mod.ActiveHeaterPositions.Remove(Blockentity.Pos);
        }

        isProvidingBonus = shouldProvide;

        if (!shouldProvide) return;

        float refund = (dt / 1000f) * (1f - 1f / multiplier);
        float newBurnTime = burnTime + refund;

        if (maxFuelBurnTimeField != null)
        {
            float maxBurnTime = (float)(maxFuelBurnTimeField.GetValue(Blockentity) ?? 0f);
            if (maxBurnTime > 0f)
                newBurnTime = Math.Min(newBurnTime, maxBurnTime);
        }

        fuelBurnTimeField.SetValue(Blockentity, newBurnTime);
    }

    /// <summary>
    /// Shows live debug state when the player hovers over the heater.
    /// </summary>
    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        if (sapi == null) return;

        float burnTime = (float)(fuelBurnTimeField?.GetValue(Blockentity) ?? 0f);
        bool burning   = burnTime > 0f;
        bool inRoom    = burning && IsInValidRoom();
        bool cooking   = inRoom  && IsActivelyUsed();

        var mod = sapi.ModLoader.GetModSystem<EfficientHeatingMod>();
        float mult = mod?.Config?.BurnTimeMultiplier ?? 2f;

        dsc.AppendLine();
        dsc.AppendLine($"[EfficientHeating on {Blockentity.GetType().Name}]");
        dsc.AppendLine($"  Room check pos: {Blockentity.Pos.UpCopy()}");
        if (!burning)
        {
            dsc.AppendLine("  Status: not burning");
        }
        else if (!inRoom)
        {
            dsc.AppendLine("  Status: no enclosed room at pos above — no bonus");
        }
        else if (cooking)
        {
            dsc.AppendLine("  Status: paused (cooking / smelting in progress)");
        }
        else
        {
            dsc.AppendLine($"  Status: ACTIVE — fuel lasts {mult:F2}x longer (~{1f/mult:P0} consumption)");
        }
    }

    private bool IsInValidRoom()
    {
        var roomRegistry = sapi!.ModLoader.GetModSystem<RoomRegistry>();
        if (roomRegistry == null) return false;

        // The heater is a solid block — the room registry tracks air spaces.
        // Check the block directly above the heater (the air the flame occupies).
        var checkPos = Blockentity.Pos.UpCopy();
        var room = roomRegistry.GetRoomForPosition(checkPos);
        return room != null && room.ExitCount == 0;
    }

    private bool IsActivelyUsed()
    {
        if (Blockentity is not IBlockEntityContainer container) return false;
        var inv = container.Inventory;
        if (inv == null) return false;

        foreach (var slot in inv)
        {
            if (slot.Empty) continue;
            float burnDuration = slot.Itemstack?.Collectible?.CombustibleProps?.BurnDuration ?? 0f;
            if (burnDuration <= 0f) return true;
        }

        return false;
    }

    private void Cleanup()
    {
        if (sapi == null) return;
        sapi.Event.UnregisterGameTickListener(tickListenerId);
        var mod = sapi.ModLoader.GetModSystem<EfficientHeatingMod>();
        mod?.ActiveHeaterPositions.Remove(Blockentity.Pos);
    }

    public override void OnBlockUnloaded()  { base.OnBlockUnloaded();  Cleanup(); }
    public override void OnBlockRemoved()   { base.OnBlockRemoved();   Cleanup(); }
}
