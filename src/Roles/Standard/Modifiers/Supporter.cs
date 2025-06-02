using System.Collections.Generic;
using System.Linq;
using Lotus;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.Extensions;
using Lotus.Factions;
using Lotus.Factions.Impostors;
using Lotus.GameModes.Standard;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Logging;
using Lotus.Managers;
using Lotus.Roles;
using Lotus.Roles.Events;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Overrides;
using Lotus.Roles.Subroles;
using Lotus.Utilities;
using LotusBloom.Patches;
using Rewired.Data.Mapping;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Logging;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;

namespace LotusBloom.Roles.Standard.Modifiers;

public class Supporter : Subrole
{
    private static readonly StandardLogger _log = LoggerFactory.GetLogger<StandardLogger>(typeof(Supporter));
    [NewOnSetup] private FixedUpdateLock fixedUpdateLock = new(ModConstants.RoleFixedUpdateCooldown);
    public int CooldownReduction;
    public bool MadmateAddon;
    [NewOnSetup] private List<int> AffectedAllies= new();
    [NewOnSetup] private List<Remote<GameOptionOverride>> AffectedCds= new();

    [UIComponent(UI.Counter)]
    private string CultCounter() => RoleUtils.Counter(AffectedAllies.Count(), AffectedCds.Count());

    [RoleAction(LotusActionType.FixedUpdate)]
    private void decreasecd()
    {
        if (Game.State is GameState.InMeeting || !fixedUpdateLock.AcquireLock()) return;
        
        List<PlayerControl> allies = Players.GetPlayers().Where(IsAllies).ToList();
        List<PlayerControl> nearbyallies = RoleUtils.GetPlayersWithinDistance(MyPlayer, 2f).Where(IsAllies).ToList();
        MultiplicativeOverride defaultOverride = new(Override.KillCooldown, 1f);
        allies.ForEach(ally =>
        {
            int index = AffectedAllies.IndexOf(ally.PlayerId);
            if (IsNear(ally))
            {
                MultiplicativeOverride multiplicativeOverride = new(Override.KillCooldown, (100f - CooldownReduction) / 100f);
                if (index == -1)
                {
                    _log.Info($"Added Override To {ally.name}", ally.name);
                    AffectedAllies.Add(ally.PlayerId);
                    AffectedCds.Add(Game.MatchData.Roles.AddOverride(ally.PlayerId, multiplicativeOverride));
                    ally.PrimaryRole().SyncOptions();
                }
            }
            else
            {
                if (index != -1)
                {
                    _log.Info($"Removed Override Of {ally.name}", ally.name);
                    AffectedCds[index].Delete();
                    AffectedCds.RemoveAt(index);
                    AffectedAllies.RemoveAt(index);
                    ally.PrimaryRole().SyncOptions();
                }
            }
        });
    }
    private bool IsAllies(PlayerControl player)
    {
        if (MyPlayer.Relationship(player) is Relation.FullAllies) return true;
        return false;
    }
    private bool IsNear(PlayerControl player)
    {
        bool isnear = false;
        List<PlayerControl> nearbyallies = RoleUtils.GetPlayersWithinDistance(MyPlayer, 2f).Where(IsAllies).ToList();
        nearbyallies.ForEach(ally =>
        {
            if (ally == player) isnear = true;
        });
        return isnear;
    }
    
    public override bool IsAssignableTo(PlayerControl player)
    {
        if (player.PrimaryRole().Faction.GetType() == typeof(ImpostorFaction) || (MadmateAddon & player.PrimaryRole().Faction.GetType() == typeof(Madmates))) return base.IsAssignableTo(player);
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