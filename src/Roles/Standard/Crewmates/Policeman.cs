using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Lotus.API.Odyssey;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Managers.History.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.API;
using Lotus.API.Stats;
using Lotus.API.Vanilla.Sabotages;
using Lotus.Roles.Internals.Enums;
using Lotus.Extensions;
using Lotus.Factions.Impostors;
using Lotus.Managers;
using Lotus.Options;
using Lotus.Patches.Systems;
using Lotus.Roles.Events;
using Lotus.Roles.Overrides;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using Lotus.GameModes.Standard;
using Lotus.Roles;
using Lotus;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using VentLib.Utilities.Collections;
using VentLib.Options.UI.Options;
using Lotus.Roles.RoleGroups.Crew;
using Lotus.Roles.RoleGroups.Impostors;

namespace LotusBloom.Roles.Standard.Crewmates;

public class Policeman : Crewmate
{
    public static List<(Func<CustomRole, bool> predicate, GameOptionBuilder builder)> RoleTypeBuilders = new()
    {
        (r => r.SpecialType is SpecialType.NeutralKilling, new GameOptionBuilder()
            .Name("Neutral Killing Are Red")//, TranslationUtil.Colorize(NeutralKillingRed, Color.red, ModConstants.Palette.NeutralColor, ModConstants.Palette.KillingColor))
            .Value(v => v.Text(GeneralOptionTranslations.OffText).Value(0).Color(Color.red).Build())
            .Value(v => v.Text(GeneralOptionTranslations.AllText).Value(1).Color(Color.green).Build())
            .Value(v => v.Text(GeneralOptionTranslations.CustomText).Value(2).Color(new Color(0.73f, 0.58f, 1f)).Build())
            .ShowSubOptionPredicate(i => (int)i == 2)),
        (r => r.SpecialType is SpecialType.Neutral, new GameOptionBuilder()
            .Name("Neutral Passive Are Red")//, TranslationUtil.Colorize(NeutralPassiveRed, Color.red, ModConstants.Palette.NeutralColor, ModConstants.Palette.PassiveColor))
            .Value(v => v.Text(GeneralOptionTranslations.OffText).Value(0).Color(Color.red).Build())
            .Value(v => v.Text(GeneralOptionTranslations.AllText).Value(1).Color(Color.green).Build())
            .Value(v => v.Text(GeneralOptionTranslations.CustomText).Value(2).Color(new Color(0.73f, 0.58f, 1f)).Build())
            .ShowSubOptionPredicate(i => (int)i == 2)),
        (r => r.Faction.GetType() == typeof(Madmates), new GameOptionBuilder()
            .Name("Madmates Are Red")//, TranslationUtil.Colorize(MadmateRed, Color.red, ModConstants.Palette.MadmateColor))
            .Value(v => v.Text(GeneralOptionTranslations.OffText).Value(0).Color(Color.red).Build())
            .Value(v => v.Text(GeneralOptionTranslations.AllText).Value(1).Color(Color.green).Build())
            .Value(v => v.Text(GeneralOptionTranslations.CustomText).Value(2).Color(new Color(0.73f, 0.58f, 1f)).Build())
            .ShowSubOptionPredicate(i => (int)i == 2))
    };
    public static List<int> RoleTypeSettings = new() { 0, 0, 0 };
    // 2 = Color red, 1 = Color green
    public static Dictionary<Type, int> RoleColoringDictionary = new();
    public Policeman()
    {
        StandardRoles.Callbacks.Add(PopulateInvestigatorOptions);
    }
    private int totalhandcuffs;
    private int handcuffs;
    private float handcuffSpeed;
    private bool dragging = false;
    private PlayerControl handcuffedplayer;
    [NewOnSetup] private Dictionary<byte, Escort.BlockDelegate> blockedPlayers;
    Remote<GameOptionOverride> optionOverride;
    [NewOnSetup] private Dictionary<byte, Remote<IndicatorComponent>> playerRemotes = null!;
    [NewOnSetup] private List<byte> goodplayers = null!;
    [NewOnSetup] private List<byte> evilplayers = null!;
    private Cooldown InterrogateCooldown;
    private int InterrogateChance;
    private int InterrogateHandcuffChance;
    private int BreakChance;
    protected override void PostSetup() => handcuffs = totalhandcuffs;

    [UIComponent(UI.Cooldown)]
    private Cooldown handcuffCooldown;

    [UIComponent(UI.Counter, ViewMode.Additive, GameState.Roaming, GameState.InMeeting)]
    public string RemainingShotCounter() => RoleUtils.Counter(handcuffs, totalhandcuffs);

