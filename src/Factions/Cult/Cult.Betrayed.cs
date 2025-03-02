using Lotus.Factions;
using Lotus.Factions.Interfaces;
using Lotus.GUI.Name.Components;

namespace LotusBloom.Factions.Cult;

public partial class Cultist
{
    public class Betrayed : Cultist, ISubFaction<Cultist>
    {
        public NameComponent NameComponent { get; }

        public Betrayed(NameComponent component)
        {
            NameComponent = component;
        }
        
        public Relation MainFactionRelationship() => Relation.None;

        public Relation Relationship(ISubFaction<Cultist> subFaction)
        {
            return subFaction switch
            {
                Origin => Relation.None,
                Converted => Relation.None,
                Initiated => Relation.None,
                Betrayed => Relation.None,
                _ => subFaction.Relationship(this)
            };
        }

        public override bool CanSeeRole(PlayerControl player) => false;
    }
}