using Lotus.Addons;
using LotusBloom.Version;
using Lotus.Roles;
using System.Collections.Generic;
using LotusBloom.Roles.Standard.Neutral.Killing;
using LotusBloom.Roles.Standard.Neutral.Passive;
using LotusBloom.Roles.Standard.Cult.CultRoles;
using LotusBloom.Roles.Standard.Impostors;
using LotusBloom.Roles.Standard.Impostors.Madmates;
using LotusBloom.Roles.Standard.Crew;
using LotusBloom.Roles.Standard.Modifiers;
using LotusBloom.Factions.Cult;
using Lotus.Factions.Interfaces;
using HarmonyLib;
using System.Reflection;
using Lotus.GameModes.Normal.Standard;

namespace LotusBloom;

public class LotusBloom: LotusAddon
{
    public static LotusBloom Instance = null!;

    private Harmony harmony;
    
    public override void Initialize()
    {
        // Create Factions
        List<IFaction> allFactions = new() {new Cultist.Origin()};
        ExportFactions(allFactions);
        Instance = this;

        // Create instances first
        List<CustomRole> allRoles = new() {
            new Policeman(), new Reverie(), new Hypnotist(), new Initiator(),
            new Scrapper(), new Harbinger(), new Shade(), new Eraser(),
            new Framer(), new QuickShooter(), new Spy(), new Damocles(),
            new Radar(), new Socializer(), new Supporter(), RoleInstances.Traitor
        };
        
        
        // Register roles
        ExportCustomRoles(allRoles, typeof(NormalStandardGameMode));
        
        harmony = new Harmony("com.citriondragon.lotusbloom");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
    
    public override string Name { get; } = "Lotus Bloom Addon";

    public override VentLib.Version.Version Version { get; } = new LotusBloomVersion();
}


