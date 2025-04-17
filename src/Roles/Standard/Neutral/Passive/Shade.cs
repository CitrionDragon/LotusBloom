using System.Collections.Generic;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Roles.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Extensions;
using Lotus.Managers.History.Events;
using Lotus.Statuses;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using Lotus.GameModes.Standard;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Roles;
using Lotus.Factions.Impostors;
using Lotus.Factions.Crew;
using Lotus;
using AmongUs.GameOptions;
using Lotus.Options;
using System.Linq;
using Lotus.Factions;
using Il2CppSystem.Runtime.Remoting.Messaging;
using Lotus.Roles.Subroles;
using VentLib.Logging;

namespace LotusBloom.Roles.Standard.Neutral.Passive;

public class Shade: Impostor
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Shade));
    private Cooldown swapCooldown= null!;
    private bool cantCallMeetings;
    private bool cantreport;
    private IRemote? cooldownOverride;
    
    protected override void PostSetup()
    {
        Rogue.IncompatibleRoles.Add(typeof(Shade));
        swapCooldown.Start();
    }

    [UIComponent(UI.Text, gameStates: GameState.Roaming)]
    private string CooldownIndicator() => swapCooldown.IsReady() ? "" : Color.gray.Colorize(" (" + swapCooldown + "s)");

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        if (swapCooldown.NotReady()) return false;
        if (target == null) return false;
        MyPlayer.RpcMark(target);
        if (MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this)) is InteractionResult.Halt) return false;
        CustomRole targetRole = target.PrimaryRole();
        CustomRole myRole = MyPlayer.PrimaryRole();
        //StandardRoles roleHolder = StandardGameMode.Instance.RoleManager.RoleHolder;
        CustomRole newRole = StandardGameMode.Instance.RoleManager.GetCleanRole(targetRole);
        Game.AssignRole(MyPlayer, newRole);
        CustomRole role = MyPlayer.PrimaryRole();
        role.Assign();
        newRole = StandardGameMode.Instance.RoleManager.GetCleanRole(myRole);
        Game.AssignRole(target, newRole);
        role = target.PrimaryRole();
        role.Assign();
        //swapCooldown.Start();
        return false;
    }

    [RoleAction(LotusActionType.OnPet)]
    public void SwapRoles()
    {
        if (swapCooldown.NotReady()) return;
        PlayerControl? target = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault(p => Relationship(p) is not Relation.FullAllies);
        if (target == null) return;
        MyPlayer.RpcMark(target);
        if (MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this)) is InteractionResult.Halt) return;
        CustomRole targetRole = target.PrimaryRole();
        CustomRole myRole = MyPlayer.PrimaryRole();
        //StandardRoles roleHolder = StandardGameMode.Instance.RoleManager.RoleHolder;
        CustomRole newRole = StandardGameMode.Instance.RoleManager.GetCleanRole(targetRole);
        Game.AssignRole(MyPlayer, newRole);
        CustomRole role = MyPlayer.PrimaryRole();
        if (ProjectLotus.AdvancedRoleAssignment) role.Assign();
        newRole = StandardGameMode.Instance.RoleManager.GetCleanRole(myRole);
        Game.AssignRole(target, newRole);
        role = target.PrimaryRole();
        role.Assign();
        
        swapCooldown.Start();
    }

    [RoleAction(LotusActionType.RoundStart)]
    private void RoundStart() => swapCooldown.Start();

    [RoleAction(LotusActionType.ReportBody)]
    private void CancelReportBody(Optional<NetworkedPlayerInfo> deadBody, ActionHandle handle)
    {
        if (deadBody.Exists() && cantreport) handle.Cancel();
        if (!deadBody.Exists() && cantCallMeetings) handle.Cancel();
    }

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Neutrals.Passive.Shade.yaml";

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Swap Cooldown")//, KnifeCooldown)
                .Value(v =>  v.Text(GeneralOptionTranslations.GlobalText).Color(new Color(1f, 0.61f, 0.33f)).Value(-1f).Build())
                .AddFloatRange(0, 120, 2.5f, 0, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(swapCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Can't Report Bodies")//, Translations.Options.CantCallEmergencyMeetings)
                .BindBool(b => cantreport = b)
                .AddOnOffValues(false)
                .Build())
            .SubOption(sub => sub.Name("Can't Call Emergency Meetings")//, Translations.Options.CantCallEmergencyMeetings)
                .BindBool(b => cantCallMeetings = b)
                .AddOnOffValues(false)
                .Build());
    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        roleModifier.RoleColor(Color.gray)
            .RoleFlags(RoleFlag.CannotWinAlone)
            .RoleAbilityFlags(RoleAbilityFlag.CannotSabotage | RoleAbilityFlag.CannotVent)
            .SpecialType(SpecialType.Neutral)
            .VanillaRole(RoleTypes.Impostor)
            .Faction(FactionInstances.Neutral);
}