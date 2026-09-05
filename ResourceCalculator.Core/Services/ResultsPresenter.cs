using ResourceCalculator.Interfaces;
using ResourceCalculator.Models;

namespace ResourceCalculator.Services;

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

    public byte[] ExportExcel(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
        => _export.ExportExcel(req, config, environments, matrixRanges);

    public byte[] ExportPdf(ResourceRequirement req, ProjectConfig config,
        IReadOnlyList<EnvironmentReport>? environments = null,
        IEnumerable<UserLoadRange>? matrixRanges = null)
        => _export.ExportPdf(req, config, environments, matrixRanges);
}
