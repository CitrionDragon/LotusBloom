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
    private static readonly List<IFaction> CultFactions = new() { FactionInstances.GetExternalFaction(LotusBloom.FactionTypes["Cultist.Origin"]), new Cultist.Initiated(null!, null!) };
    public List<IFaction> Factions() => CultFactions;

    public bool IsConditionMet(out List<IFaction> factions)
    {
        factions = CultFactions;

        int aliveUndead = 0;
        int aliveOther = 0;

        bool necromancerAlive = false;
        foreach (CustomRole role in Players.GetAlivePlayers().Select(p => p.PrimaryRole()))
        {
            if (role is CultLeader) necromancerAlive = true;
            if (role.Faction == FactionInstances.GetExternalFaction(LotusBloom.FactionTypes["Cultist.Origin"])) aliveUndead++;
            else aliveOther++;
        }

        //if (necromancerAlive && aliveUndead >= aliveOther) VentLogger.Info("Undead Win");
        return necromancerAlive && aliveOther <= 0;
    }

    public WinReason GetWinReason() => new(ReasonType.FactionLastStanding);
}