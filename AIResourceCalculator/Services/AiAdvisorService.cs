using System.Text.Json;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;

namespace AIResourceCalculator.Services;

public class AiAdvisorService
{
    private AiSettings _settings = new();
    private AiApiService? _api;
    private bool _isUk => LocalizationService.Instance.CurrentLang == "uk";

    public void UpdateSettings(AiSettings settings)
    {
        _settings = settings;
        _api = settings.EnableRealAi && settings.Provider != AiProvider.None
            ? new AiApiService(settings)
            : null;
    }

    public bool IsRealAiEnabled => _api != null;

    public async Task<List<AiRecommendation>> AnalyzeAsync(ResourceRequirement req, ProjectConfig config)
    {
        if (_api != null)
        {
            try
            {
                var prompt = _api.BuildAnalysisPrompt(req, config);
                var result = await _api.GetRecommendation(prompt);
                if (!string.IsNullOrEmpty(result) && !result.StartsWith("AI Error"))
                {
                    var aiRecs = ParseAiResponse(result);
                    if (aiRecs.Count > 0)
                        return aiRecs;
                }
            }
            catch { }
        }

        return Analyze(req, config);
    }

    public List<AiRecommendation> Analyze(ResourceRequirement req, ProjectConfig config)
    {
        var recommendations = new List<AiRecommendation>();
        recommendations.AddRange(AnalyzeInstanceFit(req));
        recommendations.AddRange(AnalyzeEfficiency(req));
        recommendations.AddRange(AnalyzeScaling(req, config));
        recommendations.AddRange(AnalyzeStorage(req));
        return recommendations;
    }

    private List<AiRecommendation> ParseAiResponse(string json)
    {
        try
        {
            json = json.Trim();

            var jsonMatch = System.Text.RegularExpressions.Regex.Match(json, @"```(?:json)?\s*([\s\S]*?)\s*```");
            if (jsonMatch.Success) json = jsonMatch.Groups[1].Value.Trim();

            if (!json.StartsWith("[")) json = "[" + json + "]";

            var recs = JsonSerializer.Deserialize<List<AiRecommendation>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return recs ?? new List<AiRecommendation>();
        }
        catch
        {
            return new List<AiRecommendation>();
        }
    }

    private string Loc(string en, string uk) => _isUk ? uk : en;

