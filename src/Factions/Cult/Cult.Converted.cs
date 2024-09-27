using Lotus.Factions;
using Lotus.Factions.Interfaces;
using Lotus.GUI.Name.Components;

namespace LotusBloom.Factions.Cult;

public partial class Cultist
{
    public class Converted : Cultist, ISubFaction<Cultist>
    {
        public NameComponent NameComponent { get; }

        public Converted(NameComponent component)
        {
            NameComponent = component;
        }

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

        public override bool CanSeeRole(PlayerControl player) => true;
    }
}