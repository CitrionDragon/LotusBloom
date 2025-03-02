using Lotus.Factions;
using Lotus.Factions.Interfaces;
using Lotus.GUI.Name.Components;
using VentLib.Utilities.Collections;

namespace LotusBloom.Factions.Cult;

public partial class Cultist
{
    public class Initiated : Cultist, ISubFaction<Cultist>
    {
        public IFaction PreviousFaction { get; }
        public Remote<IndicatorComponent> Indicator { get; }

        public Initiated(IFaction previousFaction, Remote<IndicatorComponent> indicator)
        {
            this.PreviousFaction = previousFaction;
            this.Indicator = indicator;
        }

        public Relation MainFactionRelationship() => Relation.SharedWinners;

        public Relation Relationship(ISubFaction<Cultist> subFaction)
        {
            return subFaction switch
            {
                Origin => Relation.SharedWinners,
                Converted => Relation.SharedWinners,
                Initiated => Relation.SharedWinners,
                Betrayed => Relation.None,
                _ => subFaction.Relationship(this)
            };
        }

        public override bool CanSeeRole(PlayerControl player) => false;
    }
}