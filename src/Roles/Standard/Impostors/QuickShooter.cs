using Lotus.API;
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
            .SubOption(sub => sub.Name("Reload Cooldown")
                .AddFloatRange(10, 120, 2.5f, suffix: "s")
                .BindFloat(reloadCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Max Bullets")
                .AddIntRange(1, 15, 1, 5)
                .BindInt(i => maxBullets = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build())
            .SubOption(sub => sub.Name("Kept Bullets in Meeting")
                .AddIntRange(0, 15, 1, 5)
                .BindInt(i => keptBullets = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build());

}