    [RoleAction(LotusActionType.Attack)]
    public bool Interrogate(PlayerControl target, ActionHandle handle)
    {
        handle.Cancel();
        if (dragging) return false;
        bool KillPlayer() => MyPlayer.InteractWith(target, LotusInteraction.FatalInteraction.Create(this)) is InteractionResult.Proceed;
        if (evilplayers.Contains(target.PlayerId))
        {
            return KillPlayer();
        }
        if (goodplayers.Contains(target.PlayerId))
        {
            if (target==handcuffedplayer) RemoveHandcuff();
            return false;
        }
        System.Random random = new System.Random();
        int randomnumber = random.Next(0, 100);
        if (target == handcuffedplayer)
        {
            if (randomnumber>InterrogateHandcuffChance)
            {
                MyPlayer.RpcMark();
                return false;
            }
        }
        else if (randomnumber > InterrogateChance)
        {
            MyPlayer.RpcMark();
            return false;
        }
        CustomRole role = target.PrimaryRole();
        int setting = -1;
        RoleTypeBuilders.FirstOrOptional(b => b.predicate(role)).IfPresent(rtb => setting = RoleTypeSettings[RoleTypeBuilders.IndexOf(rtb)]);
        if (setting == -1 || setting == 2) setting = RoleColoringDictionary.GetValueOrDefault(role.GetType(), -1);
        if (setting == -1) setting = role.Faction.GetType() == typeof(ImpostorFaction) ? 1 : 2;
        Color color = setting == 2 ? Color.green : Color.red;
        if (color == Color.green) goodplayers.Add(target.PlayerId);
        if (color == Color.red) evilplayers.Add(target.PlayerId);
        NameComponent nameComponent = new(new LiveString(target.name, color), Game.InGameStates, ViewMode.Replace, MyPlayer);
        target.NameModel().GetComponentHolder<NameHolder>().Add(nameComponent);
        MyPlayer.RpcMark();
        return false;
    }

    [RoleAction(LotusActionType.OnPet)]
    public void OnPet()
    {
        PlayerControl? closestPlayer = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault();
        if (closestPlayer == null) return;
        if (closestPlayer == handcuffedplayer)
        {
            dragging = !dragging;
            return;
        }
        if (handcuffs <= 0||handcuffCooldown.NotReady()) return;
        if (handcuffedplayer != null) RemoveHandcuff();
        handcuffedplayer = closestPlayer;
        optionOverride = handcuffedplayer.PrimaryRole().AddOverride(new GameOptionOverride(Override.PlayerSpeedMod, handcuffSpeed));
        handcuffedplayer.PrimaryRole().SyncOptions();
        blockedPlayers[handcuffedplayer.PlayerId] = Escort.BlockDelegate.Block(handcuffedplayer, MyPlayer, -1f);
        IndicatorComponent component = new(new LiveString("∞", new Color(0.6f, 0.6f, 0.6f)), Game.InGameStates, viewers: MyPlayer);
        playerRemotes[closestPlayer.PlayerId] = closestPlayer.NameModel().GetComponentHolder<IndicatorHolder>().Add(component);
        handcuffs--;
        handcuffCooldown.Start();
    }

    [RoleAction(LotusActionType.FixedUpdate)]
    private void drag()
    {
        if (handcuffedplayer.Data.IsDead) RemoveHandcuff();
        if (!dragging) return;
        Utils.Teleport(handcuffedplayer.NetTransform,MyPlayer.transform.position);
    }
    [RoleAction(LotusActionType.RoundEnd)]
    private void startcooldown() => handcuffCooldown.Start();

    [RoleAction(LotusActionType.RoundEnd)]
    public void RemoveHandcuff()
    {
        playerRemotes!.GetValueOrDefault(handcuffedplayer.PlayerId, null)?.Delete();
        optionOverride.Delete();
        handcuffedplayer.PrimaryRole().SyncOptions();
        blockedPlayers.ToArray().ForEach(k =>
        {
            if (k.Key != handcuffedplayer.PlayerId) return;
            blockedPlayers.Remove(k.Key);
            k.Value.Delete();
        });
        handcuffedplayer = null;
        dragging = false;
    }

    [RoleAction(LotusActionType.OnPet, ActionFlag.GlobalDetector)]
    private void BreakCuffs(PlayerControl player)
    {
        if (player != handcuffedplayer) return;
        System.Random random = new System.Random();
        int randomnumber = random.Next(0, 100);
        if (randomnumber <= BreakChance||handcuffedplayer.PrimaryRole() is Escapist||handcuffedplayer.PrimaryRole() is ExConvict) RemoveHandcuff();
    }

