using System.Collections.Generic;
using System.Linq;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.Factions.Interfaces;
using Lotus.Factions.Undead;
using Lotus.Roles.RoleGroups.Undead.Roles;
using Lotus.Victory.Conditions;
using Lotus.Extensions;
using Lotus.API.Player;
using Lotus.Roles;
using LotusBloom.Factions.Cult;
using LotusBloom.Roles.Standard.Cult.CultRoles;

namespace LotusBloom.Roles.Standard.Cult;

public class CultWinCondition : IFactionWinCondition
{
    private static readonly List<IFaction> CultFactions = [ FactionInstances.GetExternalFaction(typeof(Cultist.Origin)), new Cultist.Initiated(null!, null!) ];
    public List<IFaction> Factions() => CultFactions;

    public bool IsConditionMet(out List<IFaction> factions)
    {
        factions = CultFactions;

        int aliveCult = 0;
        int aliveOther = 0;

        bool cultLeaderAlive = false;
        foreach (CustomRole role in Players.GetAlivePlayers().Select(p => p.PrimaryRole()))
        {
            if (role is CultLeader) cultLeaderAlive = true;
            if (role.Faction == FactionInstances.GetExternalFaction(typeof(Cultist.Origin))) aliveCult++;
            else aliveOther++;
        }

        //if (necromancerAlive && aliveUndead >= aliveOther) VentLogger.Info("Undead Win");
        return cultLeaderAlive && aliveCult >= aliveOther;
    }

    public WinReason GetWinReason() => new(ReasonType.FactionLastStanding);
}