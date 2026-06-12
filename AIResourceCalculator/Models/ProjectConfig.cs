namespace AIResourceCalculator.Models;

public class ProjectConfig
{
    public string ProjectName { get; set; } = "";
    public int UserCount { get; set; }
    public DeploymentType DeploymentType { get; set; } = DeploymentType.Kubernetes;
    public LoadProfile LoadProfile { get; set; } = LoadProfile.Basic;
    public double OverprovisioningFactor { get; set; } = 1.0;
    public bool HaEnabled { get; set; } = true;
    public List<string> SelectedModules { get; set; } = new();
}
