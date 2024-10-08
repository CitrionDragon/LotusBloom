using System;
using System.Collections.Generic;
using System.Linq;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Vanilla.Meetings;
using Lotus.Chat;
using Lotus.Extensions;
using Lotus.Factions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Holders;
using Lotus.Options;
using Lotus;
using Lotus.Roles;
using Lotus.Roles.Interactions;
using Lotus.Roles.Interactions.Interfaces;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Roles.RoleGroups.Crew;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
//using VentLib.Options.Game;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using VentLib.Options.UI;
//using static Lotus.Roles.RoleGroups.Impostors.Mafioso.MafiaTranslations;
//using static Lotus.Roles.RoleGroups.Impostors.Mafioso.MafiaTranslations.MafiaOptionTranslations;

namespace LotusBloom.Roles.Standard.Neutral.Killing;

public class Scrapper: Engineer
{

    internal static Color ScrapColor = new(0.6f, 0.6f, 0.6f);
    private static Color _knifeColor = new(0.55f, 0.28f, 0.16f);
    private static Color _shieldColor = new(1f, 0.9f, 0.3f);
    private static Color _bulletColor = new(0.37f, 0.37f, 0.37f);
    private static Color _revealerColor = ModConstants.Palette.GeneralColor5;

    private static string _colorizedScrap;
    private bool modifyShopCosts;
    private bool refreshTasks;

    private int scrapFromBodies;
    private int knifeCost;
    private int shieldCost;
    private int revealerCost;
    private int bulletCost;

    [NewOnSetup] private HashSet<byte> killedPlayers = null!;
    private int bulletCount = 1;
    private bool hasKnife;
    private bool hasShield;
    private bool hasRevealer;
    private int scrapAmount;

    private byte selectedShopItem = byte.MaxValue;
    private bool hasVoted;

    private Cooldown knifeCooldown = null!;

    private ShopItem[] shopItems;
    private ShopItem[] currentShopItems;

    public override bool TasksApplyToTotal() => false;

    [UIComponent(UI.Counter, ViewMode.Absolute, GameState.InMeeting)]
    private string DisableTaskCounter() => "";

    [UIComponent(UI.Counter, gameStates: GameState.Roaming)]
    private string BulletCounter()
    {
        string counter = RoleUtils.Counter(bulletCount, color: _bulletColor);
        return hasKnife ? counter : $"<s>{counter}</s>";
    }

    [UIComponent(UI.Text, gameStates: GameState.Roaming)]
    private string ScrapIndicator()
    {
        if (hasRevealer) return RoleColor.Colorize("Role Revealer Ready") + " " + _colorizedScrap.Formatted(scrapAmount);
        return _colorizedScrap.Formatted(scrapAmount) +  (knifeCooldown.IsReady() ? "" : Color.white.Colorize(" (" + knifeCooldown + "s)"));
    }
    
    [UIComponent(UI.Name, ViewMode.Absolute, GameState.InMeeting)]
    private string CustomNameIndicator()
    {
        currentShopItems = shopItems.Where(si => si.Enabled && si.Cost <= scrapAmount).ToArray();
        return currentShopItems.Select(si => si.ToDisplay()).Fuse("\n");
    }

    protected override void PostSetup()
    {
        VentCooldown = 0f;
        VentDuration = 120f;
        _colorizedScrap = TranslationUtil.Colorize("Scrap::0: {0}", ScrapColor);
        shopItems = new ShopItem[]
        {
            new("Scrap Knife", _knifeColor, modifyShopCosts ? knifeCost : 5, true, () =>
            {
                hasKnife = true;
                shopItems[1].Enabled = true;
                shopItems[0].Enabled = false;
            }),
            new("Bullet", _bulletColor, modifyShopCosts ? bulletCost : 1, false, () => bulletCount++),
            new("Scrap Shield", _shieldColor, modifyShopCosts ? shieldCost : 3, true, () => hasShield = true),
            new("Role Revealer", _revealerColor, modifyShopCosts ? revealerCost : 9, true, () => hasRevealer = true)
        };
        if (knifeCooldown.Duration <= -1) knifeCooldown.Duration = AUSettings.KillCooldown();
        MyPlayer.NameModel().GCH<RoleHolder>().First().GameStates()[2] = GameState.Roaming;
    }

    [RoleAction(LotusActionType.RoundStart)]
    private void RoundStart()
    {
        hasVoted = false;
        selectedShopItem = byte.MaxValue;
        shopItems[0].Enabled = !hasKnife;
        shopItems[1].Enabled = hasKnife;
    }

    [RoleAction(LotusActionType.RoundEnd)]
    private void RoundEndMessage() => GetChatHandler().Message("ShopMessage").Send();

