using Lotus.API.Odyssey;
using Lotus.Roles.Events;
using LotusBloom.Roles.Standard.Cult.CultRoles;
using Lotus.API;
using VentLib.Utilities;
using Lotus;

namespace LotusBloom.Roles.Standard.Cult.Events;

public class InitiateEvent : TargetedAbilityEvent
{
    public InitiateEvent(PlayerControl source, PlayerControl target, bool successful = true) : base(source, target, successful)
    {
    }

    public override string Message() => $"{CultRole.CultColor.Colorize(Game.GetName(Player()))} initiated {ModConstants.HColor2.Colorize(Game.GetName(Target()))} within the Cult.";
}