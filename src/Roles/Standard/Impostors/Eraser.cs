using System.Collections.Generic;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Roles.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Extensions;
using Lotus.Managers.History.Events;
using Lotus.Statuses;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using Lotus.GameModes.Standard;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Roles;
using Lotus.Factions.Impostors;
using Lotus.Factions.Crew;
using Lotus;
using AmongUs.GameOptions;
using Lotus.Options;
using System.Linq;
using Lotus.Factions;
using Il2CppSystem.Runtime.Remoting.Messaging;

namespace LotusBloom.Roles.Standard.Impostors;

public class Eraser: Impostor
{
    private static Color eraseColor = new(0.37f, 0.74f, 0.35f);
    private bool isEraseMode = true;
    private Cooldown eraseCooldown= null!;
    private float originalCooldown;
    private float cooldownIncrease;
    private int erased=0;
    private NkRoleChange nkrole;
    private bool meetingErase;
    [NewOnSetup] private List<byte> marked;

    protected override void PostSetup() => originalCooldown=eraseCooldown.Duration;

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target) => base.TryKill(target);

    [UIComponent(UI.Text, gameStates: GameState.Roaming)]
    private string CooldownIndicator() => eraseCooldown.IsReady() ? "" : Color.red.Colorize(" (" + eraseCooldown + "s)");

    [RoleAction(LotusActionType.RoundStart)]
    private void RoundStart() => eraseCooldown.Start();

    [RoleAction(LotusActionType.RoundEnd)]
    private void RoundEnd()
    {
        marked.Filter(Players.PlayerById).ForEach(p =>
        {
            EraseRole(p);
        });
        marked.Clear();
    }

    [RoleAction(LotusActionType.OnPet)]
    public void TryErase()
    {
        if (eraseCooldown.NotReady()) return;
        PlayerControl? target = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault(p => Relationship(p) is not Relation.FullAllies);
        if (target == null) return;
        MyPlayer.RpcMark(target);
        if (MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this)) is InteractionResult.Halt) return;
        if (marked.Contains(target.PlayerId)) return;
        erased++;
        eraseCooldown.SetDuration(originalCooldown+(erased*cooldownIncrease));
        eraseCooldown.Start();
        if (meetingErase)
        {
            marked.Add(target.PlayerId);
        }
        else EraseRole(target);
        
    }
    public void EraseRole(PlayerControl target)
    {
        
        CustomRole targetRole = target.PrimaryRole();
        StandardRoles roleHolder = StandardGameMode.Instance.RoleManager.RoleHolder;
        if (targetRole.SpecialType == SpecialType.NeutralKilling)
            switch (nkrole)
            {
                case NkRoleChange.Hitman:
                    targetRole = roleHolder.Static.Hitman;
                    break;
                case NkRoleChange.Copycat:
                    targetRole = roleHolder.Static.Copycat;
                    break;
                case NkRoleChange.Jackal:
                    targetRole = roleHolder.Static.Jackal;
                    break;
            }
        else if (targetRole.SpecialType == SpecialType.Neutral)
            targetRole = roleHolder.Static.Opportunist;
        else if (targetRole.Faction is ImpostorFaction)
            targetRole = roleHolder.Static.Impostor;
        else
            targetRole = roleHolder.Static.Crewmate;
        CustomRole newRole = StandardGameMode.Instance.RoleManager.GetCleanRole(targetRole);
        Game.AssignRole(target, newRole);
        CustomRole role = target.PrimaryRole();
        role.Assign();
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Erase Cooldown")
                .AddFloatRange(0, 120, 2.5f, 0, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(eraseCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Cooldown Increase per Erase")
                .AddFloatRange(0, 30, 0.5f, 5, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(v => cooldownIncrease = v)
                .Build())
            .SubOption(sub => sub.Name("Erase Takes Effect on Meeting")
                .AddOnOffValues(false)
                .BindBool(b => meetingErase = b)
                .Build())
            .SubOption(sub => sub
                .Name("Neutral Killing Role Change When Erased")//, nkrole)
                .BindInt(v => nkrole = (NkRoleChange)v)
                .Value(v => v.Text("Hitman").Value(0).Color(Color.gray).Build())
                .Value(v => v.Text("Copycat").Value(1).Color(new Color(1f, 0.7f, 0.67f)).Build())
                .Value(v => v.Text("Jackal").Value(2).Color(new Color(0f, 0.71f, 0.92f)).Build())
                .Build());
//new Color(0.93f, 0.38f, 0.65f)
    private enum NkRoleChange
    {
        Hitman,
        Copycat,
        Jackal
    }
}