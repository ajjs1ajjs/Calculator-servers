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
}
