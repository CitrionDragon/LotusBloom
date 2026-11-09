using Hazel;

namespace LotusBloom.Version;


/// <summary>
/// Version Representing this Addon
/// </summary>
public class LotusBloomVersion : VentLib.Version.Version
{
    public const string VersionString = "1.2.2";
    public override VentLib.Version.Version Read(MessageReader reader)
    {
        return new LotusBloomVersion();
    }

    protected override void WriteInfo(MessageWriter writer)
    {
    }

    public override string ToSimpleName()
    {
        return $"Lotus Bloom";
    }

    public override string ToString() => $"v{VersionString}";
}
