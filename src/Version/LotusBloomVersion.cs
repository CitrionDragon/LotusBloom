using Hazel;

namespace LotusBloom.Version;


/// <summary>
/// Version Representing this Addon
/// </summary>
public class LotusBloomVersion : VentLib.Version.Version
{
    public override VentLib.Version.Version Read(MessageReader reader)
    {
        return new LotusBloomVersion();
    }

    protected override void WriteInfo(MessageWriter writer)
    {
    }

    public override string ToSimpleName()
    {
        return "Lotus Bloom Addon Version v1.1.0";
    }

    public override string ToString() => "LotusBloomAddon";
}
