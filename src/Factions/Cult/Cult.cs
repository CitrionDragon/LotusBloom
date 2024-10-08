using Lotus.Factions;
using Lotus.Factions.Crew;
using Lotus.Factions.Impostors;
using Lotus.Factions.Interfaces;
using Lotus.Factions.Neutrals;
using UnityEngine;

namespace LotusBloom.Factions.Cult;

public abstract partial class Cultist : Faction<Cultist>
{
    public override string Name() => "The Cult";

    public override Relation Relationship(Cultist sameFaction) => Relation.FullAllies;

    public override Relation RelationshipOther(IFaction other)
    {
        return other switch
        {
            ImpostorFaction => Relation.None,
            Crewmates => Relation.None,
            Neutral => Relation.None,
            _ => other.Relationship(this)
        };
    }

    public override Color Color => new(0.8f, 0.36f, 0.8f);
}