    private List<AiRecommendation> AnalyzeInstanceFit(ResourceRequirement req)
    {
        var list = new List<AiRecommendation>();
        var cpuPerNode = req.WorkerNodeCount > 0 ? req.TotalCpu / req.WorkerNodeCount : 0;
        var ramPerNode = req.WorkerNodeCount > 0 ? req.TotalRamGb / req.WorkerNodeCount : 0;

        var instanceType = RecommendInstance(cpuPerNode, ramPerNode);

        list.Add(new AiRecommendation
        {
            Category = Loc("Instance Type", "Тип інстансу"),
            Severity = "ok",
            Title = Loc($"✅ Recommended: {instanceType.Name}  ✓", $"✅ Рекомендовано: {instanceType.Name}  ✓"),
            Description = Loc(
                $"Your load of {cpuPerNode:F1} vCPU / {ramPerNode:F1} GB per node fits {instanceType.Name}",
                $"Навантаження {cpuPerNode:F1} vCPU / {ramPerNode:F1} GB на вузол відповідає {instanceType.Name}"),
            Action = Loc(
                $"Deploy {instanceType.Name} × {req.WorkerNodeCount} nodes. Estimated ${instanceType.MonthlyCost * req.WorkerNodeCount}/mo",
                $"Розгорніть {instanceType.Name} × {req.WorkerNodeCount} вузлів. ~${instanceType.MonthlyCost * req.WorkerNodeCount}/міс"),
            PotentialSavings = instanceType.MonthlyCost * req.WorkerNodeCount
        });

        if (req.DeploymentType == DeploymentType.Kubernetes)
        {
            var (podCpu, podRam) = GetAvgPodResources(req);
            var maxByCpu = (int)(cpuPerNode / Math.Max(podCpu, 0.1));
            var maxByRam = (int)(ramPerNode / Math.Max(podRam, 0.1));
            var podsPerNode = Math.Min(maxByCpu, maxByRam);

            if (podsPerNode < 5)
            {
                var targetCpu = Math.Max(8, podCpu * 10);
                list.Add(new AiRecommendation
                {
                    Category = Loc("Pod Density", "Щільність подів"),
                    Severity = "warning",
                    Title = Loc($"🟡 Low pod density: ~{podsPerNode} pods/node", $"🟡 Мало подів: ~{podsPerNode} подів/вузол"),
                    Description = Loc(
                        $"Each pod needs ~{podCpu:F2} CPU / {podRam:F2} GB RAM. Consider larger workers",
                        $"Кожному поду потрібно ~{podCpu:F2} CPU / {podRam:F2} GB RAM. Потрібні більші вузли"),
                    Action = Loc(
                        $"🔼 INCREASE worker to {targetCpu:F0} CPU / {targetCpu * 4:F0} GB RAM, or REDUCE to {Math.Max(1, req.WorkerNodeCount - 1)} nodes",
                        $"🔼 ЗБІЛЬШІТЬ вузол до {targetCpu:F0} CPU / {targetCpu * 4:F0} GB RAM, або ЗМЕНШІТЬ до {Math.Max(1, req.WorkerNodeCount - 1)} вузлів")
                });
            }
            else
            {
                list.Add(new AiRecommendation
                {
                    Category = Loc("Pod Density", "Щільність подів"),
                    Severity = "ok",
                    Title = Loc($"✅ Good density: ~{podsPerNode} pods/node", $"✅ Добра щільність: ~{podsPerNode} подів/вузол"),
                    Description = Loc(
                        $"Each worker can run ~{podsPerNode} pods efficiently",
                        $"Кожен вузол може ефективно запустити ~{podsPerNode} подів"),
                    Action = Loc("✓ Keep current configuration", "✓ Залишити поточну конфігурацію")
                });
            }
        }

        return list;
    }

