using System.Collections.Generic;
using System.Linq;
using Hazel;
using Lotus;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Stats;
using Lotus.Extensions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.GUI.Name.Interfaces;
using Lotus.Options;
using Lotus.Roles;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Roles.RoleGroups.Madmates.Roles;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Networking.RPC;
//using VentLib.Options.Game;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Options.UI;
using InnerNet;
using Lotus.Factions.Impostors;
using VentLib.Utilities.Optionals;
using Lotus.Factions;
using HarmonyLib;

namespace LotusBloom.Roles.Standard.Impostors.Madmates;

public class Spy: MadCrewmate
{
    private static IAccumulativeStatistic<int> _bloomsGrown = Statistic<int>.CreateAccumulative($"Roles.{nameof(Spy)}.BloomsGrown", () => Translations.BloomsGrownStatistic);
    private static IAccumulativeStatistic<int> _rolesRevealed = Statistic<int>.CreateAccumulative($"Roles.{nameof(Spy)}.RolesRevealed", () => Translations.RolesRevealedStatistic);
    public static readonly List<Statistic> HerbalistStatistics = new() { _bloomsGrown, _rolesRevealed };
    public override List<Statistic> Statistics() => HerbalistStatistics;

    private static Color _bloomColor = new(1f, 0.63f, 0.7f);

    private float bloomTime;
    public int bloomsBeforeReveal=2;
    private bool revealOnBloom=true;
    private int tasksForFaction;
    private bool rolerevealed;
    private bool factionrevealed;
    [NewOnSetup] private List<byte> playerrolerevealed = null!;
    [NewOnSetup] private List<byte> playerfactionrevealed = null!;
    [NewOnSetup] private Dictionary<byte, int> bloomCounts = new();
    [NewOnSetup] private HashSet<byte> blooming = new();
    [NewOnSetup] private Dictionary<byte, List<byte>> revealedPlayers = new();

    [UIComponent(UI.Cooldown)]
    private Cooldown bloomCooldown;

    [RoleAction(LotusActionType.OnPet)]
    public void PutBloomOnPlayer()
    {
        if (bloomCooldown.NotReady()) return;
        PlayerControl? closestPlayer = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault();
        if (closestPlayer == null) return;
        if (blooming.Contains(closestPlayer.PlayerId)) return;
        bloomCooldown.Start();
/*
        if (bloomCounts.GetValueOrDefault(closestPlayer.PlayerId) >= 2)
        {
            RevealPlayer(closestPlayer);
            return;
        }
        else if (bloomCounts.GetValueOrDefault(closestPlayer.PlayerId) == 1)
        {
            RevealFaction(closestPlayer);
            return;
        }
*/
        RpcV3.Immediate(closestPlayer.NetId, RpcCalls.SetScanner, SendOption.None).Write(true).Write(++MyPlayer.scannerCount).Send(MyPlayer.GetClientId());
        Async.Schedule(() => FinishBloom(closestPlayer.PlayerId), bloomTime);
    }

    protected override void OnTaskComplete(Optional<NormalPlayerTask> _)
    {
        if (TasksComplete == tasksForFaction)
        {
            foreach (byte target in playerfactionrevealed)
            {
                PlayerControl player =Utils.GetPlayerById(target);
                Color color=Color.gray;
                CustomRole role = player.PrimaryRole();
                if (role.SpecialType is not SpecialType.Neutral && role.SpecialType is not SpecialType.NeutralKilling)
                {
                    if (role.Faction.GetType() == typeof(ImpostorFaction)) color=Color.red;
                    else
                    {
                        color=Color.cyan;
                    }
                }
                PlayerControl[] viewers = Players.GetAllPlayers().Where(IsAllies).ToArray();
                viewers.ForEach(p =>
                {
                    NameComponent nameComponent = new(new LiveString(player.name, color), Game.InGameStates, ViewMode.Replace, p);
                    player.NameModel().GetComponentHolder<NameHolder>().Add(nameComponent);
                });
            }
            factionrevealed=true;
        }
        if (!HasAllTasksComplete) return;
        foreach (byte target in playerrolerevealed)
        {
            PlayerControl player = Utils.GetPlayerById(target);
            INameModel nameModel = player.NameModel();
            RoleHolder roleHolder = nameModel.GCH<RoleHolder>();
            PlayerControl[] viewers = Players.GetAllPlayers().Where(IsAllies).ToArray();
            viewers.ForEach(p =>
            {
                roleHolder.Last().AddViewer(p);
                player.NameModel().GCH<RoleHolder>().Last().AddViewer(p);
                revealedPlayers.GetOrCompute(player.PlayerId, () => new List<byte>()).Add(p.PlayerId);
            });
        }
        rolerevealed=true;
    }

