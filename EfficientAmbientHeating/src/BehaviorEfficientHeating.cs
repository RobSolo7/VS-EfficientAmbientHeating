using System;
using System.Reflection;
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

    public BehaviorEfficientHeating(BlockEntity blockentity) : base(blockentity) { }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);

        if (api.Side != EnumAppSide.Server) return;

        sapi = (ICoreServerAPI)api;

        // Resolve fuelBurnTime field on the concrete block entity type.
        // Checks both public and non-public fields so it works across VS versions.
        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        fuelBurnTimeField    = Blockentity.GetType().GetField("fuelBurnTime",    flags);
        maxFuelBurnTimeField = Blockentity.GetType().GetField("maxFuelBurnTime", flags);

        if (fuelBurnTimeField == null)
        {
            sapi.Logger.Warning("[EfficientHeating] Could not find 'fuelBurnTime' field on {0}. " +
                "Efficiency bonus will not apply to this block.", Blockentity.GetType().Name);
            return;
        }

        // Tick every second. dt will be elapsed ms since last call.
        tickListenerId = api.Event.RegisterGameTickListener(OnServerTick, 1000);
    }

    private void OnServerTick(float dt)
    {
        if (fuelBurnTimeField == null || sapi == null) return;

        float burnTime = (float)(fuelBurnTimeField.GetValue(Blockentity) ?? 0f);
        if (burnTime <= 0f) return;       // Not burning — nothing to slow down.

        if (!IsInValidRoom()) return;     // No enclosure bonus.
        if (IsActivelyUsed()) return;     // Cooking or smelting — run at normal speed.

        var mod = sapi.ModLoader.GetModSystem<EfficientHeatingMod>();
        float multiplier = mod?.Config?.BurnTimeMultiplier ?? 2f;
        if (multiplier <= 1f) return;

        // Refund a fraction of the second that just elapsed.
        // At 2x multiplier: refund 0.5 s/s → net consumption = 0.5 s/s → fuel lasts 2x.
        // dt is in milliseconds; divide by 1000 to get seconds.
        float refund = (dt / 1000f) * (1f - 1f / multiplier);

        float newBurnTime = burnTime + refund;

        // Cap at the original capacity so we never "overcharge" a fuel piece.
        if (maxFuelBurnTimeField != null)
        {
            float maxBurnTime = (float)(maxFuelBurnTimeField.GetValue(Blockentity) ?? 0f);
            if (maxBurnTime > 0f)
                newBurnTime = Math.Min(newBurnTime, maxBurnTime);
        }

        fuelBurnTimeField.SetValue(Blockentity, newBurnTime);
    }

    /// <summary>
    /// Returns true when the block entity sits inside a fully enclosed RoomRegistry room.
    /// A single open block (exit) disables the bonus — Test Case C.
    /// </summary>
    private bool IsInValidRoom()
    {
        var roomRegistry = sapi!.ModLoader.GetModSystem<RoomRegistry>();
        if (roomRegistry == null) return false;

        var room = roomRegistry.GetRoomForPosition(Blockentity.Pos);
        return room != null && room.ExitCount == 0;
    }

    /// <summary>
    /// Returns true when a non-fuel item occupies any inventory slot, indicating
    /// that the heater is actively cooking or smelting — Test Case B.
    /// Checks via CombustibleProps: anything without a burn duration is "work", not fuel.
    /// </summary>
    private bool IsActivelyUsed()
    {
        if (Blockentity is not IBlockEntityContainer container) return false;
        var inv = container.Inventory;
        if (inv == null) return false;

        foreach (var slot in inv)
        {
            if (slot.Empty) continue;

            float burnDuration = slot.Itemstack?.Collectible?.CombustibleProps?.BurnDuration ?? 0f;
            if (burnDuration <= 0f)
                return true; // Non-fuel item found → heater is in use.
        }

        return false;
    }

    public override void OnBlockUnloaded()
    {
        base.OnBlockUnloaded();
        sapi?.Event.UnregisterGameTickListener(tickListenerId);
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();
        sapi?.Event.UnregisterGameTickListener(tickListenerId);
    }
}
