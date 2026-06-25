using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class ResultsPresenter
{
    private readonly ConfigExportService _export;
    private readonly IValidationEngine _validator;

    public ResultsPresenter(ConfigExportService export, IValidationEngine validator)
    {
        _export = export;
        _validator = validator;
    }

    public List<ValidationResult> CompareProfiles(ResourceRequirement profile1, ResourceRequirement profile2)
        => _validator.CompareProfiles(profile1, profile2);

    public List<ValidationResult> Validate(ResourceRequirement required, ResourceRequirement allocated)
        => _validator.Validate(required, allocated);

    public List<ValidationResult> ValidateProject(ProjectConfig config, ResourceRequirement calculated, List<InfrastructureNode> actual)
        => _validator.ValidateProject(config, calculated, actual);

    public string ExportXml(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
        => _export.ExportXml(req, config, environments, matrixRanges);

    public string ExportHtml(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
        => _export.ExportHtml(req, config, environments, matrixRanges);

    public byte[] ExportExcel(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
        => _export.ExportExcel(req, config, environments, matrixRanges);
}