    private void RevealPlayer(PlayerControl player)
    {
        INameModel nameModel = player.NameModel();
        RoleHolder roleHolder = nameModel.GCH<RoleHolder>();
        PlayerControl? viewer = !revealedPlayers.ContainsKey(player.PlayerId) ? MyPlayer
            : RoleUtils.GetPlayersWithinDistance(player, 900, true).FirstOrDefault(p => !revealedPlayers[player.PlayerId].Contains(p.PlayerId));

        if (viewer == MyPlayer) _rolesRevealed.Update(MyPlayer.UniquePlayerId(), i => i + 1);
        if (viewer == null) return;
        roleHolder.Last().AddViewer(viewer);
        playerrolerevealed.Add(player.PlayerId);
        revealedPlayers.GetOrCompute(player.PlayerId, () => new List<byte>()).Add(viewer.PlayerId);
        if (rolerevealed)
        {
            PlayerControl[] viewers = Players.GetAllPlayers().Where(IsAllies).ToArray();
            viewers.ForEach(p =>
            {
                roleHolder.Last().AddViewer(p);
                player.NameModel().GCH<RoleHolder>().Last().AddViewer(p);
                revealedPlayers.GetOrCompute(player.PlayerId, () => new List<byte>()).Add(p.PlayerId);
            });
        }
    }

    private void RevealFaction(PlayerControl player)
    {
        Color color=Color.gray;
        CustomRole role = player.PrimaryRole();
        if (role.SpecialType is not SpecialType.Neutral && role.SpecialType is not SpecialType.NeutralKilling)
        {
            if (role.Faction.GetType() == typeof(ImpostorFaction)) color=Color.red;
            else
            {
                color=Color.cyan;
            }
        }
        playerfactionrevealed.Add(player.PlayerId);
        NameComponent nameComponent = new(new LiveString(player.name, color), Game.InGameStates, ViewMode.Replace, MyPlayer);
        player.NameModel().GetComponentHolder<NameHolder>().Add(nameComponent);
        if (factionrevealed)
        {
            PlayerControl[] viewers = Players.GetAllPlayers().Where(IsAllies).ToArray();
            viewers.ForEach(p =>
            {
                NameComponent nameComponent2 = new(new LiveString(player.name, color), Game.InGameStates, ViewMode.Replace, p);
                player.NameModel().GetComponentHolder<NameHolder>().Add(nameComponent2);
            });
        }
    }
    private bool IsAllies(PlayerControl player)
    {
        if (Relationship(player) is Relation.FullAllies) return true;
        //if (Relationship(player) is Relation.SharedWinners) return true;
        //CustomRole role = player.PrimaryRole();
        //if (role.Faction is ImpostorFaction) return true;
        return false;
    }
    private void FinishBloom(byte playerId)
    {
        blooming.Remove(playerId);
        PlayerControl? player = Players.FindPlayerById(playerId);
        if (player == null) return;
        RpcV3.Immediate(player.NetId, RpcCalls.SetScanner, SendOption.None).Write(false).Write(++MyPlayer.scannerCount).Send(MyPlayer.GetClientId());
        _bloomsGrown.Update(MyPlayer.UniquePlayerId(), i => i + 1);
        int count = bloomCounts.Compose(playerId, i => i + 1, () =>
        {
            LiveString ls = new(() =>
            {
                int count = bloomCounts.GetValueOrDefault(playerId);
                if (count < bloomsBeforeReveal) return RoleUtils.Counter(count, bloomsBeforeReveal, RoleColor);
                return _bloomColor.Colorize("✿");
            });

            player.NameModel().GCH<CounterHolder>().Add(new CounterComponent(ls, Game.InGameStates, ViewMode.Additive, MyPlayer));
            return 1;
        });

        if (count==1)
        {
            RevealFaction(player);
        }

        if (count < bloomsBeforeReveal) return;
        playerrolerevealed.Add(player.PlayerId);
        player.NameModel().GCH<RoleHolder>().Last().AddViewer(MyPlayer);
        revealedPlayers.GetOrCompute(player.PlayerId, () => new List<byte>()).Add(MyPlayer.PlayerId);
        if (rolerevealed)
        {
            PlayerControl[] viewers = Players.GetAllPlayers().Where(IsAllies).ToArray();
            viewers.ForEach(p =>
            {
                //roleHolder.Last().AddViewer(p);
                player.NameModel().GCH<RoleHolder>().Last().AddViewer(p);
                revealedPlayers.GetOrCompute(player.PlayerId, () => new List<byte>()).Add(p.PlayerId);
            });
        }
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Time until Bug Complete")//, Translations.Options.TimeUntilBloom)
                .BindFloat(f => bloomTime = f)
                .AddFloatRange(0, 180, 5f, 3, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub.Name("Plant Bug Cooldown")//, Translations.Options.PlantBloomCooldown)
                .BindFloat(bloomCooldown.SetDuration)
                .AddFloatRange(0, 180, 2.5f, 8, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub.Name("Tasks until Factions are revealed")//, Translations.Options.BloomsUntilRoleReveal)
                .AddIntRange(0, 10, 1, 3)
                .BindInt(i => tasksForFaction = i)
                .Build());

    [Localized(nameof(Spy))]
    private static class Translations
    {
        [Localized(nameof(BloomsGrownStatistic))]
        public static string BloomsGrownStatistic = "Blooms Grown";

        [Localized(nameof(RolesRevealedStatistic))]
        public static string RolesRevealedStatistic = "Roles Revealed";


        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(TimeUntilBloom))]
            public static string TimeUntilBloom = "Time Until Bloom";

            [Localized(nameof(BloomsUntilRoleReveal))]
            public static string BloomsUntilRoleReveal = "Blooms Until Role Reveal";

            [Localized(nameof(PlantBloomCooldown))]
            public static string PlantBloomCooldown = "Plant Bloom Cooldown";

            [Localized(nameof(RevealOnBloom))]
            public static string RevealOnBloom = "Reveal on Bloom";
        }
    }
}