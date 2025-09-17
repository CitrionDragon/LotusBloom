using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Il2CppSystem.Text;
using Lotus.API;
using Lotus.API.Odyssey;
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
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using VentLib.Options.UI;
using Lotus.Managers.History.Events;
using Lotus.Managers;
using VentLib.Utilities.Collections;
using Lotus.API.Player;
using Lotus.Factions.Interfaces;
using Lotus.GUI.Name.Components;
using Lotus.Roles.Interfaces;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.GUI;
using Lotus.Roles.Internals.Trackers;
using Lotus.Roles.Subroles;
using Lotus.RPC.CustomObjects.Interfaces;
using Lotus.Victory.Conditions;
using LotusBloom.RPC;
using VentLib;
using VentLib.Networking.RPC.Attributes;
using CollectionExtensions = HarmonyLib.CollectionExtensions;

//using static Lotus.Roles.RoleGroups.Impostors.Mafioso.MafiaTranslations;
//using static Lotus.Roles.RoleGroups.Impostors.Mafioso.MafiaTranslations.MafiaOptionTranslations;

namespace LotusBloom.Roles.Standard.Neutral.Killing;

public class Scrapper: Engineer, ISabotagerRole, IRoleUI
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
    private int scrapFromTasks;
    private int scrapFromBodies;
    private int knifeCost;
    private int shieldCost;
    private int radioCost;
    private int spikedCost;
    private int laserCost;
    private int adrenalineCost;

    [NewOnSetup] private HashSet<byte> killedPlayers = null!;
    [NewOnSetup] private FixedUpdateLock fixedUpdateLock = new(ModConstants.RoleFixedUpdateCooldown);
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
    private Remote<GameOptionOverride> remote;
    private bool preciseShooting;
    private Vector2 startingLocation;
    private bool aiming;

    public const string LeftArrow = "<----";
    public const string RightArrow = "--->";
    public const string NoRoleHere = "----------";
    public const float ShiftTimer = 15f;
    private bool currentlyGuessing;
    private bool isShapeshifterRole;
    private bool wasShiftingRole;
    private bool meetingEnded=true;
    private int currentPageNum;
    [NewOnSetup(true)] private MeetingPlayerSelector voteSelector = new();
    [NewOnSetup] private List<GuesserShapeshifterObject> shapeshifterObjects = [];
    
    private RoleTypes? originalRoleType;

    private IFaction lastFaction;
    private Cooldown shiftTimer;
    
    private byte selectedShopItem = byte.MaxValue;
    private bool hasVoted;

    private Cooldown knifeCooldown = null!;
    private Cooldown radioCooldown = null!;
    private bool sabotageOn = false;
    private ShopItem[] shopItems;
    private ShopItem[] currentShopItems;

    public override bool TasksApplyToTotal() => false;

    [UIComponent(UI.Counter, ViewMode.Absolute, GameState.InMeeting)]
    private string DisableTaskCounter() => "";
    
    [UIComponent(UI.Counter, ViewMode.Absolute, GameState.Roaming)]
    private string DisableTaskCounter2() => "";

    [UIComponent(UI.Text, gameStates: GameState.Roaming)]
    private string ScrapIndicator()
    {
        if (!hasRadio) return _colorizedScrap.Formatted(scrapAmount)+ (knifeCooldown.IsReady() ? "" : Color.red.Colorize(" (" + knifeCooldown + "s)"));
        return (radioCooldown.IsReady()&&!sabotageOn ? Color.yellow.Colorize("◈") : Color.yellow.Colorize("◇")) + _colorizedScrap.Formatted(scrapAmount) + (knifeCooldown.IsReady() ? "" : Color.red.Colorize(" (" + knifeCooldown + "s)"));
    }

    [UIComponent(UI.Text, gameStates: GameState.Roaming)]
    private string LaserIndicator()
    {
        if (!hasLaser) return "";
        return aiming ? Color.red.Colorize("Aiming") : Color.red.Colorize("Laser Ready");
    }

    protected override void PostSetup()
    {
        LastResort.IncompatibleRoles.Add(typeof(Scrapper));
        VentCooldown = 0f;
        VentDuration = 120f;
        radioCooldown.SetDuration(30);
        _colorizedScrap = TranslationUtil.Colorize("Scrap::0: {0}", ScrapColor);
        shopItems = new ShopItem[]
        {
            new("Scrap Knife", _knifeColor, modifyShopCosts ? knifeCost : 5, true, () =>
            {
                hasKnife = true;
                shopItems[0].Enabled = false;
            }),
            new("Spiked Shield", _spikedColor, modifyShopCosts ? spikedCost : 3, false, () =>
            {
                spiked = true;
                shopItems[1].Enabled = false;
            }),
            new("Scrap Shield", _shieldColor, modifyShopCosts ? shieldCost : 3, true, () =>
            {
                hasShield = true;
                shopItems[1].Enabled = true;
                shopItems[2].Enabled = false;
            }),
            new("Radio", _radioColor, modifyShopCosts ? radioCost : 5, true, () =>
            {
                hasRadio = true;
                shopItems[3].Enabled = false;
            }),
            new("Scrap Laser", _radioColor, modifyShopCosts ? laserCost : 9, true, () => 
            {
                hasLaser = true;
                ultimate = true;
                shopItems[4].Enabled = false;
                shopItems[5].Enabled = false;
            }),
            new("Adrenaline Shot", _radioColor, modifyShopCosts ? adrenalineCost : 9, true, () => 
            {
                hasAdrenaline = true;
                ultimate = true;
                shopItems[4].Enabled = false;
                shopItems[5].Enabled = false;
            }),
            new("End Crafting", Color.gray, 0, true, () => 
            {
                hasVoted = true;
                GetChatHandler().Message(ScrapperTranslations.CanVoteOtherPlayersOnItem).Send(MyPlayer);
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
        shopItems[0].Enabled = !hasKnife;
        if (!ultimate)
        {
            shopItems[4].Enabled = laser;
            shopItems[5].Enabled = adrenaline;
        }
        else
        {
            shopItems[4].Enabled = false;
            shopItems[5].Enabled = false;
        }
        if (adrenalineReset)
        {
            kills=0;
            knifeCooldown.SetDuration(originalCooldown);
            MyPlayer.PrimaryRole().SyncOptions();
        }
    }

    [RoleAction(LotusActionType.RoundEnd)]
    private void UpdateShop()
    {
        currentShopItems = shopItems.Where(si => si.Enabled && si.Cost <= scrapAmount).ToArray();
        shopItems[1].Enabled = hasShield&!spiked;
        shopItems[2].Enabled = !hasShield;
        meetingEnded = false;
        GetChatHandler().Message(ScrapperTranslations.ShopMessage).Send();
    }

    [RoleAction(LotusActionType.OnPet)]
    private void KillWithKnife()
    {
        if (!hasKnife&&!aiming) return;
        if (aiming) 
        {
            FireBullet();
            return;
        }
        if (knifeCooldown.NotReady()) return;
        PlayerControl closestPlayer = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault(p => Relationship(p) is not Relation.FullAllies);
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
        aiming=true;
        // DevLogger.Log($"Starting position: {startingLocation}");
    }
    //[RoleAction(LotusActionType.OnPetRelease)]
    private bool FireBullet()
    {
        if (!hasLaser) return false;
        hasLaser=false;

        Vector2 targetPosition = (MyPlayer.GetTruePosition() - startingLocation).normalized;
        // DevLogger.Log($"Target Position: {targetPosition}");
        int kills = 0;

        foreach (PlayerControl target in Players.GetAllPlayers().Where(p => p.PlayerId != MyPlayer.PlayerId && p.Relationship(MyPlayer) is not Relation.FullAllies && p.IsAlive()))
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
        if (!hasRadio) return;
        List<byte> randomSab = new List<byte>();
        if (ShipStatus.Instance is AirshipStatus)
        {
            randomSab.Add((byte)SystemTypes.HeliSabotage);
            randomSab.Add((byte)SystemTypes.Electrical);
            randomSab.Add((byte)SystemTypes.Comms);
        }
        else if (ShipStatus.Instance.Type is ShipStatus.MapType.Ship)
        {
            randomSab.Add((byte)SystemTypes.Electrical);
            randomSab.Add((byte)SystemTypes.Comms);
            randomSab.Add((byte)SystemTypes.Reactor);
            randomSab.Add((byte)SystemTypes.LifeSupp);
        }
        else if (ShipStatus.Instance.Type is ShipStatus.MapType.Hq)
        {
            randomSab.Add((byte)SystemTypes.Electrical);
            randomSab.Add((byte)SystemTypes.Comms);
            randomSab.Add((byte)SystemTypes.Reactor);
            randomSab.Add((byte)SystemTypes.LifeSupp);
        }
        else if (ShipStatus.Instance.Type is ShipStatus.MapType.Pb)
        {
            randomSab.Add((byte)SystemTypes.Electrical);
            randomSab.Add((byte)SystemTypes.Comms);
            randomSab.Add((byte)SystemTypes.Reactor);
        }
        ShipStatus.Instance.UpdateSystem(SystemTypes.Sabotage, MyPlayer, randomSab.GetRandom());
    }

    [RoleAction(LotusActionType.SabotageFixed)]
    private void GainScraponSab(ISabotage sabotage)
    {
        if (sabotage.SabotageType() is not SabotageType.Door) scrapAmount += scrapFromBodies;
    }

    [RoleAction(LotusActionType.SabotageStarted, ActionFlag.GlobalDetector)]
    private void DisableRadio(ISabotage sabotage)
    {
        if (sabotage.SabotageType() is not SabotageType.Door) sabotageOn = true;
    }

    [RoleAction(LotusActionType.SabotageFixed, ActionFlag.GlobalDetector)]
    private void StartRadioCooldown(ISabotage sabotage)
    {
        if (sabotage.SabotageType() is SabotageType.Door) return;
        sabotageOn = false;
        radioCooldown.Start();
    }
    
    [RoleAction(LotusActionType.Shapeshift, priority:Priority.VeryHigh)]
    public void SelectRoleToGuess(PlayerControl target, ActionHandle handle)
    {
        if (MeetingHud.Instance) handle.Cancel(ActionHandle.CancelType.Soft);
        if (!currentlyGuessing || !isShapeshifterRole) return;

        var firstObject = shapeshifterObjects.FirstOrDefault(o => o.RealPlayer?.NetId == target.NetId || o.NetObject?.playerControl.NetId == target.NetId);
        if (firstObject == null) return;
        HandleShapeshifterChoice(firstObject);
    }

    [RoleAction(LotusActionType.Disconnect, ActionFlag.GlobalDetector)]
    public void FillDisconnects(PlayerControl target)
    {
        if (target.PlayerId == MyPlayer.PlayerId)
        {
            CollectionExtensions.Do(shapeshifterObjects, gso => gso.Delete());
            return;
        }

        var firstObject = shapeshifterObjects.FirstOrDefault(o => o.RealPlayer?.NetId == target.NetId || o.NetObject?.playerControl.NetId == target.NetId);
        if (firstObject == null) return;
        shiftTimer.Finish();
    }

    [RoleAction(LotusActionType.MeetingEnd, ActionFlag.WorksAfterDeath)]
    public void CheckRevive()
    {
        shiftTimer.Finish(true);
        meetingEnded = true;
        if (wasShiftingRole)
        {
            wasShiftingRole = false;
            isShapeshifterRole = false;
            MyPlayer.PrimaryRole().DesyncRole = originalRoleType;
            RoleTypes targetRole = MyPlayer.PrimaryRole().RealRole;
            if (!MyPlayer.IsAlive()) targetRole = targetRole.GhostEquivalent();
            MyPlayer.RpcSetRoleDesync(targetRole, MyPlayer);
            originalRoleType = null;
        }
        DeleteAllShapeshifterObjects();
    }

    private void StartCrafting()
    {
        isShapeshifterRole = true;
        currentPageNum = 1;
        currentlyGuessing = true;
        
        if (!wasShiftingRole)
        {
            wasShiftingRole = true;
            var mainRole = MyPlayer.PrimaryRole();
            if (mainRole.DesyncRole.HasValue) originalRoleType = mainRole.DesyncRole.Value;
            else originalRoleType = null;

            mainRole.DesyncRole = RoleTypes.Shapeshifter;
        }

        ResetShapeshfiterObjects();
        MyPlayer.RpcSetRoleDesync(RoleTypes.Shapeshifter, MyPlayer);
        if (!shiftTimer.IsCoroutine)
        {
            shiftTimer.IsCoroutine = true;
            //CooldownManager.SubmitCooldown(shiftTimer);
        }
        shiftTimer.StartThenRun(CancelGuessingTimerDelay, ShiftTimer);
        SendCnoRows();
    }
    
    private void CancelGuessingTimerDelay()
    {
        isShapeshifterRole = false;
        voteSelector.Reset();
        currentlyGuessing = false;
        selectedShopItem = byte.MaxValue;
        MyPlayer.RpcSetRoleDesync(RoleTypes.Crewmate, MyPlayer);
        GuesserMessage(ScrapperTranslations.KickedFromGuessing).Send(MyPlayer);
        DeleteAllShapeshifterObjects();
    }
    
    private void ResetShapeshfiterObjects()
    {
        DeleteAllShapeshifterObjects();

        int playerIndex = 0;
        foreach (PlayerControl player in Players.GetAlivePlayers())
        {
            if (player.PlayerId == MyPlayer.PlayerId) continue;
            shapeshifterObjects.Add(new GuesserShapeshifterObject(MyPlayer, playerIndex, GetNameFromIndex(playerIndex), player));
            playerIndex += 1;
        }

        int leftOverPlayers = 15 - playerIndex;
        for (int i = 0; i < leftOverPlayers; i++)
        {
            // Create a NET OBJECT instead of using a player.
            shapeshifterObjects.Add(new GuesserShapeshifterObject(MyPlayer, playerIndex, GetNameFromIndex(playerIndex), null));
            playerIndex += 1;
        }

        return;

        string GetNameFromIndex(int thisIndex)
        {
            switch (thisIndex)
            {
                case 0:
                    return Color.white.Colorize(LeftArrow);
                case 1:
                    return Color.white.Colorize(ScrapperTranslations.PageIndex.Formatted(currentPageNum, Mathf.CeilToInt(currentShopItems.Count() / 12f)));
                case 2:
                    return Color.white.Colorize(RightArrow);
                default:
                    int listIndex = thisIndex - 3;
                    listIndex = (currentPageNum - 1) * 12 + listIndex;
                    if (listIndex >= currentShopItems.Count()) return NoRoleHere;
                    return currentShopItems[listIndex].ToDisplay();
            }
        }
    }

    private void DeleteAllShapeshifterObjects()
    {
        shapeshifterObjects.ForEach(o => o.Delete());
        shapeshifterObjects = [];
    }

    private void HandleShapeshifterChoice(GuesserShapeshifterObject shiftableObject)
    {
        switch (shiftableObject.PlayerIndex)
        {
            case 0: // left
                int startPageNum = currentPageNum;
                currentPageNum -= 1;
                if (currentPageNum < 1) currentPageNum = Mathf.CeilToInt(currentShopItems.Count() / 12f);
                shiftTimer.StartThenRun(CancelGuessingTimerDelay, ShiftTimer);
                GuesserMessage(ScrapperTranslations.MoreTimeGiven).Send(MyPlayer);
                if (startPageNum != currentPageNum) ResetShapeshfiterObjects();
                SendCnoRows();
                break;
            case 1: // pressing page icon.
                break;
            case 2: // right
                int startPageNumRight = currentPageNum;
                currentPageNum += 1;
                int maxPages  = Mathf.CeilToInt(currentShopItems.Count() / 12f);
                if (currentPageNum > maxPages) currentPageNum = 1;
                shiftTimer.StartThenRun(CancelGuessingTimerDelay, ShiftTimer);
                GuesserMessage(ScrapperTranslations.MoreTimeGiven).Send(MyPlayer);
                if (startPageNumRight != currentPageNum) ResetShapeshfiterObjects();
                SendCnoRows();
                break;
            default:
                int playerIndex = shiftableObject.PlayerIndex;
                if (playerIndex > 14) return; // crowded detection.
                int listIndex = playerIndex - 3;
                listIndex = (currentPageNum - 1) * 12 + listIndex;
                if (listIndex >= currentShopItems.Count()) return;
                shiftTimer.Finish(true);
                DeleteAllShapeshifterObjects();
                ShopItem item = currentShopItems[listIndex];
                scrapAmount -= item.Cost;
                GetChatHandler().Message(ScrapperTranslations.PurchaseItemMessage.Formatted(item.Name, scrapAmount)).Send();
                item.Action();
                currentShopItems = shopItems.Where(si => si.Enabled && si.Cost <= scrapAmount).ToArray();
                isShapeshifterRole = false;
                currentlyGuessing = false;
                MyPlayer.RpcSetRoleDesync(RoleTypes.Crewmate, MyPlayer);
                break;
        }
    }

    private void SendCnoRows()
    {
        if (MyPlayer.AmOwner) return;
        StringBuilder stringBuilder = new();
        stringBuilder.Append(ScrapperTranslations.ShifterMenuHelpText);
        stringBuilder.AppendLine();

        int lastRow = -1;
        bool firstRole = true;
        shapeshifterObjects.ForEach(obj =>
        {
            if (!obj.IsCno()) return;
            int curRow = Mathf.CeilToInt((float)(obj.PlayerIndex + 1) / 3f);
            if (curRow != lastRow)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append(ScrapperTranslations.RowText.Formatted(curRow));
                firstRole = true;
            }
            lastRow = curRow;
            if (!firstRole) stringBuilder.Append(", ");
            stringBuilder.Append(curRow == 1 ? obj.GetText().RemoveHtmlTags() : obj.GetText());
            firstRole = false;
        });
        if (lastRow == -1) return;
        GuesserMessage(stringBuilder.ToString()).Send(MyPlayer);
    }

    [RoleAction(LotusActionType.Vote)]
    private void HandleVoting(Optional<PlayerControl> player, MeetingDelegate meetingDelegate, ActionHandle handle)
    {
        if (hasVoted) return;
        player.Handle(p =>
        {
            if (p.PlayerId == MyPlayer.PlayerId) HandleSelfVote(handle);
            else hasVoted=true;
        }, () => hasVoted=true);
    }

    private void HandleSelfVote(ActionHandle handle)
    {
        if (hasVoted) return;
        handle.Cancel();
        StartCrafting();
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
        scrapAmount += playerTask.Map(pt => pt.Length is NormalPlayerTask.TaskLength.Long ? scrapFromTasks*2 : scrapFromTasks).OrElse(scrapFromTasks);
        if (HasAllTasksComplete && refreshTasks) AssignAdditionalTasks();
    }

    [RoleAction(LotusActionType.FixedUpdate)]
    public void UpdateButton()
    {
        if (!fixedUpdateLock.AcquireLock()) return;
        if (!MyPlayer.IsModded()) return;
        if (!MyPlayer.AmOwner)
        {
            if (MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault() == null) Vents.FindRPC((uint)BloomModCalls.UpdateScrapper)?.Send([MyPlayer.OwnerId],false);
            else Vents.FindRPC((uint)BloomModCalls.UpdateScrapper)?.Send([MyPlayer.OwnerId],true);
            return;
        }
        RoleButton petButton = UIManager.PetButton;
        if (MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault() == null || !hasKnife)
        {
            if (hasLaser)
            {
                if (!aiming) petButton.SetText("Aim Laser");
                else petButton.SetText("Fire");
                petButton.SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/sniper_aim.png", 130, true))
                .SetMaterial(HudManager.Instance.SabotageButton.buttonLabelText.fontMaterial);
            }
            else petButton.RevertSprite().SetText("Pet").SetMaterial(HudManager.Instance.AbilityButton.buttonLabelText.fontMaterial);
            petButton.BindCooldown(null);
            petButton.GetButton().SetCoolDown(0, 1);
        }
        else
        {
            petButton.SetText("Kill")
            .BindCooldown(knifeCooldown)
            .SetSprite(() => AmongUsButtonSpriteReferences.KillButtonSprite);
            petButton.SetMaterial(HudManager.Instance.SabotageButton.buttonLabelText.fontMaterial);
        }
        RoleButton ventButton = UIManager.AbilityButton;
        if (!meetingEnded)
        {
            ventButton.SetText("Craft")
                .SetSprite(() => AmongUsButtonSpriteReferences.AbilityButtonSprite)
                .SetMaterial(HudManager.Instance.AbilityButton.buttonLabelText.fontMaterial);
        }
        else if (radioCooldown.IsReady() && !sabotageOn && hasRadio)
        {
            ventButton.SetText("Sabotage")
            .SetSprite(() => HudManager.Instance.SabotageButton.graphic.sprite)
            .SetMaterial(HudManager.Instance.SabotageButton.buttonLabelText.fontMaterial);
        }
        else
        {
            ventButton.SetText("Vent")
            .SetSprite(() => AmongUsButtonSpriteReferences.VentButtonSprite)
            .SetMaterial(HudManager.Instance.AbilityButton.buttonLabelText.fontMaterial);
        }
        ventButton.GetButton().SetCoolDown(0, 1);
    }

    private ChatHandler GetChatHandler() => ChatHandler.Of(title: RoleColor.Colorize(RoleName)).Player(MyPlayer).LeftAlign();

    public bool CanSabotage() => true;

    public RoleButton PetButton(IRoleButtonEditor abilityButton) => hasKnife && MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault() == null
        ? abilityButton.SetText("Kill")
            .BindCooldown(knifeCooldown)
            .SetSprite(() => AmongUsButtonSpriteReferences.KillButtonSprite)
        : abilityButton.Default(false).SetText("Pet");

    public RoleButton AbilityButton(IRoleButtonEditor abilityButton) => hasRadio
        ? abilityButton.SetText("Sabotage")
            .SetSprite(() => HudManager.Instance.SabotageButton.graphic.sprite)
        : abilityButton.SetText("Vent")
            .SetSprite(() => AmongUsButtonSpriteReferences.VentButtonSprite);

    [ModRPC((uint)BloomModCalls.UpdateScrapper, RpcActors.Host, RpcActors.NonHosts)]
    private static void UpdateScrapper(bool closeplayer)
    {
        Scrapper? scrapper = PlayerControl.LocalPlayer.PrimaryRole<Scrapper>();
        if (scrapper == null) return;
        RoleButton petButton = scrapper.UIManager.PetButton;
        if (!closeplayer || !scrapper.hasKnife)
        {
            if (scrapper.hasLaser)
            {
                if (!scrapper.aiming) petButton.SetText("Aim Laser");
                else petButton.SetText("Fire");
                petButton.SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/sniper_aim.png", 130, true))
                    .SetMaterial(HudManager.Instance.SabotageButton.buttonLabelText.fontMaterial);
            }
            else petButton.RevertSprite().SetText("Pet").SetMaterial(HudManager.Instance.AbilityButton.buttonLabelText.fontMaterial);
            petButton.BindCooldown(null);
            petButton.GetButton().SetCoolDown(0, 1);
        }
        else
        {
            petButton.SetText("Kill")
                .BindCooldown(scrapper.knifeCooldown)
                .SetSprite(() => AmongUsButtonSpriteReferences.KillButtonSprite);
            petButton.SetMaterial(HudManager.Instance.SabotageButton.buttonLabelText.fontMaterial);
        }
        RoleButton ventButton = scrapper.UIManager.AbilityButton;
        if (!scrapper.meetingEnded)
        {
            ventButton.SetText("Craft")
                .SetSprite(() => AmongUsButtonSpriteReferences.AbilityButtonSprite)
                .SetMaterial(HudManager.Instance.AbilityButton.buttonLabelText.fontMaterial);
        }
        else if (scrapper.radioCooldown.IsReady() && !scrapper.sabotageOn && scrapper.hasRadio)
        {
            ventButton.SetText("Sabotage")
                .SetSprite(() => HudManager.Instance.SabotageButton.graphic.sprite)
                .SetMaterial(HudManager.Instance.SabotageButton.buttonLabelText.fontMaterial);
        }
        else
        {
            ventButton.SetText("Vent")
                .SetSprite(() => AmongUsButtonSpriteReferences.VentButtonSprite)
                .SetMaterial(HudManager.Instance.AbilityButton.buttonLabelText.fontMaterial);
        }
        ventButton.GetButton().SetCoolDown(0, 1);
    }

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Neutrals.Killing.Scrapper";

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Modify Shop Costs")//, ModifyShopCosts)
                .AddBoolean(false)
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
                    .AddIntRange(0, 20, 1, 5)
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
                .AddBoolean(false)
                .BindBool(b => hasKnife = b)
                .Build())
            .SubOption(sub => sub.Name("Knife Cooldown")//, KnifeCooldown)
                .Value(v => v.Text(GeneralOptionTranslations.GlobalText).Color(new Color(1f, 0.61f, 0.33f)).Value(-1f).Build())
                .AddFloatRange(0, 120, 2.5f, 0, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(knifeCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Scrap Laser")
                .AddBoolean(false)
                .BindBool(b => laser = b)
                .ShowSubOptionPredicate(b => (bool)b)
                .SubOption(sub => sub.Name("Precise Shooting")
                    .AddBoolean(false)
                    .BindBool(b => preciseShooting = b)
                    .Build())
                .Build())
            .SubOption(sub => sub.Name("Adrenaline")
                .AddBoolean(false)
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
                    .AddBoolean(false)
                    .BindBool(b => adrenalineReset = b)
                    .Build())
                .Build())
            .SubOption(sub => sub.Name("Scrap from Doing Tasks")//, ScrapFromReporting)
                .AddIntRange(1, 5, 1, 2)
                .BindInt(i => scrapFromTasks = i)
                .Build())
            .SubOption(sub => sub.Name("Scrap from Fixing Sabotages")//, ScrapFromReporting)
                .AddIntRange(1, 10, 1, 2)
                .BindInt(i => scrapFromBodies = i)
                .Build())
            .SubOption(sub => sub.Name("Refresh Tasks When All Complete")//, RefreshTasks)
                .AddBoolean(true)
                .BindBool(b => refreshTasks = b)
                .Build());
//Ultimates (Landmine, Laser, Adrenaline)
    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(0.6f, 0.6f, 0.6f))
            .Faction(FactionInstances.Neutral)
            .RoleAbilityFlags(RoleAbilityFlag.IsAbleToKill | RoleAbilityFlag.UsesPet)
            .SpecialType(SpecialType.NeutralKilling)
            .OptionOverride(Override.CrewLightMod, () => AUSettings.ImpostorLightMod());
    
    private class GuesserShapeshifterObject
    {
        public GuesserShiftableObject? NetObject;
        public PlayerControl? RealPlayer;
        public int PlayerIndex;

        private PlayerControl guesser;
        private string currentName;
        private bool isCno;

        private Remote<NameComponent>? overridenName;
        private Remote<IndicatorComponent>? overridenIndicator;
        private Remote<RoleComponent>? overridenRole;
        private Remote<CounterComponent>? overridenCounter;
        private Remote<TextComponent>? overridenText;

        public GuesserShapeshifterObject(PlayerControl guesser, int index, string currentName, PlayerControl? vanillaPlayer)
        {
            PlayerIndex = index;
            this.guesser  = guesser;
            this.currentName = currentName;
            if (vanillaPlayer == null)
            {
                isCno = true;
                NetObject = new GuesserShiftableObject(currentName, new Vector2(100000, 10000), guesser.PlayerId);
                return;
            }
            RealPlayer = vanillaPlayer;

            var nameModel = vanillaPlayer.NameModel();
            overridenName = nameModel.GetComponentHolder<NameHolder>().Add(new NameComponent(new LiveString(() => currentName), GameState.InMeeting, ViewMode.Absolute, viewers:guesser));
            overridenIndicator = nameModel.GetComponentHolder<IndicatorHolder>().Add(new IndicatorComponent(new LiveString(string.Empty), GameState.InMeeting, ViewMode.Absolute, viewers: guesser));
            overridenRole = nameModel.GetComponentHolder<RoleHolder>().Add(new RoleComponent(new LiveString(string.Empty), [GameState.InMeeting], ViewMode.Absolute, viewers:guesser));
            overridenCounter = nameModel.GetComponentHolder<CounterHolder>().Add(new CounterComponent(new LiveString(string.Empty), [GameState.InMeeting], ViewMode.Absolute, viewers: guesser));
            overridenText = nameModel.GetComponentHolder<TextHolder>().Add(new TextComponent(new LiveString(string.Empty), GameState.InMeeting, ViewMode.Absolute, viewers: guesser));

            if (guesser.AmOwner) nameModel.RenderFor(guesser);
            // send at a DELAY so that guesser message doesn't clear the name.
            else Async.Schedule(() => nameModel.RenderFor(guesser, force: true), NetUtils.DeriveDelay(1f));

        }

        public void ChangeName(string newName)
        {
            currentName = newName;
            NetObject?.RpcChangeSprite(newName);
            RealPlayer?.NameModel().RenderFor(guesser);
        }

        public bool IsCno() => isCno;
        public string GetText() => currentName;

        public void Delete()
        {
            NetObject?.Despawn();
            overridenName?.Delete();
            overridenIndicator?.Delete();
            overridenCounter?.Delete();
            overridenText?.Delete();
            overridenRole?.Delete();
            if (RealPlayer != null) RealPlayer.SetChatName(RealPlayer.name);
        }
    }

    private class GuesserShiftableObject : ShiftableNetObject
    {
        public GuesserShiftableObject(string objectName, Vector2 position, byte visibleTo = byte.MaxValue) : base(
            objectName, position, visibleTo)
        {

        }

        public override void SetupOutfit()
        {
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = Sprite;
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = 0;
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId = "";
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId = "";
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId = "";
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId = "";
        }
    }
    
    protected ChatHandler GuesserMessage(string message) => ChatHandler.Of(message, RoleColor.Colorize(RoleName)).LeftAlign();

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
            "You can craft items during meetings. Only items you have enough scrap for will show. To craft an item, first vote yourself so the Shapeshift menu appears. Then select the item you want to craft.";

        [Localized(nameof(CurrentShopMessage))]
        public static string CurrentShopMessage = "You can now craft:\n{0}";

        [Localized(nameof(SelectedItemMessage))]
        public static string SelectedItemMessage = "You have selected to craft {0}. Crafting this will leave you with {1} scrap. Press the skip vote button to continue your crafting.";

        [Localized(nameof(PurchaseItemMessage))]
        public static string PurchaseItemMessage = "You have crafted: {0}. You now have {1} scrap leftover.";
        
        [Localized(nameof(KickedFromGuessing))] public static string KickedFromGuessing = "You took too long to make a choice. You have been kicked from crafting.";
        [Localized(nameof(MoreTimeGiven))] public static string MoreTimeGiven = "You made a choice. The timer has been reset.";
        [Localized(nameof(PageIndex))] public static string PageIndex = "Page {0}/{1}";
        [Localized(nameof(RowText))] public static string RowText = "Row {0}: ";
        [Localized(nameof(ShifterMenuHelpText))] public static string ShifterMenuHelpText = "The extra options on the Shapeshifter menu appear as another player.\nBelow is the correct text that should be displayed:";

        [Localized(nameof(CanVoteOtherPlayers))]
        public static string CanVoteOtherPlayers = "You can now vote yourself as you en.";

        [Localized(nameof(CanVoteOtherPlayersOnSkip))]
        public static string CanVoteOtherPlayersOnSkip = "You can now vote regularly as you skipped before selecting something to craft.";
        [Localized(nameof(CanVoteOtherPlayersOnItem))]
        public static string CanVoteOtherPlayersOnItem = "You can now vote yourself as you selected to end crafting.";

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