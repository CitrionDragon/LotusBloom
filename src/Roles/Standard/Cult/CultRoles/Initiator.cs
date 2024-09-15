using System.Collections.Generic;
using System.Linq;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.Factions.Undead;
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

namespace LotusBloom.Roles.Standard.Cult.CultRoles;

public class Initiator : CultRole
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Necromancer));
    private static Deathknight _deathknight = new Deathknight();

    [UIComponent(UI.Cooldown)]
    private Cooldown convertCooldown;
    private bool isFirstConvert = true;
    private bool immuneToPartialConverted;

    private Deathknight? myDeathknight;
    private CustomRole deathknightOriginal = null!;
    private bool disableWinCheck;

    protected override void Setup(PlayerControl player)
    {
        base.Setup(player);
        Game.GetWinDelegate().AddSubscriber(DenyWinConditions);
    }

    [RoleAction(LotusActionType.Attack)]
    private bool NecromancerConvert(PlayerControl? target)
    {
        if (target == null) return false;
        if (MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this)) is InteractionResult.Halt) return false;
        MyPlayer.RpcMark(target);
        log.Debug($"Is first convert? {isFirstConvert} - {target.GetNameWithRole()}");
        if (isFirstConvert) return ConvertToDeathknight(target);
        ConvertToUndead(target);
        return false;
    }

    [RoleAction(LotusActionType.Interaction)]
    private void NecromancerImmunity(PlayerControl actor, Interaction interaction, ActionHandle handle)
    {
        if (interaction.Intent is not (IHostileIntent or IFatalIntent)) return;
        if (IsConvertedUndead(actor)) handle.Cancel();
        else if (immuneToPartialConverted && IsUnconvertedUndead(actor)) handle.Cancel();
    }

    // TODO: cooldown
    [RoleAction(LotusActionType.OnPet)]
    private void NecromancerConvertPet()
    {
        if (convertCooldown.NotReady()) return;
        convertCooldown.Start();
        NecromancerConvert(MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrOptional().OrElse(null!));
    }

    [RoleAction(LotusActionType.PlayerDeath)]
    private void NecromancerDeath()
    {
        if (myDeathknight == null || !myDeathknight.MyPlayer.IsAlive() || !myDeathknight.CanBecomeNecromancer) return;
        PlayerControl player = myDeathknight.MyPlayer;
        player.GetSubroles().Remove(deathknightOriginal);
        myDeathknight = null;
        player.NameModel().GetComponentHolder<CooldownHolder>().Clear();
        player.NameModel().GetComponentHolder<CooldownHolder>().Add(new CooldownComponent(convertCooldown, GameState.Roaming, ViewMode.Additive, viewers: player));

        StandardGameMode.Instance.Assign(player, this);
        Necromancer necromancer = player.PrimaryRole<Necromancer>()!;
        necromancer.isFirstConvert = false;
        Game.MatchData.GameHistory.AddEvent(new RoleChangeEvent(player, necromancer));
        disableWinCheck = true;
    }

    private bool ConvertToDeathknight(PlayerControl target)
    {
        isFirstConvert = false;

        ConvertToUndead(target);
        InitiateUndead(target);

        deathknightOriginal = target.PrimaryRole();
        Game.MatchData.Roles.AddSubrole(target.PlayerId, deathknightOriginal);
        StandardGameMode.Instance.Assign(target, _deathknight);
        myDeathknight = target.PrimaryRole<Deathknight>();
        target.NameModel().GetComponentHolder<RoleHolder>()[^1]
            .SetViewerSupplier(() => Players.GetAllPlayers().Where(p => p.PlayerId == target.PlayerId || p.Relationship(target) is Relation.FullAllies).ToList());

        log.Fatal($"Indicator count 22: {target.NameModel().GetComponentHolder<IndicatorHolder>().Count}");
        Game.MatchData.GameHistory.AddEvent(new RoleChangeEvent(target, _deathknight));
        return false;
    }

    private void DenyWinConditions(WinDelegate winDelegate)
    {
        if (disableWinCheck) return;
        List<PlayerControl> winners = winDelegate.GetWinners();
        if (winners.Any(p => p.PlayerId == MyPlayer.PlayerId)) return;
        List<PlayerControl> undeadWinners = winners.Where(p => p.PrimaryRole().Faction is TheUndead).ToList();

        if (undeadWinners.Count(IsConvertedUndead) == winners.Count) winDelegate.CancelGameWin();
        else if (undeadWinners.Count == winners.Count && MyPlayer.IsAlive()) winDelegate.CancelGameWin();
        else undeadWinners.Where(tc => IsConvertedUndead(tc) || MyPlayer.IsAlive() && IsUnconvertedUndead(tc)).ForEach(uw => winners.Remove(uw));
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Convert Cooldown")
                .AddFloatRange(15f, 120f, 5f, 9, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(convertCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Immune to Partially Converted")
                .AddOnOffValues()
                .BindBool(b => immuneToPartialConverted = b)
                .Build());

    protected override List<CustomRole> LinkedRoles() => base.LinkedRoles().Concat(new List<CustomRole>() { _deathknight }).ToList();

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(0.61f, 0.53f, 0.67f))
            .CanVent(false)
            .OptionOverride(new IndirectKillCooldown(convertCooldown.Duration))
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet);
}