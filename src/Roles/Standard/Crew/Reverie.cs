using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Managers.History.Events;
using Lotus.Roles;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Overrides;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Extensions;
using Lotus.Logging;
using Lotus.Options;
using Lotus.Roles.Interactions;
using Lotus.Roles.Interactions.Interfaces;
using Lotus.Roles.Interfaces;
using Lotus.Roles.Events;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Logging;
using VentLib.Options.UI;
using VentLib.Options.IO;
using VentLib.Utilities;
using VentLib.Utilities.Optionals;
using Lotus.Roles.Subroles;

namespace LotusBloom.Roles.Standard.Crew;

public partial class Reverie : Crewmate
{
    private bool paused = true;
    public Cooldown DeathTimer = null!;
    private bool beginsAfterFirstTask;
    private bool refreshTasks;
    private bool doneTask;
    private float protectionAmt;
    private bool isProtected;

    protected override void PostSetup()
    {
        Rogue.IncompatibleRoles.Add(typeof(Reverie));
    }

    [UIComponent(UI.Counter)]
    private string CustomCooldown() => (!MyPlayer.IsAlive() || DeathTimer.IsReady() || (HasAllTasksComplete && !refreshTasks)) ? "" : Color.white.Colorize(DeathTimer + "s");

    protected override void OnTaskComplete(Optional<NormalPlayerTask> _)
    {
        if (HasAllTasksComplete && refreshTasks) Tasks.AssignAdditionalTasks(this);
        doneTask = true;
        isProtected = true;
        Async.Schedule(() => isProtected = false, protectionAmt);
        paused = false;
        if (!HasAllTasksComplete || refreshTasks) DeathTimer.Start();
    }

    [UIComponent(UI.Indicator)]
    private string ProtectedIndicator() => !isProtected ? "" : RoleColor.Colorize("♣");

    [RoleAction(LotusActionType.FixedUpdate)]
    private void CheckForSuicide()
    {
        if (MyPlayer == null) return;
        if (paused || DeathTimer.NotReady() || !MyPlayer.IsAlive() || (HasAllTasksComplete && !refreshTasks)) return;

        if (Game.State is GameState.InMeeting)
        {
            paused = true;
            return;
        }

        //VentLogger.Trace($"Reverie ({MyPlayer.name}) Commiting Suicide", "Reverie::CheckForSuicide");

        MyPlayer.InteractWith(MyPlayer, new UnblockedInteraction(new FatalIntent(), this));
        Game.MatchData.GameHistory.AddEvent(new SuicideEvent(MyPlayer));
    }

    [RoleAction(LotusActionType.Interaction)]
    private void InteractedWith(Interaction interaction, ActionHandle handle)
    {
        if (!isProtected) return;
        if (interaction.Intent is not IFatalIntent) return;
        handle.Cancel();
    }

    [RoleAction(LotusActionType.RoundStart)]
    public void Reset() => isProtected = false;

    [RoleAction(LotusActionType.RoundStart)]
    private void SetupSuicideTimer()
    {
        paused = beginsAfterFirstTask && !doneTask;
        if (!paused && (!HasAllTasksComplete || refreshTasks))
        {
            DevLogger.Log("Restarting Timer");
            DeathTimer.Start();
        }
    }

    [RoleAction(LotusActionType.RoundEnd)]
    private void StopDeathTimer() => paused = true;

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Time Until Suicide")//, SerialKillerTranslations.SerialKillerOptionTranslations.TimeUntilSuicide)
                .Bind(v => DeathTimer.Duration = (float)v)
                .AddFloatRange(10, 120, 2.5f, 30, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub.Name("Timer Begins After First Task")//, SerialKillerTranslations.SerialKillerOptionTranslations.TimerAfterFirstKill)
                .BindBool(b => beginsAfterFirstTask = b)
                .AddBoolean(false)
                .Build())
            .SubOption(sub => sub.Name("Protection Duration")
                .BindFloat(v => protectionAmt = v)
                .AddFloatRange(2.5f, 180, 2.5f, 5, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub.Name("Refresh Tasks When All Complete")//, Translations.Options.RefreshTasks)
                .AddBoolean()
                .BindBool(b => refreshTasks = b)
                .Build());
    protected override RoleModifier Modify(RoleModifier roleModifier) => 
        base.Modify(roleModifier).RoleColor(new Color(0.95f, 0.65f, 0.04f));

/*
    [Localized(nameof(SerialKiller))]
    private static class SerialKillerTranslations
    {
        [Localized(ModConstants.Options)]
        public static class SerialKillerOptionTranslations
        {
            [Localized(nameof(TimeUntilSuicide))]
            public static string TimeUntilSuicide = "Time Until Suicide";

            [Localized(nameof(TimerAfterFirstKill))]
            public static string TimerAfterFirstKill = "Timer Begins After First Kill";
        }
    }*/
}