

using System;
using System.Collections.Generic;
using System.Linq;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Vanilla.Meetings;
using Lotus.Chat;
using Lotus.Extensions;
using Lotus.Factions.Impostors;
using Lotus.GameModes.Standard;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Options;
using Lotus.Roles;
using Lotus.Roles.GUI;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Trackers;
using Lotus.Roles.RoleGroups.Impostors;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;

namespace LotusBloom.Roles.Standard.Impostors;

public class Framer: Shapeshifter, IRoleUI
{
    private PlayerControl target = null;
    private bool targetReport;
    private bool currentImps;

    [UIComponent(UI.Cooldown)]
    private Cooldown swapCooldown= null!;
    private static ColorGradient _oracleGradient = new(new Color(0.49f, 0.57f, 0.84f), new Color(0.67f, 0.36f, 0.76f));

    private Optional<byte> selectedPlayer = Optional<byte>.Null();
    private bool targetLockedIn;
    private bool initialSkip;
    [NewOnSetup] private MeetingPlayerSelector voteSelector = null!;

    public static List<Lotus.Roles.CustomRole> Imps = StandardRoles.Instance.AllRoles.Where(R => R.Faction is ImpostorFaction).ToList();
    
    public void ResendMessages() => CHandler().Message(FramerTranslations.VotePlayerInfo).Send(MyPlayer);

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target) => base.TryKill(target);

    [RoleAction(LotusActionType.Shapeshift)]
    public void Mark(PlayerControl player, ActionHandle handle)
    {
        target = player;
        RoleButton petButton = UIManager.PetButton;
        petButton.SetText(FramerTranslations.ButtonText)
            .BindCooldown(swapCooldown)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/transporter_transport.png", 130, true));
    }

    [RoleAction(LotusActionType.Unshapeshift)]
    public void UnMark(PlayerControl player, ActionHandle handle)
    {
        target = null;
        RoleButton petButton = UIManager.PetButton;
        petButton.RevertSprite().SetText("Pet");
    }

    [RoleAction(LotusActionType.OnPet)]
    public void Frame()
    {
        if (target == null||!swapCooldown.IsReady()) return;
        if (MyPlayer.inVent) target.MyPhysics.ExitAllVents();
        if (target.inVent) target.MyPhysics.ExitAllVents();
        swapCooldown.Start();
        MyPlayer.MyPhysics.ResetMoveState();
        target.MyPhysics.ResetMoveState();
        Vector2 MyPlayerPosition = MyPlayer.GetTruePosition();
        Vector2 targetPosition = target.GetTruePosition();
        if (MyPlayer.IsAlive())
            Utils.Teleport(MyPlayer.NetTransform, new Vector2(targetPosition.x, targetPosition.y + 0.3636f));
        if (target.IsAlive())
            Utils.Teleport(target.NetTransform, new Vector2(MyPlayerPosition.x, MyPlayerPosition.y + 0.3636f));
        MyPlayer.InteractWith(target, new TransportInteraction(MyPlayer, MyPlayer));
        target.InteractWith(MyPlayer, new TransportInteraction(target, MyPlayer));
    }

    [RoleAction(LotusActionType.RoundEnd)]
    private void OracleSendMessage()
    {
        initialSkip = false;
        if (selectedPlayer.Exists()) return;
        CHandler().Message(FramerTranslations.VotePlayerInfo).Send(MyPlayer);
        voteSelector.Reset();
    }

    [RoleAction(LotusActionType.Vote)]
    private void OracleLockInTarget(Optional<PlayerControl> selected, MeetingDelegate _, ActionHandle handle)
    {
        if (targetLockedIn || initialSkip) return;
        handle.Cancel();
        VoteResult result = voteSelector.CastVote(selected);
        switch (result.VoteResultType)
        {
            case VoteResultType.None:
                break;
            // case VoteResultType.Unselected:
            case VoteResultType.Selected:
                selectedPlayer = selected.Map(p => p.PlayerId);
                CHandler().Message($"{FramerTranslations.SelectRole.Formatted(selected.Get().name)}\n{FramerTranslations.SkipMessage}").Send(MyPlayer);
                break;
            case VoteResultType.Skipped:
                if (!targetLockedIn)
                {
                    selectedPlayer = Optional<byte>.Null();
                    CHandler().Message(FramerTranslations.VoteNormallyMessage).Send(MyPlayer);
                    initialSkip = true;
                }
                break;
            case VoteResultType.Confirmed:
                targetLockedIn = true;
                CHandler().Message(FramerTranslations.VoteNormallyMessage).Send(MyPlayer);
                break;
        }
    }

    [RoleAction(LotusActionType.PlayerDeath)]
    private void OracleDies()
    {
        if (!selectedPlayer.Exists()) return;
        PlayerControl target = Utils.GetPlayerById(selectedPlayer.Get())!;
        if (target == null) return; // If the target no longer exists.
        target.NameModel().GetComponentHolder<RoleHolder>().LastOrDefault(c => c.ViewMode() is ViewMode.Replace)?.SetViewerSupplier(() => Players.GetAllPlayers().ToList());
        Lotus.Roles.CustomRole RandomImp;
        if (currentImps)
        {
            List<Lotus.Roles.CustomRole> CurrentImps = new();
            Imps.ForEach(role => {
                if (role.Chance > 0) CurrentImps.Add(role);
            });
            RandomImp = CurrentImps.PopRandom();
        }
        else RandomImp = Imps.PopRandom();
        string roleName = _oracleGradient.Apply(RandomImp.GetType().Name);
        target.NameModel().GetComponentHolder<RoleHolder>().Add(new RoleComponent(new LiveString(_ => roleName), Game.InGameStates, ViewMode.Replace));
        string targetRole = target.PrimaryRole().RoleColor.Colorize(target.PrimaryRole().RoleName);
        target.NameModel().GetComponentHolder<RoleHolder>().Add(new RoleComponent(new LiveString(_ => targetRole), Game.InGameStates, ViewMode.Replace, target));
        //CHandler().Message(Translations.RevealMessage, target.name, roleName).Send();
    }

    [RoleAction(LotusActionType.ReportBody, ActionFlag.GlobalDetector)]
    private void CancelReportBody(Optional<NetworkedPlayerInfo> deadBody, ActionHandle handle, PlayerControl player)
    {
        if (deadBody.Exists() && player == target && targetReport) handle.Cancel();
    }

    [RoleAction(LotusActionType.Disconnect, ActionFlag.GlobalDetector)]
    private void TargetDisconnected(PlayerControl dcPlayer)
    {
        if (!selectedPlayer.Exists() || selectedPlayer.Get() != dcPlayer.PlayerId) return;
        selectedPlayer = Optional<byte>.Null();
        targetLockedIn = false;
    }

    public RoleButton PetButton(IRoleButtonEditor petButton) => target == null
        ? petButton.Default(false).SetText("Pet")
        : petButton.SetText(FramerTranslations.ButtonText)
            .BindCooldown(swapCooldown)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/transporter_transport.png", 130, true));

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        AddShapeshiftOptions(base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Shift Swap Cooldown")
                .AddFloatRange(0, 120, 2.5f, 0, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(swapCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Shift Target Can't Report Bodies")//, Translations.Options.CantCallEmergencyMeetings)
                .AddBoolean(false)
                .BindBool(b => targetReport = b)
                .Build())
            .SubOption(sub => sub.Name("Framing Chooses Enabled Imps")//, Translations.Options.CantCallEmergencyMeetings)
                .AddBoolean(false)
                .BindBool(b => currentImps = b)
                .Build()));

    private ChatHandler CHandler() => ChatHandler.Of(title: Color.red.Colorize(FramerTranslations.FramerMessageTitle)).LeftAlign();

    public class TransportInteraction : LotusInteraction
    {
        public PlayerControl transporter;
        public TransportInteraction(PlayerControl actor, PlayerControl transporter) : base(new NeutralIntent(), actor.PrimaryRole()) { this.transporter = transporter; }
    }

    [Localized(nameof(Framer))]
    private static class FramerTranslations
    {
        [Localized(nameof(ButtonText))] public static string ButtonText = "Swap";
        [Localized(nameof(FramerMessageTitle))]
        public static string FramerMessageTitle = "Framer Ability";

        [Localized(nameof(VotePlayerInfo))]
        public static string VotePlayerInfo = "Vote to select a player to reveal on your death. You can vote someone else to change to that person.\nAfter confirming your target cannot be changed.";

        [Localized(nameof(SelectRole))]
        public static string SelectRole = "You have selected: {0}";

        [Localized(nameof(UnselectRole))]
        public static string UnselectRole = "You have unselected: {0}";

        [Localized(nameof(VoteNormallyMessage))]
        public static string VoteNormallyMessage = "You may now vote normally.";

        [Localized(nameof(SkipMessage))]
        public static string SkipMessage = "Vote the same player to continue.";

        [Localized(nameof(RevealMessage))]
        public static string RevealMessage = "The Oracle has revealed to all that {0} is the {1}";
    }
}