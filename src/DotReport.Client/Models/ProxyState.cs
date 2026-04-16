namespace DotReport.Client.Models;

public sealed class ProxyState
{
    public ModelStatus PrimaryStatus { get; set; } = ModelStatus.Unloaded;
    public ModelStatus BackupStatus { get; set; } = ModelStatus.Unloaded;
    public BuildProfile ActiveBuild { get; set; } = BuildProfile.Standard;
    public bool IsProcessing { get; set; }
    public bool BothModelsReady =>
        PrimaryStatus == ModelStatus.Ready && BackupStatus == ModelStatus.Ready;
    public bool BackupOnlyReady =>
        PrimaryStatus != ModelStatus.Ready && BackupStatus == ModelStatus.Ready;
    public int ProvisioningFacesAssembled { get; set; }
    public const int TotalDodecahedronFaces = 12;
    public bool IsFullyProvisioned =>
        ProvisioningFacesAssembled >= TotalDodecahedronFaces;
    public string? LastError { get; set; }
    public long LastPrimaryLatencyMs { get; set; }
    public bool PrimaryExceedsLatencyThreshold => LastPrimaryLatencyMs > 500;
}
