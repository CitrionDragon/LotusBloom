using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Managers.History.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Extensions;
using Lotus.Roles.RoleGroups.Crew;
using Lotus.Roles.RoleGroups.Neutral;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using Lotus.Options.Roles;
using Lotus.Roles.RoleGroups.Undead.Roles;
using Lotus.Roles.Subroles;
using Lotus.Roles;
using Lotus.Factions.Crew;
using Lotus.Options;
using Lotus.Factions.Impostors;
using Lotus.GameModes.Standard;
using Lotus.API.Reactive.HookEvents;
using HarmonyLib;
using Lotus.API.Player;
using System.Reflection;
using Lotus.Roles.Overrides;
using VentLib.Utilities.Optionals;
using Lotus.Roles.Properties;

namespace LotusBloom.Roles.Standard.Modifiers;

public class Traitor : Subrole
{
    private bool restrictedToCompatibleRoles;
    public bool requiresBaseKillMethod;
    private List<Color> colorGradient;

    public int roundUntilSpawn;

    [RoleAction(LotusActionType.Attack)]
    private bool TryKill(PlayerControl target)
    {
        if (!requiresBaseKillMethod) return false;
        InteractionResult result = MyPlayer.InteractWith(target, LotusInteraction.FatalInteraction.Create(this));
        Game.MatchData.GameHistory.AddEvent(new KillEvent(MyPlayer, target, result is InteractionResult.Proceed));
        return result is InteractionResult.Proceed;
    }

    //[RoleAction(LotusActionType.RoundStart)]
    protected override void PostSetup()
    {
        CustomRole role = MyPlayer.PrimaryRole();
        RoleHolder roleHolder = MyPlayer.NameModel().GetComponentHolder<RoleHolder>();
        colorGradient = new List<Color> { role.RoleColor, Color.red };
        RoleColorGradient = new ColorGradient(colorGradient.ToArray());
        string newRoleName = RoleColorGradient.Apply(role.RoleName);
        role.Faction = FactionInstances.Impostors;
        StandardRoles roleHolder2 = StandardGameMode.Instance.RoleManager.RoleHolder;
        Game.AssignRole(MyPlayer, roleHolder2.Static.Impostor);
        CustomRole role2 = MyPlayer.PrimaryRole();
        role2.Assign();
        MyPlayer.GetSubroles().Add(role);
        if (role.RealRole.IsCrewmate())
        {
            role.DesyncRole = RoleTypes.Impostor;
            MyPlayer.GetTeamInfo().MyRole = RoleTypes.Impostor;
        }
        roleHolder.Add(new RoleComponent(new LiveString(newRoleName), Game.InGameStates, ViewMode.Replace, MyPlayer));
        requiresBaseKillMethod = !role.GetActions(LotusActionType.Attack).Any();
    }
/*
    [RoleAction(LotusActionType.Exiled, ActionFlag.GlobalDetector)]
    private void Exiled(PlayerControl exiled)
    {
        if (exiled == null) return;
        if (exiled.PrimaryRole().Faction is ImpostorFaction) revealed = true;
    }

    [RoleAction(LotusActionType.PlayerDeath, ActionFlag.GlobalDetector)]
    public void CheckPlayerDeath(PlayerControl target)
    {
        if (target.PrimaryRole().Faction is ImpostorFaction) revealed = true;
    }

    [RoleAction(LotusActionType.Disconnect, ActionFlag.GlobalDetector)]
    public void CheckPlayerDc(PlayerControl target)
    {
        if (target.PrimaryRole().Faction is ImpostorFaction) revealed = true;
    }
*/

    public override bool IsAssignableTo(PlayerControl player)
    {
        int count = RoleInstances.Traitor.Count;
        int traitors = Players.GetPlayers(PlayerFilter.NonPhantom).Count(p => p.GetSubroles().Contains(RoleInstances.Traitor));
        if (traitors >= count) return false;
        System.Random random = new System.Random();
        int randomnumber = random.Next(0, 100);
        int chance = RoleInstances.Traitor.Chance;
        if (randomnumber > chance) return false;
        if (player.PrimaryRole().Faction is Crewmates) return base.IsAssignableTo(player);
        else return false;
    }

    public override HashSet<Type>? RestrictedRoles()
    {
        HashSet<Type>? restrictedRoles = base.RestrictedRoles();
        if (!restrictedToCompatibleRoles) return restrictedRoles;
        Rogue.IncompatibleRoles.ForEach(r => restrictedRoles?.Add(r));
        return restrictedRoles;
    }

    public override CompatabilityMode RoleCompatabilityMode => CompatabilityMode.Blacklisted;

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Modifiers.Traitor.yaml";

    public override string Identifier() => "";
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Restrict to Compatible Roles")//, Translations.Options.RestrictToCompatbileRoles)
                .BindBool(b => restrictedToCompatibleRoles = b)
                .AddOnOffValues()
                .Build())
            .SubOption(sub => sub.Name("Rounds until Traitor can Spawn")//, Translations.Options.BloomsUntilRoleReveal)
                .AddIntRange(0, 10, 1, 3)
                .BindInt(i => roundUntilSpawn = i)
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) => base.Modify(roleModifier)
        .RoleColor(Color.red)
        .RoleFlags(RoleFlag.Unassignable)
        .RoleAbilityFlags(RoleAbilityFlag.IsAbleToKill)
        .OptionOverride(Override.CrewLightMod, () => AUSettings.ImpostorLightMod());

}