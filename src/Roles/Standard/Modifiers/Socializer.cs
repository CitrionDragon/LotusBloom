using System.Linq;
using Lotus.API.Odyssey;
using Lotus.API.Vanilla.Meetings;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Roles;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Overrides;
using Lotus.Roles.Subroles;
using UnityEngine;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Optionals;

namespace LotusBloom.Roles.Standard.Modifiers;

public class Socializer : Subrole
{
    private int nearbyplayers;
    private bool paused;
    private float starttime;
    private Cooldown socializetime = null!;
    private IRemote roleOverride;
    private int additionalVotes;
    private bool benefits = false;

    [UIComponent(UI.Text, gameStates: GameState.Roaming)]
    private string socializetimeText() => RoleColor.Colorize("("+socializetime+"s)");

    [RoleAction(LotusActionType.RoundStart)]
    public void RoundStart()
    {
        socializetime.SetDuration(starttime);
        socializetime.Start();
        benefits = false;
        roleOverride?.Delete();
    }

    [RoleAction(LotusActionType.RoundEnd)]
    public void RoundEnd()
    {
        if (socializetime.NotReady()) return;
        benefits = true;
        roleOverride = AddOverride(new GameOptionOverride(Override.AnonymousVoting, false));
    }

    [RoleAction(LotusActionType.Vote)]
    private void Vote(Optional<PlayerControl> voted, MeetingDelegate meetingDelegate)
    {
        if (!benefits) return;
        for (int i = 0; i < additionalVotes; i++) meetingDelegate.CastVote(MyPlayer, voted);
    }

    [RoleAction(LotusActionType.FixedUpdate)]
    public void FixedUpdate()
    {
        nearbyplayers = RoleUtils.GetPlayersWithinDistance(MyPlayer, 2f).Count();
        if (nearbyplayers < 2)
        {
            if (!paused)
            {
                socializetime.SetDuration(socializetime.TimeRemaining());
            }
            socializetime.Start();
            paused=true;
            return;
        }
        paused=false;
    }

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Modifiers.Socializer.yaml";

    public override string Identifier() => "❀";
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Time Before Benefits are Gained")//, Translations.Options.SpeedGainPerKill)
                .AddFloatRange(5f, 60f, 2.5f, 8)
                .BindFloat(f => starttime = f)
                .Build())
            .SubOption(sub => sub.Name("Additional Votes Gained")//, Translations.Options.MayorAdditionalVotes)
                .AddIntRange(0, 10, 1, 1)
                .BindInt(i => additionalVotes = i)
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) => base.Modify(roleModifier)
        .RoleColor(new Color(1f,0.1f,0.55f));
}