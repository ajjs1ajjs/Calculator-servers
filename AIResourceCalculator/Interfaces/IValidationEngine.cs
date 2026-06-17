using AIResourceCalculator.Models;

namespace AIResourceCalculator.Interfaces;

public interface IValidationEngine
{
    List<ValidationResult> CompareProfiles(ResourceRequirement profile1, ResourceRequirement profile2);
    List<ValidationResult> Validate(ResourceRequirement required, ResourceRequirement allocated);
    List<ValidationResult> ValidateProject(ProjectConfig config, ResourceRequirement calculated, List<InfrastructureNode> actualResources);
}
