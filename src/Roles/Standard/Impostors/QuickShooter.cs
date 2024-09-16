using System.Collections.Generic;
using System.Linq;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Vanilla.Meetings;
using Lotus.Factions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Options;
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
    private float reloadCooldown;

    [UIComponent(UI.Counter)]
    private string BulletCounter() => RoleUtils.Counter(bulletCount, maxBullets, RoleColor);

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        reloadCooldown = KillCooldown;
        if (bulletCount == 0) return true;
        AddOverride(new GameOptionOverride(Override.KillCooldown, 1));
        bulletCount--;
        return true;
    }

    [RoleAction(LotusActionType.OnPet)]
    public void GainBullet()
    {
        if (reloadCooldown >= 0 || bulletCount == maxBullets) return;
        reloadCooldown = KillCooldown;
        bulletCount++;
        MyPlayer.RpcMark(MyPlayer);
    }

    [RoleAction(LotusActionType.RoundStart)]
    public void ResetBullets()
    {
        bulletCount = keptBullets;
    }
    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
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