    [RoleAction(LotusActionType.OnPet)]
    private void KillWithKnife()
    {
        if (hasRevealer)
        {
            HandleReveal();
            return;
        }

        if (!hasKnife) return;
        if (bulletCount <= 0 || knifeCooldown.NotReady()) return;
        PlayerControl? closestPlayer = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault(p => Relationship(p) is not Relation.FullAllies);
        if (closestPlayer == null) return;
        bulletCount--;
        knifeCooldown.Start();
        MyPlayer.InteractWith(closestPlayer, LotusInteraction.FatalInteraction.Create(this));
        killedPlayers.Add(closestPlayer.PlayerId);
    }
/*
    [RoleAction(LotusActionType.ReportBody)]
    private void OnReportBody(GameData.PlayerInfo deadPlayer)
    {
        if (!killedPlayers.Contains(deadPlayer.PlayerId)) scrapAmount += scrapFromBodies;
    }
*/
    [RoleAction(LotusActionType.Vote)]
    private void HandleVoting(Optional<PlayerControl> player, MeetingDelegate meetingDelegate, ActionHandle handle)
    {
        player.Handle(p =>
        {
            if (p.PlayerId == MyPlayer.PlayerId) HandleSelfVote(handle);
            else if (!hasVoted)
            {
                handle.Cancel();
                meetingDelegate.CastVote(MyPlayer, player);
            }
        }, () => HandleSkip(handle));
    }

    [RoleAction(LotusActionType.Interaction)]
    private void HandleInteraction(Interaction interaction, ActionHandle handle)
    {
        switch (interaction.Intent)
        {
            case IFatalIntent:
                if (Relationship(interaction.Emitter()) is Relation.FullAllies) handle.Cancel();
                break;
            case IHostileIntent:
                if (Relationship(interaction.Emitter()) is Relation.FullAllies) handle.Cancel();
                return;
        }

        if (!hasShield) return;
        hasShield = false;
        switch (interaction)
        {
            case DelayedInteraction:
            case IndirectInteraction:
            case ManipulatedInteraction:
            case RangedInteraction:
            case Transporter.TransportInteraction:
            case LotusInteraction:
                handle.Cancel();
                break;
        }
    }

    private void HandleReveal()
    {
        PlayerControl? closestPlayer = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault(p => Relationship(p) is not Relation.FullAllies);
        if (closestPlayer == null) return;
        hasRevealer = false;
        closestPlayer.NameModel().GCH<RoleHolder>().LastOrDefault()?.AddViewer(MyPlayer);
    }

    protected override void OnTaskComplete(Optional<NormalPlayerTask> playerTask)
    {
        scrapAmount += playerTask.Map(pt => pt.Length is NormalPlayerTask.TaskLength.Long ? 2 : 1).OrElse(1);
        if (HasAllTasksComplete && refreshTasks) AssignAdditionalTasks();

    }

    private void HandleSelfVote(ActionHandle handle)
    {
        if (!hasVoted) return;
        if (currentShopItems.Length == 0) return;
        handle.Cancel();
        if (selectedShopItem == byte.MaxValue) selectedShopItem = 0;
        else selectedShopItem++;
        if (selectedShopItem >= currentShopItems.Length) selectedShopItem = 0;
        ShopItem item = currentShopItems[selectedShopItem];
        //GetChatHandler().Message(SelectedItemMessage.Formatted(item.Name, scrapAmount - item.Cost)).Send();
    }

    private void HandleSkip(ActionHandle handle)
    {
        if (selectedShopItem == byte.MaxValue) return;
        if (selectedShopItem >= currentShopItems.Length)
        {
            handle.Cancel();
            return;
        }
        ShopItem item = currentShopItems[selectedShopItem];
        scrapAmount -= item.Cost;
        if (item.Color != _knifeColor && scrapAmount > 0 && hasVoted) handle.Cancel();
        //GetChatHandler().Message(PurchaseItemMessage.Formatted(item.Name, scrapAmount)).Send();
        item.Action();
        currentShopItems = shopItems.Where(si => si.Enabled && si.Cost <= scrapAmount).ToArray();
    }

