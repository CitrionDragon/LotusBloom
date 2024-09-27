using Lotus.API.Odyssey;
using Lotus.Roles.Events;
using LotusBloom.Roles.Standard.Cult.CultRoles;
using Lotus;
using VentLib.Utilities;

namespace LotusBloom.Roles.Standard.Cult.Events;

public class ConvertEvent : TargetedAbilityEvent
{
    public ConvertEvent(PlayerControl source, PlayerControl target, bool successful = true) : base(source, target, successful)
    {
    }

    public override string Message() => $"{CultRole.CultColor.Colorize(Game.GetName(Player()))} turned {ModConstants.HColor2.Colorize(Game.GetName(Target()))} to the Cult.";
}
