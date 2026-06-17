namespace AIResourceCalculator.Models;

public class ProjectConfig
{
    public string ProjectName { get; set; } = "";
    public int UserCount { get; set; }
    public DeploymentType DeploymentType { get; set; } = DeploymentType.Kubernetes;
    public ProductType ProductType { get; set; } = ProductType.Standard;
    public LoadProfile LoadProfile { get; set; } = LoadProfile.Basic;
    public DatabaseType DatabaseType { get; set; } = DatabaseType.MsSql;
    public bool HaEnabled { get; set; } = true;
    public List<string> SelectedModules { get; set; } = new();
}