    private ChatHandler GetChatHandler() => ChatHandler.Of(title: RoleColor.Colorize(RoleName)).Player(MyPlayer).LeftAlign();


    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Modify Shop Costs")//, ModifyShopCosts)
                .AddOnOffValues(false)
                .BindBool(b => modifyShopCosts = b)
                .ShowSubOptionPredicate(b => (bool)b)
                .SubOption(sub2 => sub2.Name("Knife Cost")//, KnifeCost)
                    .AddIntRange(0, 20, 1, 3)
                    .BindInt(i => knifeCost = i)
                    .Build())
                .SubOption(sub2 => sub2.Name("Bullet Cost")//, BulletCost)
                    .AddIntRange(0, 20, 1, 0)
                    .BindInt(i => bulletCost = i)
                    .Build())
                .SubOption(sub2 => sub2.Name("Shield Cost")//, ShieldCost)
                    .AddIntRange(0, 20, 1, 6)
                    .BindInt(i => shieldCost = i)
                    .Build())
                .SubOption(sub2 => sub2.Name("Revealer Cost")//, RoleRevealerCost)
                    .AddIntRange(0, 20, 1, 10)
                    .BindInt(i => revealerCost = i)
                    .Build())
                .Build())
            .SubOption(sub => sub.Name("Starts Game WIth Knife")//, StartsGameWithKnife)
                .AddOnOffValues(false)
                .BindBool(b => hasKnife = b)
                .Build())
            .SubOption(sub => sub.Name("Knife Cooldown")//, KnifeCooldown)
                .Value(v =>  v.Text(GeneralOptionTranslations.GlobalText).Color(new Color(1f, 0.61f, 0.33f)).Value(-1f).Build())
                .AddFloatRange(0, 120, 2.5f, 0, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(knifeCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Scrap from Reporting Bodies")//, ScrapFromReporting)
                .AddIntRange(0, 10, 1, 2)
                .BindInt(i => scrapFromBodies = i)
                .Build())
            .SubOption(sub => sub.Name("Refresh Tasks When All Complete")//, RefreshTasks)
                .AddOnOffValues()
                .BindBool(b => refreshTasks = b)
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(0.6f, 0.6f, 0.6f))
            .Faction(FactionInstances.Neutral)
            .RoleAbilityFlags(RoleAbilityFlag.IsAbleToKill)
            .SpecialType(SpecialType.Neutral)
            .OptionOverride(Override.CrewLightMod, () => AUSettings.ImpostorLightMod());
/*
    [Localized(nameof(Mafioso))]
    internal static class MafiaTranslations
    {
        [Localized(nameof(ScrapText))]
        public static string ScrapText = "Scrap::0: {0}";

        [Localized(nameof(KnifeItem))]
        public static string KnifeItem = "Scrap Knife";

        [Localized(nameof(ShieldItem))]
        public static string ShieldItem = "Scrap Shield";

        [Localized(nameof(BulletItem))]
        public static string BulletItem = "Bullet";

        [Localized(nameof(RevealerItem))]
        public static string RevealerItem = "Role Revealer";

        [Localized(nameof(RevealerReady))]
        public static string RevealerReady = "Role Revealer Ready";

        [Localized(nameof(ShopMessage))]
        public static string ShopMessage =
            "You are a member of the Mafia! You can purchase items during meetings. To purchase an item, first vote yourself until that item is selected. Then skip to continue.\nVoting for ANY OTHER player will count as your vote for that player, otherwise you will still remain in shop mode.";

        [Localized(nameof(SelectedItemMessage))]
        public static string SelectedItemMessage = "You have selected to purchase {0}. Purchasing this will leave you with {1} scrap. Press the skip vote button to continue your purchase.";

        [Localized(nameof(PurchaseItemMessage))]
        public static string PurchaseItemMessage = "You have purchased: {0}. You now have {1} scrap leftover.";

        [Localized(ModConstants.Options)]
        internal static class MafiaOptionTranslations
        {
            [Localized(nameof(RefreshTasks))]
            public static string RefreshTasks = "Refresh Tasks When All Complete";

            [Localized(nameof(StartsGameWithKnife))]
            public static string StartsGameWithKnife = "Starts Game With Knife";

            [Localized(nameof(ModifyShopCosts))]
            public static string ModifyShopCosts = "Modify Shop Costs";

            [Localized(nameof(KnifeCost))]
            public static string KnifeCost = "Knife Cost";

            [Localized(nameof(BulletCost))]
            public static string BulletCost = "Bullet Cost";

            [Localized(nameof(ShieldCost))]
            public static string ShieldCost = "Shield Cost";

            [Localized(nameof(RoleRevealerCost))]
            public static string RoleRevealerCost = "Role Revealer Cost";

            [Localized(nameof(KnifeCooldown))]
            public static string KnifeCooldown = "Knife Cooldown";

            [Localized(nameof(ScrapFromReporting))]
            public static string ScrapFromReporting = "Scrap from Reporting Bodies";
        }
    }
*/
    private class ShopItem
    {
        public string Name;
        public Color Color;
        public int Cost;
        public bool Enabled;
        public Action Action;

        public ShopItem(string name, Color color, int cost, bool enabled, Action action)
        {
            Name = name;
            Color = color;
            Cost = cost;
            Enabled = enabled;
            Action = action;
        }

        public string ToDisplay() => $"{Color.Colorize(Name)} ({ScrapColor.Colorize(Cost.ToString())})";
    }
}