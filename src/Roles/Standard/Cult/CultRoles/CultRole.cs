using System.Collections.Generic;
using System.Linq;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.Factions.Interfaces;
using LotusBloom.Factions.Cult;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.GUI.Name.Interfaces;
using Lotus.Roles;
using Lotus.Roles.Internals.Enums;
using LotusBloom.Roles.Standard.Cult.Events;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Extensions;
using Lotus.Roles.Internals;
using UnityEngine;
using Lotus.API.Player;
using VentLib.Utilities;

namespace LotusBloom.Roles.Standard.Cult.CultRoles;

public class CultRole : Impostor
{
    //private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(CultRole));
    public static Color CultColor = new(0.8f, 0.36f, 0.8f);

    public override bool CanSabotage() => false;

    public void InitiateToCult(PlayerControl target)
    {
        CustomRole role = target.PrimaryRole();
        if (IsUnconvertedCult(target)) return; //already converted
        List<PlayerControl> viewers = Players.GetAlivePlayers().Where(IsConvertedCult).ToList();

        INameModel nameModel = target.NameModel();
        IndicatorComponent indicatorComponent = new(new LiveString("☆", CultColor), Game.InGameStates, ViewMode.Additive, () => viewers);

        role.Faction = new Cultist.Initiated(role.Faction, nameModel.GetComponentHolder<IndicatorHolder>().Add(indicatorComponent));
        role.SpecialType = SpecialType.Coven;
        Game.MatchData.GameHistory.AddEvent(new InitiateEvent(MyPlayer, target));
    }

    public void ConvertToCult(PlayerControl target)
    {
        CustomRole role = target.PrimaryRole();
        if (role.SpecialType != SpecialType.Coven) return;
        if (role.Faction is not Cultist.Initiated initiated) return;
        List<PlayerControl> cult = Players.GetAlivePlayers().Where(IsConvertedCult).ToList();

        initiated.Indicator.Delete();
        
        RoleHolder roleHolder = target.NameModel().GetComponentHolder<RoleHolder>();
        string newRoleName = CultColor.Colorize(role.RoleName);
        roleHolder.Add(new RoleComponent(new LiveString(newRoleName), Game.InGameStates, ViewMode.Replace, target));
        role.Faction = FactionInstances.GetExternalFaction(LotusBloom.FactionTypes["Cultist.Origin"]);

        cult.ForEach(p =>
        {
            roleHolder[0].AddViewer(p);
            roleHolder.Add(new RoleComponent(new LiveString(newRoleName), Game.InGameStates, ViewMode.Replace, p));
            p.NameModel().GetComponentHolder<RoleHolder>().Last().AddViewer(target);
        });
        Game.MatchData.GameHistory.AddEvent(new ConvertEvent(MyPlayer, target));
    }

    protected static bool IsUnconvertedCult(PlayerControl player) => player.PrimaryRole().Faction is Cultist.Initiated;
    protected static bool IsConvertedCult(PlayerControl player)
    {
        IFaction faction = player.PrimaryRole().Faction;
        if (faction is not Cultist) return false;
        return faction is not Cultist.Initiated;
    }

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .SpecialType(SpecialType.NeutralKilling)
            .Faction(FactionInstances.GetExternalFaction(LotusBloom.FactionTypes["Cultist.Origin"]));
}