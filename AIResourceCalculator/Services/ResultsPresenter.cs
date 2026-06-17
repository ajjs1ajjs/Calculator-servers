using System.Text;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class ResultsPresenter
{
    private readonly ConfigExportService _export = new();
    private readonly ValidationEngine _validator = new();

    public List<ValidationResult> CompareProfiles(ResourceRequirement profile1, ResourceRequirement profile2)
        => _validator.CompareProfiles(profile1, profile2);

    public List<ValidationResult> Validate(ResourceRequirement required, ResourceRequirement allocated)
        => _validator.Validate(required, allocated);

    public List<ValidationResult> ValidateProject(ProjectConfig config, ResourceRequirement calculated, List<InfrastructureNode> actual)
        => _validator.ValidateProject(config, calculated, actual);

    public string ExportText(ResourceRequirement req, ProjectConfig config)
        => _export.ExportTxt(req, config);

    public string ExportHtml(ResourceRequirement req, ProjectConfig config)
        => _export.ExportHtml(req, config);

    public string ExportSvg(ResourceRequirement req, ProjectConfig config)
        => _export.ExportSvg(req, config);

    public string ExportMermaid(ResourceRequirement req, ProjectConfig config)
        => _export.ExportMermaid(req, config);

    public string ExportTerraform(ResourceRequirement req, ProjectConfig config)
        => _export.ExportTerraform(req, config);

    public string ExportArmTemplate(ResourceRequirement req, ProjectConfig config)
        => _export.ExportArmTemplate(req, config);

    public string ExportBicep(ResourceRequirement req, ProjectConfig config)
        => _export.ExportBicep(req, config);

    public string ExportPulumi(ResourceRequirement req, ProjectConfig config)
        => _export.ExportPulumi(req, config);

    public string ExportAnsible(ResourceRequirement req, ProjectConfig config)
        => _export.ExportAnsible(req, config);

    public string ExportHld(ResourceRequirement req, ProjectConfig config)
        => _export.ExportHld(req, config);

    public static string BuildSvgDiagram(ResourceRequirement req, ProjectConfig config)
        => DiagramBuilder.BuildSvg(req, config);

    public static List<ServiceComponent> ComputeScaling(
        ProjectConfig config,
        List<ServiceComponent> points,
        ISizingEngine engine,
        List<ProjectModule> modules)
    {
        var result = new List<ServiceComponent>();
        var step = config.UserCount <= 100 ? 25 : config.UserCount <= 500 ? 50 : 100;
        var steps = new List<int>();
        for (int u = step; u <= config.UserCount * 2; u += step)
            steps.Add(u);
        if (steps.Count > 30)
            steps = steps.Where((_, i) => i % (steps.Count / 20) == 0).ToList();

        engine.SetModules(modules);
        foreach (var uc in steps)
        {
            var cfg = new ProjectConfig
            {
                ProjectName = config.ProjectName, UserCount = uc,
                DeploymentType = config.DeploymentType,
                ProductType = config.ProductType, LoadProfile = config.LoadProfile
            };
            var req = engine.Calculate(cfg);
            result.Add(new ServiceComponent
            {
                Name = $"{uc} users",
                Cpu = Math.Round(req.TotalCpu, 1),
                RamGb = Math.Round(req.TotalRamGb, 1),
                Replicas = req.Infrastructure.Sum(n => n.NodeCount),
                Notes = $"IOPS:{req.TotalIops}, Storage:{req.TotalStorageGb}GB"
            });
        }
        return result;
    }
}
