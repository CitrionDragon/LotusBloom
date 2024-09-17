using System.Collections.Generic;
using System.Linq;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Vanilla.Meetings;
using Lotus.Factions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Options;
using Lotus.Roles;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Overrides;
using Lotus.Roles.RoleGroups.Vanilla;
using UnityEngine;
using VentLib.Options.UI;
using VentLib.Options.IO;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using VentLib.Localization.Attributes;
using Lotus.Extensions;

namespace LotusBloom.Roles.Standard.Impostors;

public class QuickShooter: Impostor
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
        //MyPlayer.InteractWith(MyPlayer, FakeFatalIntent());
        MyPlayer.RpcMark(MyPlayer);
    }

    [RoleAction(LotusActionType.RoundStart)]
    public void ResetBullets()
    {
        reloadCooldown.Start(AUSettings.KillCooldown());
        if (bulletCount >= keptBullets) bulletCount = keptBullets;
    }
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.Name("Reload Cooldown")
                .AddFloatRange(0, 120, 2.5f, suffix: "s")
                .BindFloat(reloadCooldown.SetDuration)
                .Build())
            .SubOption(sub => sub.Name("Max Bullets")
                .AddIntRange(1, 15, 1, 5)
                .BindInt(i => maxBullets = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build())
            .SubOption(sub => sub.Name("Kept Bullets in Meeting")
                .AddIntRange(1, 15, 1, 5)
                .BindInt(i => keptBullets = i)
                .ShowSubOptionPredicate(o => (int)o > 1)
                .Build());

}