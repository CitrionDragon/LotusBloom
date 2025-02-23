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

public class RoleInstances
{
    public static Traitor Traitor = new();
}