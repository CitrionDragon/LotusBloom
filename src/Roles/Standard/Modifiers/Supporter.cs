using System.Collections.Generic;
using System.Linq;
using Lotus.API.Odyssey;
using Lotus.Extensions;
using Lotus.Factions;
using Lotus.Factions.Impostors;
using Lotus.GameModes.Standard;
using Lotus.Managers;
using Lotus.Roles;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Overrides;
using Lotus.Roles.Subroles;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;

namespace LotusBloom.Roles.Standard.Modifiers;

public class Supporter : Subrole
{
    public int CooldownReduction;
    public bool MadmateAddon;
    //[NewOnSetup] private List<byte> AffectedCd = null!;
    [NewOnSetup] private Dictionary<PlayerControl, Remote<GameOptionOverride>> AffectedCd;

    [RoleAction(LotusActionType.FixedUpdate)]
    private void decreasecd()
    {
        if (Game.State is GameState.InMeeting) return;
        List<PlayerControl> nearbyallies = RoleUtils.GetPlayersWithinDistance(MyPlayer, 2f).Where(IsAllies).ToList();
        nearbyallies.ForEach(p =>
        {
            if (!AffectedCd.ContainsKey(p))
            {
                MultiplicativeOverride multiplicativeOverride = new(Override.KillCooldown, (100f - CooldownReduction    ) / 100f);
                AffectedCd[p] = Game.MatchData.Roles.AddOverride(p.PlayerId, multiplicativeOverride);
                p.PrimaryRole().SyncOptions();
            }
        });
        AffectedCd.ForEach(pid =>
        {
            PlayerControl player = pid.Key;
            if (!nearbyallies.Contains(player))
            {
                AffectedCd.Remove(pid.Key);
                pid.Value.Delete();
                player.PrimaryRole().SyncOptions();
            }
        });
    }
    private bool IsAllies(PlayerControl player)
    {
        if (MyPlayer.Relationship(player) is Relation.FullAllies) return true;
        return false;
    }

    public override bool IsAssignableTo(PlayerControl player)
    {
        if (player.PrimaryRole().Faction.GetType()==typeof(ImpostorFaction)||(MadmateAddon&player.PrimaryRole().Faction.GetType()==typeof(Madmates))) base.IsAssignableTo(player);
        return false;
    }

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Modifiers.Supporter.yaml";

    public override string Identifier() => "⁑";
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Cooldown Reduction")//, DeadlyTranslations.Options.CooldownReduction)
                .AddIntRange(0, 100, 5, 5, "%")
                .BindInt(i => CooldownReduction = i)
                .Build())
            .SubOption(sub => sub.Name("Can Assign To Madmates")
                .AddBoolean()
                .BindBool(b => MadmateAddon = b)
                .Build());
    protected override RoleModifier Modify(RoleModifier roleModifier) => base.Modify(roleModifier)
        .RoleColor(Color.red);
}