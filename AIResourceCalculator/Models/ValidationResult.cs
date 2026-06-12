namespace AIResourceCalculator.Models;

public class ValidationResult
{
    public string ResourceName { get; set; } = "";
    public double Required { get; set; }
    public double Allocated { get; set; }
    public string Unit { get; set; } = "";
    public bool IsCompliant => Allocated >= Required;
    public double Delta => Allocated - Required;
    public double DeltaPercent => Required > 0 ? Math.Round((Allocated - Required) / Required * 100, 1) : 0;
    public string Severity { get; set; } = "OK";
    public string Recommendation { get; set; } = "";
}
