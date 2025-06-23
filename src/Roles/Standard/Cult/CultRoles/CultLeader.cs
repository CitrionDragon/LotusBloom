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
using Lotus.Roles.Events;
using LotusBloom.Factions.Cult;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.GUI;

namespace LotusBloom.Roles.Standard.Cult.CultRoles;

public class CultLeader : CultRole, IRoleUI
{
    private bool limitedKillRange;
    private int alivePlayers;
    private bool disableWinCheck;

    public CultLeader()
    {
        RelatedRoles.Add(typeof(Initiator));
    }

    protected override void PostSetup()
    {
        Game.GetWinDelegate().AddSubscriber(DenyWinConditions);
        List<PlayerControl> initiated = Players.GetPlayers().Where(p => p.PlayerId != MyPlayer.PlayerId && Relationship(p) is Relation.SharedWinners).ToList();
        initiated.ForEach(p=>
        {
            ConvertToCult(p);
        });
    }

    [UIComponent(UI.Cooldown)]
    private Cooldown killCooldown = null!;

    [RoleAction(LotusActionType.Interaction)]
    private void NecromancerImmunity(PlayerControl actor, Interaction interaction, ActionHandle handle)
    {
        if (interaction.Intent is not (IHostileIntent or IFatalIntent)) return;
        if (IsConvertedCult(actor)) handle.Cancel();
    }

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        killCooldown.Start(AUSettings.KillCooldown());
        return base.TryKill(target);
    }

    [RoleAction(LotusActionType.OnPet)]
    private void CultistKill()
    {
        if (killCooldown.NotReady()) return;
        killCooldown.Start(AUSettings.KillCooldown());
        List<PlayerControl> eligiblePlayers = Players.GetAlivePlayers()
            .Where(p => p.Relationship(MyPlayer) is Relation.FullAllies & p.PlayerId != MyPlayer.PlayerId)
            .ToList();
        PlayerControl selected = eligiblePlayers.PopRandom();
        KillNearestPlayer(selected, limitedKillRange);
        MyPlayer.RpcMark(MyPlayer);
    }

    private bool KillNearestPlayer(PlayerControl player, bool limitToRange)
    {
        List<PlayerControl> inRangePlayers = limitToRange
            ? player.GetPlayersInAbilityRangeSorted()
            : RoleUtils.GetPlayersWithinDistance(player, 9999, true).ToList();

        if (inRangePlayers.Count == 0) return false;

        PlayerControl target = inRangePlayers.GetRandom();
        ManipulatedPlayerDeathEvent playerDeathEvent = new(target, player);
        FatalIntent fatalIntent = new(false, () => playerDeathEvent);

        bool isDead = player.InteractWith(target, new ManipulatedInteraction(fatalIntent, player.PrimaryRole(), MyPlayer)) is InteractionResult.Proceed;
        Game.MatchData.GameHistory.AddEvent(new ManipulatedPlayerKillEvent(player, target, MyPlayer, isDead));

        return isDead;
    }

    private void DenyWinConditions(WinDelegate winDelegate)
    {
        if (disableWinCheck) return;
        List<PlayerControl> winners = winDelegate.GetWinners();
        if (winners.Any(p => p.PlayerId == MyPlayer.PlayerId)) return;
        List<PlayerControl> undeadWinners = winners.Where(p => p.PrimaryRole().Faction == FactionInstances.GetExternalFaction(typeof(Cultist.Origin))).ToList();

        if (undeadWinners.Count(IsConvertedCult) == winners.Count) winDelegate.CancelGameWin();
        else if (undeadWinners.Count == winners.Count && MyPlayer.IsAlive()) winDelegate.CancelGameWin();
        else undeadWinners.Where(tc => IsConvertedCult(tc) || MyPlayer.IsAlive() && IsUnconvertedCult(tc)).ForEach(uw => winners.Remove(uw));
    }

    public RoleButton PetButton(IRoleButtonEditor petButton) => 
        petButton.SetText("Command")
            .BindCooldown(killCooldown)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/mastermind_manipulate.png", 130, true));

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        AddKillCooldownOptions(base.RegisterOptions(optionStream)
            .SubOption(sub2 => sub2.Name("Limited Cultist Kill Range")
                .AddBoolean(false)
                .BindBool(b => limitedKillRange = b)
                .Build()));

    public override RoleType GetRoleType() => RoleType.Transformation;

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(0.8f, 0.36f, 0.8f))
            .RoleFlags(RoleFlag.TransformationRole)
            .CanVent(false)
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet);
}