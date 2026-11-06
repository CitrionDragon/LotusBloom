using Lotus.API;
using Lotus;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Roles;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.RoleGroups.Vanilla;
using VentLib.Options.UI;
using Lotus.Extensions;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.GUI;
using LotusBloom.RPC;
using VentLib.Networking.RPC.Attributes;
using VentLib.Localization.Attributes;

namespace LotusBloom.Roles.Standard.Impostors;

public class QuickShooter: Impostor, IRoleUI
{
    private int maxBullets;
    private int bulletCount;
    private int keptBullets;

    [UIComponent(UI.Cooldown)]
    private Cooldown reloadCooldown;

    [UIComponent(UI.Counter)]
    private string BulletCounter() => RoleUtils.Counter(bulletCount, maxBullets);

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        KillCooldown = AUSettings.KillCooldown();
        reloadCooldown.Start();
        if (bulletCount == 0) return base.TryKill(target);
        KillCooldown = 1;
        base.TryKill(target);
        bulletCount--;
        return true;
    }

    [RoleAction(LotusActionType.OnPet)]
    public void GainBullet()
    {
        if (bulletCount == maxBullets) return;
        if (reloadCooldown.NotReady()) return;
        reloadCooldown.Start();
        bulletCount++;
        MyPlayer.RpcMark(MyPlayer);
    }

    [RoleAction(LotusActionType.RoundStart)]
    public void ResetBullets()
    {
        reloadCooldown.Start(AUSettings.KillCooldown());
        if (bulletCount >= keptBullets) bulletCount = keptBullets;
    }

    public RoleButton PetButton(IRoleButtonEditor petButton) => 
        petButton.SetText("Reload")
            .BindCooldown(reloadCooldown)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Crew/sheriff_kill.png", 130, true));

    protected override string ForceRoleImageDirectory() => "LotusBloom.assets.Impostors.QuickShooter.yaml";

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.KeyName("Reload Cooldown", Translations.Options.ReloadCooldown)
                .AddFloatRange(10, 120, 2.5f, suffix: "s")
                .BindFloat(reloadCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.KeyName("Max Bullets", Translations.Options.MaxBullets)
                .AddIntRange(1, 15, 1, 5)
                .BindInt(i => maxBullets = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build())
            .SubOption(sub => sub.KeyName("Kept Bullets in Meeting", Translations.Options.KeptBullets)
                .AddIntRange(0, 15, 1, 5)
                .BindInt(i => keptBullets = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build());

    [Localized(nameof(QuickShooter))]
    private static class Translations
    {
        [Localized(LBConstants.RoleName)] public static string RoleName = "QuickShooter";
        [Localized(LBConstants.Description)] public static string Description = "When your reload cd is at 0, you can use pet button to store a bullet, resetting your kill cd. When you kill and have bullets, a bullet is used to set your kill cd to 0 immediately." + LBConstants.DescriptionFooter;
        [Localized(LBConstants.Blurb)] public static string Blurb = "Save Bullets";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(ReloadCooldown))] public static string ReloadCooldown = "Reload Cooldown";
            [Localized(nameof(MaxBullets))] public static string MaxBullets = "Max Bullets";
            [Localized(nameof(KeptBullets))] public static string KeptBullets = "Bullets Kept in Meeting";
        }
    }
}