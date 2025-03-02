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
using LotusBloom.Roles.Standard.Crew;
using LotusBloom.Roles.Standard.Modifiers;
using LotusBloom.Factions.Cult;
using System;
using Lotus.Factions.Interfaces;
using VentLib.Utilities.Extensions;
using Lotus.Roles.RoleGroups.Crew;
using HarmonyLib;
using System.Reflection;
using Il2CppMono.Security.Authenticode;

namespace LotusBloom;

#nullable enable
public class LotusBloom: LotusAddon
{
    public static LotusBloom Instance = null!;

    private Harmony harmony;

    public static Dictionary<string, Type?> FactionTypes = new()
    {
        {"Cultist.Origin", null}
    };
    
    public override void Initialize()
    {
        List<IFaction> allFactions = new() {new Cultist.Origin()};
        allFactions.ForEach((f, i) => FactionTypes[i switch {
            0 => "Cultist.Origin",
            _ => throw new ArgumentOutOfRangeException($"{i} is not in our list of valid indexes. Did you forgot to add a number?")
        }] = f.GetType());
        ExportFactions(allFactions);
        Instance = this;
        
        // Create instances first
        List<CustomRole> allRoles = new List<CustomRole>() {new Policeman(), new Reverie(), new Hypnotist(), new Scrapper(), new Harbinger(), new Shade(), new Eraser(), new QuickShooter(), new Spy(), new Damocles(), new Radar(), new Socializer(), new Supporter(), RoleInstances.Traitor, new Initiator()};

        // Add your role to the gamemmode of your choice (Standard in this case.)
        allRoles.ForEach(StandardRoles.AddRole);
        //StandardRoles.AddRole(RoleInstances.Traitor);
        
        // Register roles
        ExportCustomRoles(allRoles, typeof(StandardGameMode));

        

        //Harmony
        harmony = new Harmony("com.citriondragon.lotusbloom");
        harmony.PatchAll();
    }

    public override string Name { get; } = "Lotus Bloom Addon";

    public override VentLib.Version.Version Version { get; } = new LotusBloomVersion();
}


