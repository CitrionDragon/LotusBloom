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
using Lotus.Roles.Subroles;
using LotusBloom.Roles.Standard.Neutral.Passive;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.GUI;

namespace LotusBloom.Roles.Standard.Crew;

public class Policeman : Crewmate, IRoleUI
{
    /*
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
    };*/
    //ublic static List<int> RoleTypeSettings = new() { 0, 0, 0 };
    // 2 = Color red, 1 = Color green
    //public static Dictionary<Type, int> RoleKillerDictionary = new();
    /*public Policeman()
    {
        StandardRoles.Callbacks.Add(PopulateInvestigatorOptions);
    }*/
    /*public override void Sheriff()
    {
        private static Policeman _policeman = new();
        public CustomRole Variation() => _policeman;
    }*/
    
    private int totalhandcuffs;
    private int handcuffs;
    private float handcuffSpeed;
    private bool dragging = false;
    private PlayerControl handcuffedplayer = null;
    [NewOnSetup] private Dictionary<byte, Escort.BlockDelegate> blockedPlayers;
    Remote<GameOptionOverride> optionOverride;
    [NewOnSetup] private Dictionary<byte, Remote<IndicatorComponent>> playerRemotes = null!;
    [NewOnSetup] private List<byte> goodplayers = null!;
    [NewOnSetup] private List<byte> evilplayers = null!;
    [NewOnSetup] private FixedUpdateLock fixedUpdateLock = new(ModConstants.RoleFixedUpdateCooldown);
    private Cooldown InterrogateCooldown = null!;
    private int InterrogateChance;
    private int InterrogateHandcuffChance;
    private int BreakChance;
    private int KbAction = 0; //0 = Interrogate, 1 = Kill, 2 = Free
    protected override void PostSetup()
    {
        handcuffs = totalhandcuffs;
        Rogue.IncompatibleRoles.Add(typeof(Policeman));
    }

    [UIComponent(UI.Cooldown)]
    private Cooldown handcuffCooldown;

    [UIComponent(UI.Text)]
    private string ModeDisplay() 
    {
        if (KbAction == 0) return Color.yellow.Colorize("Interrogate");
        if (KbAction == 1) return Color.red.Colorize("Kill");
        return Color.green.Colorize("Free");
    }

    [UIComponent(UI.Counter, ViewMode.Additive, GameState.Roaming, GameState.InMeeting)]
    public string RemainingShotCounter() => RoleUtils.Counter(handcuffs, totalhandcuffs);

