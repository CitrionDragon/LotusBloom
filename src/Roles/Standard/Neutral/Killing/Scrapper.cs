using System;
using System.Collections.Generic;
using System.Linq;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Vanilla;
using Lotus.API.Vanilla.Meetings;
using Lotus.API.Vanilla.Sabotages;
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
using VentLib.Localization;
using Lotus.Managers.History.Events;
using VentLib.Utilities.Collections;
using Lotus.API.Player;
using Lotus.API.Reactive.HookEvents;
using MS.Internal.Xml.XPath;
using System.Diagnostics.Tracing;
using Lotus.Options.General;
using Lotus.Roles.Interfaces;
using Lotus.GameModes.Standard;
using System.ComponentModel;
using Lotus.Victory.Conditions;
//using static Lotus.Roles.RoleGroups.Impostors.Mafioso.MafiaTranslations;
//using static Lotus.Roles.RoleGroups.Impostors.Mafioso.MafiaTranslations.MafiaOptionTranslations;

namespace LotusBloom.Roles.Standard.Neutral.Killing;

public class Scrapper: Engineer, ISabotagerRole
{

    internal static Color ScrapColor = new(0.6f, 0.6f, 0.6f);
    private static Color _knifeColor = new(0.55f, 0.28f, 0.16f);
    private static Color _shieldColor = new(1f, 0.9f, 0.3f);
    private static Color _spikedColor = new(0.37f, 0.37f, 0.37f);
    private static Color _radioColor = ModConstants.Palette.GeneralColor5;

    private static string _colorizedScrap;
    private bool modifyShopCosts;
    private bool adrenaline;
    private bool laser;
    private bool refreshTasks;

    private int scrapFromBodies;
    private int knifeCost;
    private int shieldCost;
    private int radioCost;
    private int spikedCost;
    private int laserCost;
    private int adrenalineCost;

    [NewOnSetup] private HashSet<byte> killedPlayers = null!;
    private bool spiked;
    private bool hasKnife;
    private bool hasShield;
    private bool hasRadio;
    private bool hasLaser;
    private bool hasAdrenaline;
    private bool ultimate;
    private int scrapAmount;
    private int kills;
    private bool adrenalineReset;
    private float cooldowndecrease;
    private float speedGainPerKill;
    private float originalCooldown;
    private Remote<GameOptionOverride>? remote;
    private bool preciseShooting;
    private Vector2 startingLocation;

    public static string ShopMessage =
        "You can craft items during meetings. Only items you have enough scrap for will show. To craft an item, first vote yourself until that item is selected. Then skip to continue.\nVoting for ANY OTHER player will count as your vote for that player, otherwise you will still remain in shop mode.";
    public static string SelectedItemMessage = "You have selected to craft {0}. Crafting this will leave you with {1} scrap. Press the skip vote button to continue your crafting.";
    public static string PurchaseItemMessage = "You have crafted: {0}. You now have {1} scrap leftover.";

    private byte selectedShopItem = byte.MaxValue;
    private bool hasVoted;

    private Cooldown knifeCooldown = null!;
    private Cooldown radioCooldown = null!;
    private ShopItem[] shopItems;
    private ShopItem[] currentShopItems;

    public override bool TasksApplyToTotal() => false;

