using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace EfficientAmbientHeating;

/// <summary>
/// BlockBehavior attached to crop blocks. When the crop is inside a fully enclosed room
/// that contains an active heater, the tooltip shows "+10°C Heated Greenhouse" to indicate
/// that both the greenhouse effect and an idle heat source are present.
/// </summary>
public class BehaviorHeatedGreenhouseInfo : BlockBehavior
{
    // Maximum distance (blocks) from crop to heater for the "heated" indicator to show.
    private const float MaxHeaterDistance = 24f;

    public BehaviorHeatedGreenhouseInfo(Block block) : base(block) { }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        if (world.Side != EnumAppSide.Server)
            return base.GetPlacedBlockInfo(world, pos, forPlayer);

        var sapi = (ICoreServerAPI)world.Api;
        var roomRegistry = sapi.ModLoader.GetModSystem<RoomRegistry>();
        var mod          = sapi.ModLoader.GetModSystem<EfficientHeatingMod>();

        if (roomRegistry == null || mod == null || mod.ActiveHeaterPositions.Count == 0)
            return base.GetPlacedBlockInfo(world, pos, forPlayer);

        // Crop must itself be inside an enclosed room.
        var room = roomRegistry.GetRoomForPosition(pos);
        if (room == null || room.ExitCount > 0)
            return base.GetPlacedBlockInfo(world, pos, forPlayer);

        // Check if any currently-active heater is within range.
        foreach (var heaterPos in mod.ActiveHeaterPositions)
        {
            if (pos.DistanceTo(heaterPos) <= MaxHeaterDistance)
                return "+10°C Heated Greenhouse";
        }

        return base.GetPlacedBlockInfo(world, pos, forPlayer);
    }
}
