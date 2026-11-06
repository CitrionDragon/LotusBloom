using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Lotus.Addons;
using LotusBloom.Version;
using VentLib;
using VentLib.Logging;

namespace LotusBloom;

[BepInAutoPlugin("com.citriondragon.LotusBloom", "LotusBloom", LotusBloomVersion.VersionString)]
[BepInDependency(Vents.Id)]
[BepInDependency(ProjectLotus.Id)]
public partial class BloomPlugin : BasePlugin
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(BloomPlugin));
    private LotusAddon addon;

    public override void Load()
    {
        try
        {
            BootLog("Loading Lotus Bloom (from BepInEx plugin)");
            
            if (AddonManager.Addons.Any(x => x.Version.ToSimpleName() == "Lotus Bloom"))
            {
                BootLog("Lotus Bloom is already loaded. Skipping.");
                return;
            }
            
            addon = (LotusAddon)Activator.CreateInstance(typeof(LotusBloom));
            if (addon == null) throw new Exception("Failed to create LotusBloom instance");
            Vents.Register(Assembly.GetExecutingAssembly());
            AddonManager.Addons.Add(addon);
            addon.Initialize();
        }
        catch (Exception e)
        {
            BootLog("Failed to load LotusBloom D:");
            log.Exception(e);
            AddonManager.FailedAddons.Add("LotusBloom.dll");
            return;
        }


        BootLog("LotusBloom loaded. Running post-initialize.");
        addon.PostInitialize(AddonManager.Addons);
    }
    
    private static void BootLog(string message, params object[] args) => log.Log(LogLevel.Info, message, args);
}