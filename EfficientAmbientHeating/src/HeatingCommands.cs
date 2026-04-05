using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace EfficientAmbientHeating;

public class HeatingCommands
{
    private readonly ICoreServerAPI api;
    private readonly EfficientHeatingMod mod;

    public HeatingCommands(ICoreServerAPI api, EfficientHeatingMod mod)
    {
        this.api = api;
        this.mod = mod;
    }

    public void Register()
    {
        var cmd = api.ChatCommands
            .Create("heating")
            .WithDescription("Efficient Ambient Heating admin commands")
            .RequiresPrivilege(Privilege.controlserver);

        cmd.BeginSubCommand("mult")
            .WithDescription("Get or set the fuel burn-time multiplier. Usage: /heating mult [value]")
            .WithArgs(api.ChatCommands.Parsers.OptionalFloat("value"))
            .HandleWith(OnMultCommand)
            .EndSubCommand();

        cmd.BeginSubCommand("status")
            .WithDescription("Check whether your current position is inside a valid enclosed room")
            .HandleWith(OnStatusCommand)
            .EndSubCommand();
    }

    private TextCommandResult OnMultCommand(TextCommandCallingArgs args)
    {
        float value = (float)(args[0] ?? 0f);

        if (value > 0f)
        {
            mod.Config.BurnTimeMultiplier = value;
            mod.SaveConfig();
            return TextCommandResult.Success($"Burn-time multiplier set to {value:F2}x.");
        }

        return TextCommandResult.Success(
            $"Current burn-time multiplier: {mod.Config.BurnTimeMultiplier:F2}x  " +
            $"(fuel lasts {mod.Config.BurnTimeMultiplier:F2}x longer when idle in an enclosed room)");
    }

    private TextCommandResult OnStatusCommand(TextCommandCallingArgs args)
    {
        if (args.Caller.Player is not IServerPlayer player)
            return TextCommandResult.Error("This command must be run by a player.");

        var pos = player.Entity.Pos.AsBlockPos;
        var roomRegistry = api.ModLoader.GetModSystem<RoomRegistry>();

        if (roomRegistry == null)
            return TextCommandResult.Success("Room registry is not available.");

        var room = roomRegistry.GetRoomForPosition(pos);

        if (room == null)
            return TextCommandResult.Success("You are not inside any recognised room.");

        if (room.ExitCount > 0)
            return TextCommandResult.Success(
                $"Room detected, but it has {room.ExitCount} open exit(s). " +
                "Seal it completely to activate the efficiency bonus.");

        return TextCommandResult.Success(
            $"You are inside a fully enclosed room. " +
            $"Idle heat sources here burn fuel {mod.Config.BurnTimeMultiplier:F2}x slower.");
    }
}
