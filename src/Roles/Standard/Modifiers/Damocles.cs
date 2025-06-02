using System.Collections.Generic;
using System.Linq;
using Epic.OnlineServices.Inventory;
using Il2CppSystem.Runtime.Remoting.Messaging;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Reactive.Actions;
using Lotus.API.Vanilla.Meetings;
using Lotus.API.Vanilla.Sabotages;
using Lotus.Chat;
using Lotus.Extensions;
using Lotus.Factions;
using Lotus.Factions.Impostors;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Managers.History.Events;
using Lotus.Roles;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Overrides;
using Lotus.Roles.Subroles;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;

namespace LotusBloom.Roles.Standard.Modifiers;

public class Damocles : Subrole
{
    private Cooldown suicideTimer=null!;
    private bool paused;
    private float extratimekill;
    private float extratimemeeting;
    [NewOnSetup] private HashSet<int> enteredVents = new();

    [UIComponent(UI.Counter)]
    private string CustomCooldown() => (!MyPlayer.IsAlive() || suicideTimer.IsReady()) ? "" : Color.red.Colorize(suicideTimer + "s");

    [RoleAction(LotusActionType.VentEntered)]
    private void EnterVent(Vent vent)
    {
        if (enteredVents.Contains(vent.Id)) return;
        suicideTimer.SetDuration(suicideTimer.TimeRemaining()+10f);
        suicideTimer.Start();
        enteredVents.Add(vent.Id);
    }

    [RoleAction(LotusActionType.SabotageFixed)]
    private void ReduceTime(ISabotage sabotage)
    {
        if (sabotage.SabotageType() is SabotageType.Door) return;
        suicideTimer.SetDuration(suicideTimer.TimeRemaining()-15f);
        suicideTimer.Start();
    }

    [RoleAction(LotusActionType.ReportBody)]
    private void ReportBody(Optional<NetworkedPlayerInfo> deadBody)
    {
        if (!deadBody.Exists()) return;
        suicideTimer.SetDuration(suicideTimer.TimeRemaining()*0.9f);
    }

    [RoleAction(LotusActionType.PlayerDeath, ActionFlag.GlobalDetector)]
    public void CheckPlayerDeath(PlayerControl target, PlayerControl killer, IDeathEvent deathEvent)
    {
        if (target.Relationship(MyPlayer) is Relation.FullAllies)
        {
            suicideTimer.SetDuration(suicideTimer.TimeRemaining()-20f);
            suicideTimer.Start();
        }
        killer = deathEvent.Instigator().Map(p => p.MyPlayer).OrElse(killer);
        if (killer == null) return;
        if (killer.PlayerId == MyPlayer.PlayerId)
        {
            suicideTimer.SetDuration(suicideTimer.TimeRemaining()+AUSettings.KillCooldown()+extratimekill);
            suicideTimer.Start();
            return;
        }
        if (killer.Relationship(MyPlayer) is Relation.FullAllies)
        {
            suicideTimer.SetDuration(suicideTimer.TimeRemaining()+100f);
            suicideTimer.Start();
        }
    }

    [RoleAction(LotusActionType.FixedUpdate)]
    public void FixedUpdate()
    {
        if (MyPlayer == null) return;
        if (paused || suicideTimer.NotReady() || !MyPlayer.IsAlive()) return;
        if (Game.State is GameState.InMeeting)
        {
            suicideTimer.SetDuration(suicideTimer.TimeRemaining());
            paused=true;
            return;
        }
        MyPlayer.InteractWith(MyPlayer, new UnblockedInteraction(new FatalIntent(), this));
        Game.MatchData.GameHistory.AddEvent(new SuicideEvent(MyPlayer));
    }

    [RoleAction(LotusActionType.Exiled, ActionFlag.GlobalDetector)]
    private void Exiled(PlayerControl exiled)
    {
        if (exiled == null) return;
        if (Relationship(exiled) is Relation.FullAllies) suicideTimer.SetDuration(suicideTimer.TimeRemaining()*0.80f);
        else suicideTimer.SetDuration(suicideTimer.TimeRemaining()*1.30f);
    }

    [RoleAction(LotusActionType.RoundStart)]
    private void RoundStart()
    {
        if (MyPlayer.IsAlive())
        {
            suicideTimer.SetDuration(suicideTimer.Duration+extratimemeeting);
            suicideTimer.Start();
            paused = false;
        }
        enteredVents.Clear();
    }

    public override bool IsAssignableTo(PlayerControl player)
    {

        if (!player.GetVanillaRole().IsImpostor()) return false;
        if (!player.PrimaryRole().RoleAbilityFlags.HasFlag(RoleAbilityFlag.IsAbleToKill)) return false;
        if (player.PrimaryRole().Faction.GetType()!=typeof(ImpostorFaction)) return false;
        return base.IsAssignableTo(player);
    }

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Modifiers.Damocles.yaml";

    public override string Identifier() => "ψ";
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Starting Time")//, Translations.Options.SpeedGainPerKill)
                .AddFloatRange(10f, 120f, 2.5f, 10)
                .BindFloat(suicideTimer.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Killing Extra Time")//, Translations.Options.SpeedGainPerKill)
                .AddFloatRange(2.5f, 30f, 2.5f, 4)
                .BindFloat(f => extratimekill = f)
                .Build())
            .SubOption(sub => sub.Name("Surviving Meeting Extra Time")//, Translations.Options.SpeedGainPerKill)
                .AddFloatRange(2.5f, 30f, 2.5f, 4)
                .BindFloat(f => extratimemeeting = f)
                .Build());
    protected override RoleModifier Modify(RoleModifier roleModifier) => base.Modify(roleModifier)
        .RoleColor(Color.red);
}