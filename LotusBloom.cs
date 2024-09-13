using Lotus.Addons;
using Lotus.GameModes.Standard;
using LotusBloom.Version;
using Lotus.Roles;
using System.Collections.Generic;
using SampleRoleAddon.Roles.Standard;
using LotusBloom.Roles.Standard;
using LotusBloom.Roles.Standard.Neutral;

namespace LotusBloom;

public class LotusBloom: LotusAddon
{
    public override void Initialize()
    {
        // Create instances first
        List<CustomRole> allRoles = new List<CustomRole>() { new CrewCrew(), new Hypnotist(), new Harbinger()};

        // Add your role to the gamemmode of your choice (Standard in this case.)
        allRoles.ForEach(StandardRoles.AddRole);

        // Register roles
        ExportCustomRoles(allRoles, typeof(StandardGameMode));
    }

    public override string Name { get; } = "Lotus Bloom Addon Addon";

    public override VentLib.Version.Version Version { get; } = new LotusBloomVersion();
}