    private List<AiRecommendation> AnalyzeEfficiency(ResourceRequirement req)
    {
        var list = new List<AiRecommendation>();
        var ratio = req.TotalCpu > 0 ? req.TotalRamGb / req.TotalCpu : 0;

        if (ratio < 1)
        {
            var recommendedCpu = req.TotalRamGb * 1.5;
            list.Add(new AiRecommendation
            {
                Category = Loc("CPU/RAM Balance", "Баланс CPU/RAM"),
                Severity = "warning",
                Title = Loc($"🟡 Too much CPU: ratio {ratio:F2}", $"🟡 Надлишок CPU: співвідношення {ratio:F2}"),
                Description = Loc(
                    $"For {req.TotalRamGb:F0} GB RAM you need ~{recommendedCpu:F0} CPU (ratio ~1.5:1). Currently {req.TotalCpu:F0} CPU",
                    $"Для {req.TotalRamGb:F0} GB RAM потрібно ~{recommendedCpu:F0} CPU (співвідн. ~1.5:1). Зараз {req.TotalCpu:F0} CPU"),
                Action = Loc(
                    $"🔽 REDUCE CPU: {req.TotalCpu:F0} → {recommendedCpu:F0} cores. Switch to c-series (compute-optimized)",
                    $"🔽 ЗМЕНШІТЬ CPU: {req.TotalCpu:F0} → {recommendedCpu:F0} ядер. Візьміть c-series (compute-optimized)"),
                PotentialSavings = (req.TotalCpu - recommendedCpu) * 8
            });
        }
        else if (ratio > 6)
        {
            var recommendedRam = req.TotalCpu * 4;
            var reduceRam = req.TotalRamGb - recommendedRam;
            list.Add(new AiRecommendation
            {
                Category = Loc("CPU/RAM Balance", "Баланс CPU/RAM"),
                Severity = "warning",
                Title = Loc($"🟡 Too much RAM: ratio {ratio:F2}", $"🟡 Надлишок RAM: співвідношення {ratio:F2}"),
                Description = Loc(
                    $"For {req.TotalCpu:F0} CPU you need ~{recommendedRam:F0} GB RAM (ratio ~4:1). Currently {req.TotalRamGb:F0} GB",
                    $"Для {req.TotalCpu:F0} CPU потрібно ~{recommendedRam:F0} GB RAM (співвідн. ~4:1). Зараз {req.TotalRamGb:F0} GB"),
                Action = Loc(
                    $"🔽 REDUCE RAM: {req.TotalRamGb:F0} → {recommendedRam:F0} GB. Switch to r-series (memory-optimized)",
                    $"🔽 ЗМЕНШІТЬ RAM: {req.TotalRamGb:F0} → {recommendedRam:F0} GB. Візьміть r-series (memory-optimized)"),
                PotentialSavings = reduceRam * 3
            });
        }
        else
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("CPU/RAM Balance", "Баланс CPU/RAM"),
                Severity = "ok",
                Title = Loc($"✅ Good balance: ratio {ratio:F2} (norm 1-6)", $"✅ Добрий баланс: {ratio:F2} (норма 1-6)"),
                Description = Loc(
                    $"CPU ({req.TotalCpu:F0}) and RAM ({req.TotalRamGb:F0} GB) are well balanced",
                    $"CPU ({req.TotalCpu:F0}) та RAM ({req.TotalRamGb:F0} GB) збалансовані"),
                Action = Loc("✓ Keep current. Use m-series (general purpose)", "✓ Залишити. Використовуйте m-series")
            });
        }

        return list;
    }

    private List<AiRecommendation> AnalyzeScaling(ResourceRequirement req, ProjectConfig config)
    {
        var list = new List<AiRecommendation>();

        if (req.WorkerNodeCount < 3 && config.HaEnabled)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("High Availability", "Відмовостійкість"),
                Severity = "critical",
                Title = Loc($"🔴 Only {req.WorkerNodeCount} nodes — HA requires ≥3!", $"🔴 Лише {req.WorkerNodeCount} вузлів — для HA потрібно ≥3!"),
                Description = Loc(
                    $"With {req.WorkerNodeCount} worker(s) you have NO high availability. One failure = downtime",
                    $"З {req.WorkerNodeCount} вузлом(ами) НЕМАЄ відмовостійкості. Відмова = простій"),
                Action = Loc(
                    $"🔼 INCREASE workers: {req.WorkerNodeCount} → 3+ nodes. Distribute across AZs",
                    $"🔼 ЗБІЛЬШІТЬ вузли: {req.WorkerNodeCount} → 3+. Розподіліть по AZ")
            });
        }
        else if (req.WorkerNodeCount >= 3)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("High Availability", "Відмовостійкість"),
                Severity = "ok",
                Title = Loc($"✅ HA OK: {req.WorkerNodeCount} nodes meet HA requirements", $"✅ HA OK: {req.WorkerNodeCount} вузлів достатньо"),
                Description = Loc(
                    $"{req.WorkerNodeCount} nodes provide fault tolerance for production",
                    $"{req.WorkerNodeCount} вузлів забезпечують відмовостійкість для production"),
                Action = Loc("✓ Keep current. Add pod anti-affinity rules", "✓ Залишити. Додайте anti-affinity для подів")
            });
        }

        if (config.UserCount >= 1000)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("Auto-scaling", "Автомасштабування"),
                Severity = "info",
                Title = Loc($"💡 Enable auto-scaling for {config.UserCount} users", $"💡 Увімкніть автопідсилення для {config.UserCount} користувачів"),
                Description = Loc(
                    "HPA (Horizontal Pod Autoscaler) + Cluster Autoscaler for demand spikes",
                    "HPA (горизонтальне підсилення подів) + Cluster Autoscaler для піків"),
                Action = Loc(
                    "⚙️ SET: min_nodes=3, max_nodes=20, CPU target=70%",
                    "⚙️ НАЛАШТУЙТЕ: min=3, max=20 вузлів, CPU=70%")
            });
        }

        return list;
    }

    private List<AiRecommendation> AnalyzeStorage(ResourceRequirement req)
    {
        var list = new List<AiRecommendation>();
        var tb = req.TotalStorageGb / 1024.0;

        if (tb > 2)
        {
            var recommendedStorage = (int)(tb * 0.6 * 1024);
            list.Add(new AiRecommendation
            {
                Category = Loc("Storage", "Сховище"),
                Severity = "warning",
                Title = Loc($"🟡 High storage: {tb:F1} TB", $"🟡 Багато даних: {tb:F1} TB"),
                Description = Loc(
                    $"Review if all {req.TotalStorageGb} GB needs SSD or can use HDD tiers",
                    $"Перевірте, чи всі {req.TotalStorageGb} GB потребують SSD, чи можна HDD"),
                Action = Loc(
                    $"🔽 REDUCE: separate hot (SSD) and cold (HDD) data. ~60% can be colder",
                    $"🔽 ЗМЕНШІТЬ: розділіть гарячі (SSD) та холодні (HDD) дані. ~60% може бути холодними")
            });
        }

        if (req.TotalIops > 10000)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("IOPS", "IOPS"),
                Severity = "warning",
                Title = Loc($"💡 High IOPS: {req.TotalIops:N0}", $"💡 Високий IOPS: {req.TotalIops:N0}"),
                Description = Loc(
                    $"Ensure IOPS-provisioned volumes (gp3/io2). {req.TotalIops:N0} IOPS needs careful planning",
                    $"Потрібні диски з гарантованим IOPS (gp3/io2). {req.TotalIops:N0} IOPS потребує планування"),
                Action = Loc(
                    "🔼 UPGRADE: gp3 with IOPS provisioning or io2 for > 16000 IOPS",
                    "🔼 ПОКРАЩТЕ: gp3 з IOPS або io2 для > 16000 IOPS")
            });
        }

        if (tb <= 2 && req.TotalIops <= 10000)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("Storage", "Сховище"),
                Severity = "ok",
                Title = Loc($"✅ Storage OK: {req.TotalStorageGb} GB / {req.TotalIops:N0} IOPS", $"✅ Сховище OK: {req.TotalStorageGb} GB / {req.TotalIops:N0} IOPS"),
                Description = Loc(
                    "Storage configuration looks adequate",
                    "Конфігурація сховища достатня"),
                Action = Loc("✓ Keep current storage configuration", "✓ Залишити поточну конфігурацію")
            });
        }

        return list;
    }

    private (string Name, string Description, double MonthlyCost) RecommendInstance(double cpu, double ram)
    {
        return (cpu, ram) switch
        {
            (<= 2, <= 4) => ("t3.medium", Loc("Burstable, low traffic", "Бurstable, низький трафік"), 30),
            (<= 4, <= 16) => ("m5.large", Loc("General purpose, balanced", "Загального призначення"), 70),
            (<= 8, <= 32) => ("m5.xlarge", Loc("General purpose, most workloads", "Загального призначення"), 140),
            (<= 16, <= 64) => ("m5.2xlarge", Loc("General purpose, high perf", "Продуктивний"), 280),
            (<= 32, <= 128) => ("m5.4xlarge", Loc("General purpose, heavy", "Важкі навантаження"), 560),
            (<= 48, <= 192) => ("m5.8xlarge", Loc("General purpose, enterprise", "Корпоративний"), 1120),
            (<= 64, <= 256) => ("c5.9xlarge", Loc("Compute optimized, CPU", "Оптимізований CPU"), 1300),
            _ => ("m5.4xlarge", Loc("General purpose", "Загального призначення"), 560)
        };
    }

    private (double cpu, double ram) GetAvgPodResources(ResourceRequirement req)
    {
        var pods = req.Components.Where(c => c.Cpu > 0).ToList();
        if (pods.Count == 0) return (0.5, 2);
        return (pods.Average(c => c.Cpu), pods.Average(c => c.RamGb));
    }
}
