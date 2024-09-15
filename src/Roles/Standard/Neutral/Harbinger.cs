﻿using System.Linq;
using Il2CppSystem;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.Extensions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Logging;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.RoleGroups.Stock;
using Lotus.Victory.Conditions;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Logging;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Optionals;
using VentLib.Options.UI;
using Lotus.Roles;
using Lotus;

namespace LotusBloom.Roles.Standard.Neutral;

// TODO: Harbinger variant which kills everyone and is super OP or something lol- Have this one with a sacrifice?
public class Harbinger : TaskRoleBase
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Harbinger));
    private static ColorGradient _harbingerGradient = new(new Color(0.18f, 0.24f, 0.39f), new Color(0.57f, 0.36f, 0.57f));
    private Vector2 targetLocation;
    private int tasksBeforeCircle = 3;
    private int circleToWin;

    private const int MaxRitual = 30;
    private const float MaxRitualF = MaxRitual;

    private Remote<NameComponent>? circleProgress;
    private int progress;

    private int taskCount;
    private int ritualCount;

    [UIComponent(UI.Counter)]
    private string TaskCheck() => RoleUtils.Counter(taskCount, tasksBeforeCircle, RoleColor);

    [UIComponent(UI.Indicator)]
    private string RitualIndicator() => taskCount == tasksBeforeCircle ? RoleUtils.CalculateArrow(MyPlayer, targetLocation, RoleColor) : "";

    [RoleAction(LotusActionType.OnHoldPet)]
    public void BeginProgressBar(int timesPet)
    {
        if (timesPet != 1 || taskCount < tasksBeforeCircle || Vector2.Distance(MyPlayer.GetTruePosition(), targetLocation) > ModConstants.ArrowActivationMin) return;
        progress = 0;
        LiveString liveString = new(() =>
        {
            if (progress < 0) return "";
            DevLogger.Log($"{progress / MaxRitualF}");
            return RoleUtils.HealthBar(progress, MaxRitual, _harbingerGradient.Evaluate(progress / MaxRitualF)) + "\n";
        });
        circleProgress?.Delete();
        circleProgress = MyPlayer.NameModel().GCH<NameHolder>().Insert(0, new NameComponent(liveString, new[] { GameState.Roaming }, ViewMode.Additive, MyPlayer));
        Async.WaitUntil(() => progress = Math.Min(progress + 1, MaxRitual), p => p is < 0 or >= MaxRitual, FinishProgress, 0.2f, 30);
    }

    protected override void OnTaskComplete(Optional<NormalPlayerTask> playerTask)
    {
        taskCount++;
        if (taskCount > tasksBeforeCircle)
        {
            if (MyPlayer.IsAlive()) MyPlayer.InteractWith(MyPlayer, new UnblockedInteraction(new FatalIntent(), this));
            taskCount = tasksBeforeCircle;
        }

        PlayerControl? farPlayer = RoleUtils.GetPlayersWithinDistance(MyPlayer, 200, true).LastOrDefault();
        if (farPlayer == null) return;
        targetLocation = farPlayer.GetTruePosition();
    }

    private void FinishProgress(int prog)
    {
        if (prog < 0)
        {
            circleProgress?.Delete();
            circleProgress = null;
            log.Trace("Harbinger Ritual Cancelled");
            return;
        }
        DevLogger.Log("Completed one!!");
        ritualCount++;
        taskCount = 0;
        if (!MyPlayer.IsAlive()) return;
        if (ritualCount == circleToWin) ManualWin.Activate(MyPlayer, ReasonType.SoloWinner, 999);
    }

    [RoleAction(LotusActionType.OnPetRelease)]
    private void CancelProgressBar() => progress = -2;

    // To Add a role image you want to override GetRoleImage. Add your png and use the AssetLoader that comes with ProjectLotus.
    /*protected override Func<Sprite> GetRoleImage()
    {
        // Depending on your image size, you may have to change 500 to another number. If it is too big or too small keep changing it until it looks good for you.
        return () => AssetLoader.LoadSprite("SampleRoleAddon.assets.crewcrew.png", 500, true);
    }*/

    protected override RoleModifier Modify(RoleModifier roleModifier) => roleModifier
        .RoleFlags(RoleFlag.CannotWinAlone)
        .RoleColor(new Color(0.57f, 0.36f, 0.57f))
        .Gradient(_harbingerGradient)
        .SpecialType(SpecialType.Neutral);

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Tasks per Ritual Circle")
                .AddIntRange(1, 5, 1, 3)
                .BindInt(i => tasksBeforeCircle = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build())
            .SubOption(sub => sub.Name("Ritual Circles Until Win")
                .AddIntRange(1, 5, 1, 3)
                .BindInt(r => circleToWin = r)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build());
            /*.SubOption(sub => sub.Name("Sacrifice to Win")
                // .AddOnOffValues() I recommend using AddBoolean which is a checkmark. But AddOnOffValues is still here for decaprecation.
                .AddBoolean()
                .BindBool(b => makesBodiesUnreportable = b)
                .Build())*/

    [Localized(nameof(Harbinger))]
    private static class Translations
    {
        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(TasksPerRitualCircle))]
            public static string TasksPerRitualCircle = "Tasks per Ritual Circle";

            [Localized(nameof(RitualCirclesUntilWin))]
            public static string RitualCirclesUntilWin = "Ritual Circles Until Win";
        }
    }
}

