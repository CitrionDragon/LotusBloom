using Lotus.Factions;
using Lotus.Factions.Interfaces;

namespace LotusBloom.Factions.Cult;

public partial class Cultist
{
    public class Origin : Cultist, ISubFaction<Cultist>
    {
        public override bool CanSeeRole(PlayerControl player) => true;

        public Relation MainFactionRelationship() => Relation.FullAllies;

        public Relation Relationship(ISubFaction<Cultist> subFaction)
        {
            return subFaction switch
            {
                Origin => Relation.FullAllies,
                Converted => Relation.FullAllies,
                Initiated => Relation.SharedWinners,
                Betrayed => Relation.None,
                _ => subFaction.Relationship(this)
            };
        }
    }
}