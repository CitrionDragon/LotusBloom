using System.Collections.Generic;
using System.Linq;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.Factions.Crew;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.GUI.Name.Impl;
using Lotus.Managers.History.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Interactions.Interfaces;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Victory;
using Lotus.Extensions;
using Lotus.Managers;
using Lotus.Options;
using UnityEngine;
using VentLib.Logging;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using Lotus.API.Player;
using Lotus.GameModes.Standard;
using Lotus.Roles;
using Lotus.Factions.Neutrals;
using Lotus.Factions.Impostors;
using Lotus.Factions.Crew;
using System.Xml.Serialization;

namespace LotusBloom.Roles.Standard.Cult.CultRoles;

public class Initiator : CultRole
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Initiator));

    [UIComponent(UI.Cooldown)]
    private Cooldown convertCooldown;

    private int backedAlivePlayers;
    private int knownAlivePlayers;
    private int initiatedPlayers;

    private bool disableWinCheck;
    private bool changeInMeeting;
    private static readonly CultLeader CultLeader = new();

    protected override void Setup(PlayerControl player)
    {
        base.Setup(player);
        RelatedRoles.Add(typeof(CultLeader));
        Game.GetWinDelegate().AddSubscriber(DenyWinConditions);
    }

    [UIComponent(UI.Counter)]
    private string CultCounter() => RoleUtils.Counter(initiatedPlayers, knownAlivePlayers);

    [RoleAction(LotusActionType.Disconnect)]
    [RoleAction(LotusActionType.PlayerDeath)]
    private int CountAlivePlayers() => backedAlivePlayers = Players.GetPlayers(PlayerFilter.Alive | PlayerFilter.Neutral).Count(p => p.PlayerId != MyPlayer.PlayerId && Relationship(p) is not Relation.FullAllies);
    private int CountInitiatedPlayers() => initiatedPlayers = Players.GetPlayers(PlayerFilter.Alive | PlayerFilter.Neutral).Count(p => p.PlayerId != MyPlayer.PlayerId && Relationship(p) is Relation.SharedWinners);

    [RoleAction(LotusActionType.RoundStart)]
    protected override void PostSetup()
    {
        knownAlivePlayers = CountAlivePlayers();
        CountInitiatedPlayers();
    }

    [RoleAction(LotusActionType.RoundEnd)]
    private void ChangeRole()
    {
        knownAlivePlayers = CountAlivePlayers();
        CountInitiatedPlayers();
        if (initiatedPlayers >= backedAlivePlayers) StandardGameMode.Instance.Assign(MyPlayer, CultLeader);
    }

    [RoleAction(LotusActionType.Attack)]
    private bool NecromancerConvert(PlayerControl? target)
    {
        if (target == null) return false;
        if (MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this)) is InteractionResult.Halt) return false;
        CustomRole role = target.PrimaryRole();
        if (role.SpecialType is not SpecialType.Neutral && role.SpecialType is not SpecialType.NeutralKilling) return false;
        MyPlayer.RpcMark(target);
        ConvertToCult(target);
        CountInitiatedPlayers();
        if (initiatedPlayers < backedAlivePlayers || changeInMeeting) return false;
        StandardGameMode.Instance.Assign(MyPlayer, CultLeader);
        return false;
    }

    [RoleAction(LotusActionType.Interaction)]
    private void NecromancerImmunity(PlayerControl actor, Interaction interaction, ActionHandle handle)
    {
        if (interaction.Intent is not (IHostileIntent or IFatalIntent)) return;
        if (IsConvertedCult(actor)) handle.Cancel();
        else if (IsUnconvertedCult(actor)) handle.Cancel();
    }

    private void DenyWinConditions(WinDelegate winDelegate)
    {
        if (disableWinCheck) return;
        List<PlayerControl> winners = winDelegate.GetWinners();
        if (winners.Any(p => p.PlayerId == MyPlayer.PlayerId)) return;
        List<PlayerControl> undeadWinners = winners.Where(p => p.PrimaryRole().Faction is CultRole).ToList();

        if (undeadWinners.Count(IsConvertedCult) == winners.Count) winDelegate.CancelGameWin();
        else if (undeadWinners.Count == winners.Count && MyPlayer.IsAlive()) winDelegate.CancelGameWin();
        else undeadWinners.Where(tc => IsConvertedCult(tc) || MyPlayer.IsAlive() && IsUnconvertedCult(tc)).ForEach(uw => winners.Remove(uw));
    }
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Convert Cooldown")
                .AddFloatRange(15f, 120f, 5f, 9, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(convertCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Change Role in Meeting")
                .AddOnOffValues()
                .BindBool(b => changeInMeeting = b)
                .Build());
    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(0.8f, 0.36f, 0.8f))
            .CanVent(false)
            .OptionOverride(new IndirectKillCooldown(convertCooldown.Duration))
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet);
    protected override List<CustomRole> LinkedRoles() => new() {CultLeader};
}