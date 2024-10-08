using Lotus.Addons;
using Lotus.GameModes.Standard;
using LotusBloom.Version;
using Lotus.Roles;
using System.Collections.Generic;
using LotusBloom.Roles.Standard;
using LotusBloom.Roles.Standard.Neutral.Killing;
using LotusBloom.Roles.Standard.Neutral.Passive;
using LotusBloom.Roles.Standard.Cult.CultRoles;
using LotusBloom.Roles.Standard.Impostors;
using LotusBloom.Roles.Standard.Impostors.Madmates;
using LotusBloom.Roles.Standard.Crewmates;
using LotusBloom.Factions.Cult;

namespace LotusBloom;

public class LotusBloom: LotusAddon
{
    public override void Initialize()
    {
        // Create instances first
        List<CustomRole> allRoles = new List<CustomRole>() {new Reverie(), new Hypnotist(), new Scrapper(), new Harbinger(), new Initiator(), new QuickShooter(), new Spy()};

        // Add your role to the gamemmode of your choice (Standard in this case.)
        allRoles.ForEach(StandardRoles.AddRole);

        // Register roles
        ExportCustomRoles(allRoles, typeof(StandardGameMode));
        ExportFactions(new Cultist.Origin());
    }

    public override string Name { get; } = "Lotus Bloom Addon Addon";

    public override VentLib.Version.Version Version { get; } = new LotusBloomVersion();
}