    [UIComponent(UI.Counter, ViewMode.Absolute, GameState.InMeeting)]
    private string DisableTaskCounter() => "";
/*
    [UIComponent(UI.Counter, gameStates: GameState.Roaming)]
    private string SpikedCounter()
    {
        string counter = RoleUtils.Counter(spikedCount, color: _spikedColor);
        return hasKnife ? counter : $"<s>{counter}</s>";
    }
*/
    [UIComponent(UI.Text, gameStates: GameState.Roaming)]
    private string ScrapIndicator()
    {
        //if (hasRadio) return RoleColor.Colorize("Radio Ready") + " " + _colorizedScrap.Formatted(scrapAmount);
        return (radioCooldown.IsReady() ? "" : Color.yellow.Colorize(" (" + radioCooldown + "s)")) + _colorizedScrap.Formatted(scrapAmount) + (knifeCooldown.IsReady() ? "" : Color.red.Colorize(" (" + knifeCooldown + "s)"));
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
            new("Nothing", Color.gray, 0, true, () =>shopItems[0].Enabled = true),
            new("Scrap Knife", _knifeColor, modifyShopCosts ? knifeCost : 5, true, () =>
            {
                hasKnife = true;
                shopItems[1].Enabled = false;
            }),
            new("Spiked Shield", _spikedColor, modifyShopCosts ? spikedCost : 3, false, () => spiked = true),
            new("Scrap Shield", _shieldColor, modifyShopCosts ? shieldCost : 3, true, () => 
            {
                hasShield = true;
                shopItems[2].Enabled = true;
            }),
            new("Radio", _radioColor, modifyShopCosts ? radioCost : 5, true, () => hasRadio = true),
            new("Scrap Laser", _radioColor, modifyShopCosts ? laserCost : 9, true, () => 
            {
                hasLaser = true;
                ultimate = true;
            }),
            new("Adrenaline Shot", _radioColor, modifyShopCosts ? adrenalineCost : 9, true, () => 
            {
                hasAdrenaline = true;
                ultimate = true;
            })
        };
        if (knifeCooldown.Duration <= -1) knifeCooldown.Duration = AUSettings.KillCooldown();
        originalCooldown=knifeCooldown.Duration;
        MyPlayer.NameModel().GCH<RoleHolder>().First().GameStates()[2] = GameState.Roaming;
        AdditiveOverride additiveOverride = new(Override.PlayerSpeedMod, () => kills * speedGainPerKill);
        remote = Game.MatchData.Roles.AddOverride(MyPlayer.PlayerId, additiveOverride);
    }

    [RoleAction(LotusActionType.RoundStart)]
    private void RoundStart()
    {
        hasVoted = false;
        if (hasKnife) knifeCooldown.Start();
        selectedShopItem = byte.MaxValue;
        shopItems[1].Enabled = !hasKnife;
        if (!ultimate)
        {
            shopItems[5].Enabled = laser;
            shopItems[6].Enabled = adrenaline;
        }
        else
        {
            shopItems[5].Enabled = false;
            shopItems[6].Enabled = false;
        }
        if (adrenalineReset)
        {
            kills=0;
            knifeCooldown.SetDuration(originalCooldown);
            MyPlayer.PrimaryRole().SyncOptions();
        }
    }

    [RoleAction(LotusActionType.RoundEnd)]
    private void RoundEndMessage() => GetChatHandler().Message(ShopMessage).Send();

    [RoleAction(LotusActionType.RoundEnd)]
    private void UpdateShop()
    {
        shopItems[2].Enabled = hasShield;
        shopItems[3].Enabled = !hasShield;
    }

    [RoleAction(LotusActionType.OnPet)]
    private void KillWithKnife()
    {
        if (!hasKnife) return;
        if (knifeCooldown.NotReady()) return;
        PlayerControl? closestPlayer = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault(p => Relationship(p) is not Relation.FullAllies);
        if (closestPlayer == null) return;
        if (hasAdrenaline)
        {
            kills++;
            knifeCooldown.SetDuration(originalCooldown-(kills*cooldowndecrease));
            MyPlayer.PrimaryRole().SyncOptions();
        }
        knifeCooldown.Start();
        MyPlayer.InteractWith(closestPlayer, LotusInteraction.FatalInteraction.Create(this));
        killedPlayers.Add(closestPlayer.PlayerId);
    }

    [RoleAction(LotusActionType.OnHoldPet)]
    private void StartSniping()
    {
        if (!hasLaser) return;
        startingLocation = MyPlayer.GetTruePosition();
        // DevLogger.Log($"Starting position: {startingLocation}");
    }
    [RoleAction(LotusActionType.OnPetRelease)]
    private bool FireBullet()
    {
        if (!hasLaser) return false;
        hasLaser=false;

        Vector2 targetPosition = (MyPlayer.GetTruePosition() - startingLocation).normalized;
        // DevLogger.Log($"Target Position: {targetPosition}");
        int kills = 0;

        foreach (PlayerControl target in Players.GetAllPlayers().Where(p => p.PlayerId != MyPlayer.PlayerId && p.Relationship(MyPlayer) is not Relation.FullAllies))
        {
            //DevLogger.Log(target.name);
            Vector3 targetPos = target.transform.position - (Vector3)MyPlayer.GetTruePosition();
            Vector3 targetDirection = targetPos.normalized;
            // DevLogger.Log($"Target direction: {targetDirection}");
            float dotProduct = Vector3.Dot(targetPosition, targetDirection);
            // DevLogger.Log($"Dot Product: {dotProduct}");
            float error = !preciseShooting ? targetPos.magnitude : Vector3.Cross(targetPosition, targetPos).magnitude;
            // DevLogger.Log($"Error: {error}");
            if (dotProduct < 0.98 || (error >= 1.0 && preciseShooting)) continue;
            float distance = Vector2.Distance(MyPlayer.transform.position, target.transform.position);
            InteractionResult result = MyPlayer.InteractWith(target, new RangedInteraction(new FatalIntent(true, () => new CustomDeathEvent(target, MyPlayer, ModConstants.DeathNames.Sniped)), distance, this));
            if (result is InteractionResult.Halt) continue;
            kills++;
            MyPlayer.RpcMark();
            //if (kills > 10 && 10 != -1) break;
        }

        return kills > 0;
    }
    [RoleAction(LotusActionType.VentEntered)]
    private void UseRadio()
    {
        if (!hasRadio||radioCooldown.NotReady()) return;
        radioCooldown.Start();
        List<ISabotage> randomSab = new List<ISabotage>();
        if (ShipStatus.Instance is AirshipStatus)
        {
            randomSab.Add(new HelicopterSabotage(MyPlayer));
            randomSab.Add(new ElectricSabotage(MyPlayer));
            randomSab.Add(new CommsSabotage(MyPlayer));
        }
        else if (ShipStatus.Instance.Type is ShipStatus.MapType.Ship)
        {
            randomSab.Add(new ElectricSabotage(MyPlayer));
            randomSab.Add(new CommsSabotage(MyPlayer));
            randomSab.Add(new ReactorSabotage(MyPlayer));
            randomSab.Add(new OxygenSabotage(MyPlayer));
        }
        else if (ShipStatus.Instance.Type is ShipStatus.MapType.Hq)
        {
            randomSab.Add(new ElectricSabotage(MyPlayer));
            randomSab.Add(new CommsSabotage(MyPlayer));
            randomSab.Add(new ReactorSabotage(MyPlayer));
            randomSab.Add(new OxygenSabotage(MyPlayer));
        }
        else if (ShipStatus.Instance.Type is ShipStatus.MapType.Pb)
        {
            randomSab.Add(new ElectricSabotage(MyPlayer));
            randomSab.Add(new CommsSabotage(MyPlayer));
            randomSab.Add(new ReactorSabotage(MyPlayer));
        }
        ISabotage sabotage = randomSab.GetRandom();
        SystemTypes system = SystemTypes.Sabotage;
        sabotage.CallSabotage(MyPlayer);
        //ISabotage sabotage1 = 
        //ShipStatus.UpdateSystem(SystemTypes.Electrical, MyPlayer, 1);
        
    }

    [RoleAction(LotusActionType.SabotageFixed)]
    private void GainScraponSab(ISabotage sabotage)
    {
        if (sabotage.SabotageType() is not SabotageType.Door) scrapAmount += scrapFromBodies;
    }

    [RoleAction(LotusActionType.Vote)]
    private void HandleVoting(Optional<PlayerControl> player, MeetingDelegate meetingDelegate, ActionHandle handle)
    {
        player.Handle(p =>
        {
            if (p.PlayerId == MyPlayer.PlayerId) HandleSelfVote(handle);
            else if (!hasVoted)
            {
                hasVoted=true;
                //handle.Cancel();
                //meetingDelegate.CastVote(MyPlayer, player);
            }
        }, () => HandleSkip(handle));
    }

    [RoleAction(LotusActionType.Interaction)]
    private void HandleInteraction(PlayerControl actor, Interaction interaction, ActionHandle handle)
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
        if (!spiked) return;
        spiked = false;
        IDeathEvent deathEvent = new CustomDeathEvent(MyPlayer, actor, ModConstants.DeathNames.Parried);
        MyPlayer.InteractWith(actor, new LotusInteraction(new FatalIntent(interaction is not LotusInteraction, () => deathEvent), this));
    }

    protected override void OnTaskComplete(Optional<NormalPlayerTask> playerTask)
    {
        scrapAmount += playerTask.Map(pt => pt.Length is NormalPlayerTask.TaskLength.Long ? 2 : 1).OrElse(1);
        if (HasAllTasksComplete && refreshTasks) AssignAdditionalTasks();
    }

    private void HandleSelfVote(ActionHandle handle)
    {
        if (hasVoted) return;
        if (currentShopItems.Length == 0) return;
        handle.Cancel();
        if (selectedShopItem == byte.MaxValue) selectedShopItem = 0;
        else selectedShopItem++;
        if (selectedShopItem >= currentShopItems.Length) selectedShopItem = 0;
        ShopItem item = currentShopItems[selectedShopItem];
        GetChatHandler().Message(SelectedItemMessage.Formatted(item.Name, scrapAmount - item.Cost)).Send();
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
        GetChatHandler().Message(PurchaseItemMessage.Formatted(item.Name, scrapAmount)).Send();
        item.Action();
        currentShopItems = shopItems.Where(si => si.Enabled && si.Cost <= scrapAmount).ToArray();
    }

    private ChatHandler GetChatHandler() => ChatHandler.Of(title: RoleColor.Colorize(RoleName)).Player(MyPlayer).LeftAlign();

    public bool CanSabotage() => true;

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
                .SubOption(sub2 => sub2.Name("Shield Cost")//, ShieldCost)
                    .AddIntRange(0, 20, 1, 6)
                    .BindInt(i => shieldCost = i)
                    .Build())
                .SubOption(sub2 => sub2.Name("Spiked Shield Cost")//, SpikedCost)
                    .AddIntRange(0, 20, 1, 0)
                    .BindInt(i => spikedCost = i)
                    .Build())
                .SubOption(sub2 => sub2.Name("Radio Cost")//, RoleRadioCost)
                    .AddIntRange(0, 20, 1, 10)
                    .BindInt(i => radioCost = i)
                    .Build())
                .SubOption(sub2 => sub2.Name("Scrap Laser Cost")//, RoleRadioCost)
                    .AddIntRange(0, 20, 1, 10)
                    .BindInt(i => laserCost = i)
                    .Build())
                .SubOption(sub2 => sub2.Name("Adrenaline Cost")//, RoleRadioCost)
                    .AddIntRange(0, 20, 1, 10)
                    .BindInt(i => adrenalineCost = i)
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
            .SubOption(sub => sub.Name("Radio Cooldown")
                //.Value(v =>  v.Text(GeneralOptionTranslations.GlobalText).Color(new Color(1f, 0.61f, 0.33f)).Value(-1f).Build())
                .AddFloatRange(0, 120, 2.5f, 0, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(radioCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Scrap Laser")
                .AddOnOffValues(false)
                .BindBool(b => laser = b)
                .ShowSubOptionPredicate(b => (bool)b)
                .SubOption(sub => sub.Name("Precise Shooting")
                    .AddOnOffValues(false)
                    .BindBool(b => preciseShooting = b)
                    .Build())
                .Build())
            .SubOption(sub => sub.Name("Adrenaline")
                .AddOnOffValues(false)
                .BindBool(b => adrenaline = b)
                .ShowSubOptionPredicate(b => (bool)b)
                .SubOption(sub => sub.Name("Cooldown Decrease per Kill")
                    //.Value(v =>  v.Text(GeneralOptionTranslations.GlobalText).Color(new Color(1f, 0.61f, 0.33f)).Value(-1f).Build())
                    .AddFloatRange(0, 30, 0.5f, 5, GeneralOptionTranslations.SecondsSuffix)
                    .BindFloat(v => cooldowndecrease = v)
                    .Build())
                .SubOption(sub => sub.Name("Additional Speed per Kill")//, Translations.Options.SpeedGainPerKill)
                    .AddFloatRange(0.1f, 1f, 0.1f, 3)
                    .BindFloat(f => speedGainPerKill = f)
                    .Build())
                .SubOption(sub => sub.Name("Adrenaline Reset on Meeting")
                    .AddOnOffValues(false)
                    .BindBool(b => adrenalineReset = b)
                    .Build())
                .Build())
            .SubOption(sub => sub.Name("Scrap from Fixing Sabotages")//, ScrapFromReporting)
                .AddIntRange(0, 10, 1, 2)
                .BindInt(i => scrapFromBodies = i)
                .Build())
            .SubOption(sub => sub.Name("Refresh Tasks When All Complete")//, RefreshTasks)
                .AddOnOffValues()
                .BindBool(b => refreshTasks = b)
                .Build());
//Radio, Ultimates (Landmine, Laser, Adrenaline)
    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(0.6f, 0.6f, 0.6f))
            .Faction(FactionInstances.Neutral)
            .RoleAbilityFlags(RoleAbilityFlag.IsAbleToKill)
            .SpecialType(SpecialType.NeutralKilling)
            .OptionOverride(Override.CrewLightMod, () => AUSettings.ImpostorLightMod());

    [Localized(nameof(Scrapper))]
    internal static class ScrapperTranslations
    {
        [Localized(nameof(ScrapText))]
        public static string ScrapText = "Scrap::0: {0}";

        [Localized(nameof(KnifeItem))]
        public static string KnifeItem = "Scrap Knife";

        [Localized(nameof(ShieldItem))]
        public static string ShieldItem = "Scrap Shield";

        [Localized(nameof(SpikedItem))]
        public static string SpikedItem = "Spiked";

        [Localized(nameof(RadioItem))]
        public static string RadioItem = "Radio";

        [Localized(nameof(RadioReady))]
        public static string RadioReady = "Radio Ready";

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

            [Localized(nameof(SpikedCost))]
            public static string SpikedCost = "Spiked Cost";

            [Localized(nameof(ShieldCost))]
            public static string ShieldCost = "Shield Cost";

            [Localized(nameof(RoleRadioCost))]
            public static string RoleRadioCost = "Role Radio Cost";

            [Localized(nameof(KnifeCooldown))]
            public static string KnifeCooldown = "Knife Cooldown";

            [Localized(nameof(ScrapFromReporting))]
            public static string ScrapFromReporting = "Scrap from Reporting Bodies";
        }
    }

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