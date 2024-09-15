using System.Collections.Generic;
using System.Linq;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.Factions.Interfaces;
using Lotus.Factions.Undead;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.GUI.Name.Interfaces;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.RoleGroups.Undead.Events;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Extensions;
using Lotus.Roles.Internals;
using UnityEngine;
using Lotus.API.Player;

namespace Lotus.Roles.RoleGroups.Undead.Roles;

public class UndeadRole : Impostor
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(UndeadRole));
    public static Color UndeadColor = new(0.33f, 0.46f, 0.76f);

    public override bool CanSabotage() => false;

    public void ConvertToUndead(PlayerControl target)
    {
        List<PlayerControl> viewers = Players.GetAlivePlayers().Where(IsConvertedUndead).ToList();

        INameModel nameModel = target.NameModel();

        IndicatorComponent indicatorComponent = new(new LiveString("◎", new Color(0.46f, 0.58f, 0.6f)), new[] { GameState.Roaming, GameState.InMeeting }, ViewMode.Additive, () => viewers);

        nameModel.GetComponentHolder<IndicatorHolder>().Add(indicatorComponent);
        viewers.ForEach(v => nameModel.GetComponentHolder<RoleHolder>()[0].AddViewer(v));

        CustomRole role = target.PrimaryRole();
        role.Faction = new TheUndead.Unconverted(role.Faction, indicatorComponent);
        role.SpecialType = SpecialType.Undead;
        Game.MatchData.GameHistory.AddEvent(new ConvertEvent(MyPlayer, target));
    }

    public void InitiateUndead(PlayerControl target)
    {
        List<PlayerControl> undead = Players.GetAlivePlayers().Where(IsConvertedUndead).ToList();
        List<PlayerControl> viewers = new() { target };

        // LiveString undeadPlayerName = new(target.name, UndeadColor);

        if (target.PrimaryRole().Faction is TheUndead.Unconverted unconverted)
        {
            IndicatorComponent oldComponent = unconverted.UnconvertedName;
            // oldComponent.SetMainText(undeadPlayerName);
            oldComponent.AddViewer(target);
        }
        else
        {
            IndicatorComponent newComponent = new(new LiveString("●", UndeadColor), new[] { GameState.Roaming, GameState.InMeeting }, ViewMode.Replace, () => viewers);
            target.NameModel().GetComponentHolder<IndicatorHolder>().Add(newComponent);
        }

        target.PrimaryRole().Faction = FactionInstances.TheUndead;
        undead.ForEach(p =>
        {
            log.Debug($"undead namemodel update - {p.GetNameWithRole()}");
            INameModel nameModel = p.NameModel();
            nameModel.GetComponentHolder<RoleHolder>()[0].AddViewer(target);

            switch (p.PrimaryRole().Faction)
            {
                case TheUndead.Converted converted:
                    converted.NameComponent.AddViewer(target);
                    break;
                case TheUndead.Unconverted:
                    log.Debug($"unconverted {nameModel.GetComponentHolder<IndicatorHolder>().Count}");
                    nameModel.GetComponentHolder<IndicatorHolder>()[0].AddViewer(target);
                    break;
                default: // origin
                    nameModel.GetComponentHolder<IndicatorHolder>().Add(new IndicatorComponent(new LiveString("●", UndeadColor), [GameState.Roaming, GameState.InMeeting], ViewMode.Replace, viewers: () => viewers));
                    break;
            }
        });
        Game.MatchData.GameHistory.AddEvent(new InitiateEvent(MyPlayer, target));
    }

    protected static bool IsUnconvertedUndead(PlayerControl player) => player.PrimaryRole().Faction is TheUndead.Unconverted;
    protected static bool IsConvertedUndead(PlayerControl player)
    {
        IFaction faction = player.PrimaryRole().Faction;
        if (faction is not TheUndead) return false;
        return faction is not TheUndead.Unconverted;
    }

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .SpecialType(SpecialType.Undead)
            .Faction(FactionInstances.TheUndead);
}