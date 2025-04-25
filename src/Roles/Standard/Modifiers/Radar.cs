using System.Collections.Generic;
using System.Linq;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Reactive.Actions;
using Lotus.Chat;
using Lotus.Extensions;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Managers.History.Events;
using Lotus.Roles;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Subroles;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;

namespace LotusBloom.Roles.Standard.Modifiers;

public class Radar : Subrole
{
    private PlayerControl Lastplayer;
    private Color Lastcolor;
    [NewOnSetup] private List<Remote<IndicatorComponent>> indicatorComponents = null!;

    [RoleAction(LotusActionType.FixedUpdate)]
    public void GetNearestPlayer()
    {
        Color color = Color.black;
        PlayerControl selectedPlayer = null;
        PlayerControl closestPlayer;
        closestPlayer = RoleUtils.GetPlayersWithinDistance(MyPlayer,100,true).FirstOrDefault();
        if (closestPlayer != null) {color = Color.red; selectedPlayer = closestPlayer;}
        closestPlayer = RoleUtils.GetPlayersWithinDistance(MyPlayer,6f,true).FirstOrDefault();
        if (closestPlayer != null) {color = Color.yellow; selectedPlayer = closestPlayer;}
        closestPlayer = RoleUtils.GetPlayersWithinDistance(MyPlayer,3f,true).FirstOrDefault();
        if (closestPlayer != null) {color = Color.green; selectedPlayer = closestPlayer;}
        if (color == Color.black||(selectedPlayer == Lastplayer & color == Lastcolor)) return;
        indicatorComponents.ForEach(c => c.Delete());
        indicatorComponents.Clear();
        LiveString liveString = new(() => RoleUtils.CalculateArrow(MyPlayer, selectedPlayer, color));
        var remote = MyPlayer.NameModel().GetComponentHolder<IndicatorHolder>().Add(new IndicatorComponent(liveString, GameState.Roaming, viewers: MyPlayer));
        indicatorComponents.Add(remote);
        Lastplayer = selectedPlayer;
        Lastcolor = color;
    }

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Modifiers.Radar.yaml";

    public override string Identifier() => "◉";

    protected override RoleModifier Modify(RoleModifier roleModifier) => base.Modify(roleModifier)
        .RoleColor(Color.green);
}