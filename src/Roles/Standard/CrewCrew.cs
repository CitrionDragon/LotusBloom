using System.Collections.Generic;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Roles;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Utilities;
using Lotus.Victory.Conditions;
using UnityEngine;
using Lotus.Roles.Internals.Enums;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using Lotus.API.Odyssey;
using Lotus.Chat;
using System;

namespace LotusBloom.Roles.Standard;

// The "Crew Crew" is a Crewmate role that does something.
// If "Crew Crew" successfully reports enough players (determined by host) they win the game! It's that simple
[Localized($"Roles.{nameof(CrewCrew)}")] // used for localization, not needed on files unless you utilize localization. You will have to go into the yaml file yourself and replace the default values.
public class CrewCrew: Crewmate // There are a couple built-in role types you can extend from, crewmate is one of them.
{
    [Localized("WarningMessage")]
    private static string _warningMessage = "The {0) is close to winning! Vote them out this meeting or lose!";
    
    // Settings
    private int reportedPlayersBeforeWin;
    private bool makesBodiesUnreportable;
    private bool sendWarningMessageOnWin;
    
    // Instance-based variables below

    [NewOnSetup] 
    private HashSet<byte> grabbedPlayers;

    [UIComponent(UI.Cooldown)] // For rendering the cooldown
    private Cooldown reportBodyCooldown;

    private string ReportedPlayerCounter() => RoleUtils.Counter(grabbedPlayers.Count, reportedPlayersBeforeWin);

    [RoleAction(LotusActionType.ReportBody)]
    public void ReportAbility(NetworkedPlayerInfo reportedBody)
    {
        if (reportBodyCooldown.NotReady()) return;
        reportBodyCooldown.Start();
        
        grabbedPlayers.Add(reportedBody.PlayerId);
        if (makesBodiesUnreportable) Game.MatchData.UnreportableBodies.Add(reportedBody.PlayerId);
    }

    [RoleAction(LotusActionType.RoundStart)]
    public void ActiveGameWin()
    {
        reportBodyCooldown.Start();
        if (CheckWinCondition()) ManualWin.Activate(MyPlayer, ReasonType.SoloWinner, 999);
    }

    [RoleAction(LotusActionType.RoundEnd)]
    public void NotifyDuringMeeting()
    {
        if (!CheckWinCondition() || !sendWarningMessageOnWin) return;
        new ChatHandler()
            .Title(RoleName + " Win")
            .Message(string.Format(_warningMessage, MyPlayer.name))
            .LeftAlign()
            .Send();
    }

    // To Add a role image you want to override GetRoleImage. Add your png and use the AssetLoader that comes with ProjectLotus.
    /*
    protected override Func<Sprite> GetRoleImage()
    {
        // Depending on your image size, you may have to change 500 to another number. If it is too big or too small keep changing it until it looks good for you.
        return () => AssetLoader.LoadSprite("SampleRoleAddon.assets.crewcrew.png", 500, true);
    }
*/
    private bool CheckWinCondition() => grabbedPlayers.Count >= reportedPlayersBeforeWin;

    // This registers the options in the option menu
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Reported Bodies Amount")
                .AddIntRange(1, 15, 1, 7)
                .BindInt(i => reportedPlayersBeforeWin = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build())
            .SubOption(sub => sub.Name("Report Body Cooldown")
                .AddFloatRange(0, 120, 2.5f, suffix: "s")
                .BindFloat(reportBodyCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Make Body Unreportable")
                // .AddOnOffValues() I recommend using AddBoolean which is a checkmark. But AddOnOffValues is still here for decaprecation.
                .AddBoolean()
                .BindBool(b => makesBodiesUnreportable = b)
                .Build())
            .SubOption(sub => sub.Name("Send Warning Message")
                .AddBoolean()
                .BindBool(b => sendWarningMessageOnWin = b)
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) => 
        base.Modify(roleModifier).RoleColor(Color.magenta);
}