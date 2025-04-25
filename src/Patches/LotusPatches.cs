using System;
using System.Collections.Generic;
using System.Linq;
using Lotus.GameModes.Standard;
using Lotus.Victory;
using Lotus.Victory.Conditions;
using LotusBloom.Roles.Standard.Cult;
using HarmonyLib;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Reactive.HookEvents;
using Lotus.Extensions;
using Lotus.Factions.Crew;
using Lotus.Factions.Impostors;
using VentLib.Logging;
using VentLib.Utilities.Extensions;
using LotusBloom.Roles.Standard.Modifiers;

namespace LotusBloom.Patches;

public static class LotusPatches
{
    private static readonly StandardLogger _log = LoggerFactory.GetLogger<StandardLogger>(typeof(LotusPatches));
    
    [HarmonyPatch(typeof(StandardGameMode), nameof(StandardGameMode.SetupWinConditions))]
    public static class SetupWinConditionsPatches
    {
        [HarmonyPrefix]
        public static void Prefix(StandardGameMode __instance, WinDelegate winDelegate)
        {
            new List<IWinCondition>
            {
                new CultWinCondition()
            }.ForEach(winDelegate.AddWinCondition);
            _log.Debug("Added Cult win-con.");
        }
    }

    [HarmonyPatch(typeof(StandardGameMode), nameof(StandardGameMode.ShowInformationToGhost))]
    [HarmonyPatch(new Type[] { typeof(PlayerControl) })]
    public static class ShowInformationToGhostPatches 
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerControl player)
        {
            if (player == null) return;
        
            _log.Debug($"Impostor Faction? {player.PrimaryRole().Faction.GetType()}");
            if (player.PrimaryRole().Faction.GetType() != typeof(ImpostorFaction)) return;
        
            _log.Debug($"Meetings Called? {Game.MatchData.MeetingsCalled}");
            if (Game.MatchData.MeetingsCalled < RoleInstances.Traitor.roundUntilSpawn) return; // Not sure if you mean "< 1". Because this means traitor can only spawn round 1
        
            List<PlayerControl> candidates = Players.GetPlayers().Where(p => p.IsAlive() && p.PrimaryRole().Faction is Crewmates).ToList();
            _log.Debug($"Possible Crew {candidates.Count}");
            if (candidates.Count == 0) return;
            PlayerControl candidate = candidates.GetRandom();
            if (!RoleInstances.Traitor.IsAssignableTo(candidate)) return;
            StandardGameMode.Instance.Assign(candidate, RoleInstances.Traitor, false);
        }
    }
}