    [RoleAction(LotusActionType.PlayerAction, ActionFlag.GlobalDetector)]
    private void BlockAction(PlayerControl source, ActionHandle handle, RoleAction action)
    {
        if (action.Blockable) Block(source, handle);
    }

    [RoleAction(LotusActionType.VentEntered, ActionFlag.GlobalDetector)]
    private void Block(PlayerControl source, ActionHandle handle)
    {
        Escort.BlockDelegate? blockDelegate = blockedPlayers.GetValueOrDefault(source.PlayerId);
        if (blockDelegate == null) return;

        handle.Cancel();
        blockDelegate.UpdateDelegate();
    }

    [RoleAction(LotusActionType.SabotageStarted, ActionFlag.GlobalDetector)]
    private void BlockSabotage(PlayerControl caller, ActionHandle handle)
    {
        Escort.BlockDelegate? blockDelegate = blockedPlayers.GetValueOrDefault(caller.PlayerId);
        if (blockDelegate == null) return;

        handle.Cancel();
        blockDelegate.UpdateDelegate();
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .Color(RoleColor)
            .SubOption(sub => sub
                .Name("Interrogate Cooldown")//, Translations.Options.KillCooldown)
                .BindFloat(this.InterrogateCooldown.SetDuration)
                .AddFloatRange(0, 120, 2.5f, 12, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub.Name("Interrogate Success Chance")//, DeadlyTranslations.Options.CooldownReduction)
                .AddIntRange(0, 100, 5, 5, "%")
                .BindInt(i => InterrogateChance = i)
                .Build())
            .SubOption(sub => sub.Name("Interrogate Handcuffed Success Chance")//, DeadlyTranslations.Options.CooldownReduction)
                .AddIntRange(0, 100, 5, 5, "%")
                .BindInt(i => InterrogateHandcuffChance = i)
                .Build())
            .SubOption(sub => sub
                .Name("Total Handcuffs")//, Translations.Options.TotalShots)
                .Bind(v => this.totalhandcuffs = (int)v)
                .AddIntRange(1, 30, 1, 4)
                .Build())
            .SubOption(sub => sub
                .Name("Handcuff Cooldown")//, Translations.Options.KillCooldown)
                .BindFloat(this.handcuffCooldown.SetDuration)
                .AddFloatRange(0, 120, 2.5f, 12, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub
                .Name("Handcuffed Speed")//, Translations.Options.SpeedIncrease)
                .Value(v =>  v.Text("Frozen").Color(new Color(1f, 0.61f, 0.33f)).Value(-1f).Build())
                .AddFloatRange(0.25f, 2.5f, 0.25f, 3)
                .BindFloat(f => handcuffSpeed = f)
                .Build())
            .SubOption(sub => sub.Name("Handcuff Break Chance")//, DeadlyTranslations.Options.CooldownReduction)
                .AddIntRange(0, 100, 5, 5, "%")
                .BindInt(i => BreakChance = i)
                .Build());

    private void PopulateInvestigatorOptions()
    {
        StandardRoles.Instance.AllRoles.OrderBy(r => r.EnglishRoleName).ForEach(r =>
        {
            RoleTypeBuilders.FirstOrOptional(b => b.predicate(r)).Map(i => i.builder)
                .IfPresent(builder =>
                {
                    builder.SubOption(sub => sub.KeyName(r.EnglishRoleName, r.RoleColor.Colorize(r.RoleName))
                        .AddEnableDisabledValues()
                        .BindBool(b =>
                        {
                            if (b) RoleColoringDictionary[r.GetType()] = 1;
                            else RoleColoringDictionary[r.GetType()] = 2;
                        })
                        .Build());
                });
        });
        RoleTypeBuilders.ForEach((rtb, index) =>
        {
            rtb.builder.BindInt(i => RoleTypeSettings[index] = i);
            Option option = rtb.builder.Build();
            //RoleOptions.AddChild(option);
            GlobalRoleManager.RoleOptionManager.Register(option, OptionLoadMode.LoadOrCreate);
        });
    }

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .DesyncRole(RoleTypes.Impostor)
            .RoleColor(Color.blue)
            .RoleAbilityFlags(RoleAbilityFlag.CannotVent | RoleAbilityFlag.CannotSabotage | RoleAbilityFlag.IsAbleToKill)
            .OptionOverride(Override.KillCooldown, () => InterrogateCooldown.Duration)
            .OptionOverride(Override.ImpostorLightMod, () => AUSettings.CrewLightMod())
            .OptionOverride(Override.ImpostorLightMod, () => AUSettings.CrewLightMod() / 5, () => SabotagePatch.CurrentSabotage != null && SabotagePatch.CurrentSabotage.SabotageType() is SabotageType.Lights);
    
}