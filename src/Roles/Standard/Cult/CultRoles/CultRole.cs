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
using Lotus.Roles.Internals.Enums;
using LotusBloom.Roles.Standard.Cult.Events;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Extensions;
using Lotus.Roles.Internals;
using UnityEngine;
using Lotus.API.Player;

namespace LotusBloom.Roles.Standard.Cult.CultRoles;

public class CultRole : Impostor
{
    //private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(CultRole));
    public static Color CultColor = new(0.33f, 0.46f, 0.76f);

    public override bool CanSabotage() => false;

    public void ConvertToCult(PlayerControl target)
    {
        List<PlayerControl> viewers = Players.GetAlivePlayers().Where(IsConvertedCult).ToList();

        INameModel nameModel = target.NameModel();

        IndicatorComponent indicatorComponent = new(new LiveString("◎", new Color(0.46f, 0.58f, 0.6f)), new[] { GameState.Roaming, GameState.InMeeting }, ViewMode.Additive, () => viewers);

        nameModel.GetComponentHolder<IndicatorHolder>().Add(indicatorComponent);
        viewers.ForEach(v => nameModel.GetComponentHolder<RoleHolder>()[0].AddViewer(v));

        CustomRole role = target.PrimaryRole();
        role.Faction = new Cult.initiated(role.Faction, indicatorComponent);
        role.SpecialType = SpecialType.Undead;
        Game.MatchData.GameHistory.AddEvent(new ConvertEvent(MyPlayer, target));
    }

    public void InitiateCult(PlayerControl target)
    {
        List<PlayerControl> undead = Players.GetAlivePlayers().Where(IsConvertedCult).ToList();
        List<PlayerControl> viewers = new() { target };

        // LiveString undeadPlayerName = new(target.name, UndeadColor);

        if (target.PrimaryRole().Faction is Cult.Initiated unconverted)
        {
            IndicatorComponent oldComponent = unconverted.UnconvertedName;
            // oldComponent.SetMainText(undeadPlayerName);
            oldComponent.AddViewer(target);
        }
        else
        {
            IndicatorComponent newComponent = new(new LiveString("●", CultColor), new[] { GameState.Roaming, GameState.InMeeting }, ViewMode.Replace, () => viewers);
            target.NameModel().GetComponentHolder<IndicatorHolder>().Add(newComponent);
        }

        target.PrimaryRole().Faction = FactionInstances.Cult;
        undead.ForEach(p =>
        {
            log.Debug($"Cult namemodel update - {p.GetNameWithRole()}");
            INameModel nameModel = p.NameModel();
            nameModel.GetComponentHolder<RoleHolder>()[0].AddViewer(target);

            switch (p.PrimaryRole().Faction)
            {
                case Cult.Converted converted:
                    converted.NameComponent.AddViewer(target);
                    break;
                case Cult.Initiated:
                    log.Debug($"initiated {nameModel.GetComponentHolder<IndicatorHolder>().Count}");
                    nameModel.GetComponentHolder<IndicatorHolder>()[0].AddViewer(target);
                    break;
                default: // origin
                    nameModel.GetComponentHolder<IndicatorHolder>().Add(new IndicatorComponent(new LiveString("●", CultColor), [GameState.Roaming, GameState.InMeeting], ViewMode.Replace, viewers: () => viewers));
                    break;
            }
        });
        Game.MatchData.GameHistory.AddEvent(new InitiateEvent(MyPlayer, target));
    }

    protected static bool IsUnconvertedCult(PlayerControl player) => player.PrimaryRole().Faction is Cult.Initiated;
    protected static bool IsConvertedCult(PlayerControl player)
    {
        IFaction faction = player.PrimaryRole().Faction;
        if (faction is not Cult) return false;
        return faction is not Cult.Initiated;
    }

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .SpecialType(SpecialType.Cult)
            .Faction(FactionInstances.Cult);
}