    [RoleAction(LotusActionType.Attack)]
    public bool Interrogate(PlayerControl target, ActionHandle handle)
    {
        handle.Cancel();
        if (dragging) return false;
        bool KillPlayer() => MyPlayer.InteractWith(target, LotusInteraction.FatalInteraction.Create(this)) is InteractionResult.Proceed;
        CustomRole role = target.PrimaryRole();
        int setting = -1;
        Color color = Color.gray;
        Sheriff.RoleTypeBuilders.FirstOrOptional(b => b.predicate(role)).IfPresent(rtb => setting = Sheriff.RoleTypeSettings[Sheriff.RoleTypeBuilders.IndexOf(rtb)]);
        if (setting == 0) color = Color.green;
        else if (setting == 1) color = Color.red;
        if (color == Color.gray)
        {
            setting = Sheriff.RoleKillerDictionary.GetValueOrDefault(role.GetType(), -1);
            if (setting == -1) setting = role.Faction.GetType() == typeof(ImpostorFaction) ? 1 : 2;
            color = setting == 1 ? Color.red : Color.green;
        }
        
        if (KbAction == 1)
        {
            if (color== Color.red) return KillPlayer();
            DeathEvent deathEvent = new MisfiredEvent(MyPlayer);
            UnblockedInteraction lotusInteraction = new(new FatalIntent(false, () => deathEvent), this);
            MyPlayer.InteractWith(MyPlayer, lotusInteraction);
            return true;
        }
        if (KbAction == 2)
        {
            if (target==handcuffedplayer) RemoveHandcuff();
            return false;
        }
        /*
        if (evilplayers.Contains(target.PlayerId))
        {
            return KillPlayer();
        }
        if (goodplayers.Contains(target.PlayerId))
        {
            if (target==handcuffedplayer) RemoveHandcuff();
            return false;
        }*/
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
        PlayerControl closestPlayer = MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault();
        if (closestPlayer == null)
        {
            RoleButton killButton = UIManager.KillButton;
            KbAction++;
            if (KbAction >= 3) KbAction = 0;
            if (KbAction == 0)
            {
                killButton.SetText("Interrogate")
                .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/investigator_investigate.png", 130, true));
            }
            else if (KbAction == 1)
            {
                killButton.SetText("Kill")
                .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/sheriff_kill.png", 130, true));
            }
            else
            {
                killButton.SetText("Free")
                .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/escort_roleblock.png", 130, true));
            }
            return;
        }
        if (closestPlayer == handcuffedplayer)
        {
            //dragging = !dragging; Disable dragging for now; You can use the Pet button near the handcuffed player to drag them around.
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
/*
    [RoleAction(LotusActionType.FixedUpdate)]
    private void drag()
    {
        if (handcuffedplayer.Data.IsDead) RemoveHandcuff();
        if (!dragging) return;
        Utils.Teleport(handcuffedplayer.NetTransform,MyPlayer.transform.position);
    }
    */
    [RoleAction(LotusActionType.RoundEnd)]
    private void startcooldown() => handcuffCooldown.Start();

    [RoleAction(LotusActionType.RoundEnd)]
    public void RemoveHandcuff()
    {
        if (handcuffedplayer == null) return;
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
        Escort.BlockDelegate blockDelegate = blockedPlayers.GetValueOrDefault(source.PlayerId);
        if (blockDelegate == null) return;

        handle.Cancel();
        blockDelegate.UpdateDelegate();
    }

    [RoleAction(LotusActionType.SabotageStarted, ActionFlag.GlobalDetector)]
    private void BlockSabotage(PlayerControl caller, ActionHandle handle)
    {
        Escort.BlockDelegate blockDelegate = blockedPlayers.GetValueOrDefault(caller.PlayerId);
        if (blockDelegate == null) return;

        handle.Cancel();
        blockDelegate.UpdateDelegate();
    }

[RoleAction(LotusActionType.FixedUpdate)]
    public void UpdateButton()
    {
        if (!fixedUpdateLock.AcquireLock()) return;
        if (handcuffedplayer != null)
        {
            if (handcuffedplayer.Data.IsDead) RemoveHandcuff();
        }
        RoleButton petButton = UIManager.PetButton;
        if (MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault() == null)
        {
            petButton.SetText(RoleTranslations.Switch)
            .BindCooldown(null)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/generic_switch_ability.png", 130, true));
            petButton.GetButton().SetCoolDown(0,1);
        }
        else
        {
            petButton.SetText("Handcuff")
            .BindCooldown(handcuffCooldown)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/escort_roleblock.png", 130, true));
        }
    }

    //Kill Buttons: Sheriff Kill, Interogate, Free
    public RoleButton KillButton(IRoleButtonEditor killButton) =>
        killButton.SetText("Interrogate")
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/investigator_investigate.png", 130, true));
    //Pet Buttons: Switch Ability, Handcuff
    public RoleButton PetButton(IRoleButtonEditor abilityButton) => MyPlayer.GetPlayersInAbilityRangeSorted().FirstOrDefault() == null
        ? abilityButton.SetText(RoleTranslations.Switch)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/generic_switch_ability.png", 130, true))
        : abilityButton.SetText("Handcuff")
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/escort_roleblock.png", 130, true));

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Crew.Policeman.yaml";

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

    //protected override RoleType GetRoleType() => RoleType.Variation;

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .DesyncRole(RoleTypes.Impostor)
            .RoleColor(Color.blue)
            .RoleAbilityFlags(RoleAbilityFlag.CannotVent | RoleAbilityFlag.CannotSabotage | RoleAbilityFlag.IsAbleToKill)
            //.RoleFlags(RoleFlag.VariationRole)
            .OptionOverride(Override.KillCooldown, () => InterrogateCooldown.Duration)
            .OptionOverride(Override.ImpostorLightMod, () => AUSettings.CrewLightMod())
            .OptionOverride(Override.ImpostorLightMod, () => AUSettings.CrewLightMod() / 5, () => SabotagePatch.CurrentSabotage != null && SabotagePatch.CurrentSabotage.SabotageType() is SabotageType.Lights);
    
}