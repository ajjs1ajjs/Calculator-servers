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

    public async Task<AiDualProfileResult> AnalyzeAsync(ResourceRequirement req, ProjectConfig config, ResourceRequirement? perfReq = null)
    {
        var result = new AiDualProfileResult();

        if (_api != null)
        {
            try
            {
                var prompt = _api.BuildAnalysisPrompt(req, config, perfReq);
                var response = await _api.GetRecommendation(prompt);
                if (!string.IsNullOrEmpty(response) && !response.StartsWith("AI Error"))
                {
                    var parsed = ParseDualResponse(response);
                    if (parsed != null)
                        return parsed;
                }
            }
            catch { }
        }

        result.Balance.Recommendations = Analyze(req, config);
        result.Balance.Infrastructure = BuildAiInfrastructure(req, config);
        if (perfReq != null)
        {
            result.Performance.Recommendations = Analyze(perfReq, config);
            result.Performance.Infrastructure = BuildAiInfrastructure(perfReq, config);
        }
        return result;
    }

    public List<AiRecommendation> Analyze(ResourceRequirement req, ProjectConfig config)
    {
        var recommendations = new List<AiRecommendation>();
        recommendations.AddRange(AnalyzeInstanceFit(req));
        recommendations.AddRange(AnalyzeEfficiency(req));
        recommendations.AddRange(AnalyzeScaling(req, config));
        recommendations.AddRange(AnalyzeStorage(req, config));
        recommendations.AddRange(AnalyzeProductSpecific(req, config));
        recommendations.AddRange(AnalyzeSqlConfiguration(req));
        recommendations.AddRange(AnalyzeDeploymentFit(req, config));
        recommendations.AddRange(AnalyzeGpuRequirements(req, config));
        recommendations.AddRange(AnalyzeRedisRequirements(req, config));
        return recommendations;
    }

    private AiDualProfileResult? ParseDualResponse(string json)
    {
        try
        {
            json = json.Trim();
            var m = System.Text.RegularExpressions.Regex.Match(json, "```(?:json)?\\s*([\\s\\S]*?)\\s*```");
            if (m.Success) json = m.Groups[1].Value.Trim();

            var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = new AiDualProfileResult();

            foreach (var (key, profile) in new[] { ("balance", result.Balance), ("performance", result.Performance) })
            {
                if (!root.TryGetProperty(key, out var section) || section.ValueKind != JsonValueKind.Object)
                    continue;

                if (section.TryGetProperty("recommendations", out var recs) && recs.ValueKind == JsonValueKind.Array)
                {
                    var r = JsonSerializer.Deserialize<List<AiRecommendation>>(recs.GetRawText(), opt);
                    if (r != null) profile.Recommendations = r;
                }

                if (section.TryGetProperty("infrastructure", out var infra) && infra.ValueKind == JsonValueKind.Array)
                {
                    foreach (var n in infra.EnumerateArray())
                    {
                        profile.Infrastructure.Add(new InfrastructureNode
                        {
                            Name = n.TryGetProperty("name", out var nn) ? nn.GetString() ?? "" : "",
                            Cpu = n.TryGetProperty("cpu", out var nc) ? nc.GetDouble() : 0,
                            RamGb = n.TryGetProperty("ramGb", out var nr) ? nr.GetDouble() : 0,
                            NodeCount = n.TryGetProperty("nodeCount", out var ncnt) ? ncnt.GetInt32() : 1,
                            StorageGb = n.TryGetProperty("storageGb", out var ns) ? ns.GetInt32() : 0
                        });
                    }
                }
            }

            return result;
        }
        catch
        {
            return null;
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

    private List<AiRecommendation> AnalyzeStorage(ResourceRequirement req, ProjectConfig config)
    {
        var list = new List<AiRecommendation>();
        var tb = req.TotalStorageGb / 1024.0;
        var isDocFlow = config.ProductType == ProductType.DocumentFlow;
        var iopsThreshold = isDocFlow ? 5000 : 10000;

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

        if (req.TotalIops > iopsThreshold)
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

        if (tb <= 2 && req.TotalIops <= iopsThreshold)
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

    private List<AiRecommendation> AnalyzeProductSpecific(ResourceRequirement req, ProjectConfig config)
    {
        var list = new List<AiRecommendation>();
        var isDocFlow = config.ProductType == ProductType.DocumentFlow;

        if (isDocFlow)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("Product Profile", "Профіль продукту"),
                Severity = "info",
                Title = Loc(" DocumentFlow: higher resource requirements", "📋 Документообіг: вищі вимоги до ресурсів"),
                Description = Loc(
                    "DocumentFlow uses ~30% more CPU/RAM per pod and 2.4x higher IOPS for AppServers",
                    "Документообіг використовує ~30% більше CPU/RAM на под та 2.4x вищий IOPS для AppServer"),
                Action = Loc(
                    "Ensure io2/gp3 volumes with 1200+ IOPS. Consider larger worker nodes (16 CPU / 64 GB)",
                    "Використовуйте io2/gp3 з 1200+ IOPS. Розгляньте більші вузли (16 CPU / 64 GB)")
            });

            if (req.TotalIops > 1200)
            {
                list.Add(new AiRecommendation
                {
                    Category = Loc("DocumentFlow IOPS", "IOPS Документообіг"),
                    Severity = "warning",
                    Title = Loc($"🟡 DocumentFlow IOPS: {req.TotalIops:N0} (base 1200/server)", $"🟡 IOPS Документообіг: {req.TotalIops:N0} (база 1200/сервер)"),
                    Description = Loc(
                        "DocumentFlow AppServers require 1200 IOPS each vs 500 for Standard",
                        "AppServer Документообіг потребує 1200 IOPS кожен проти 500 для Стандарт"),
                    Action = Loc(
                        " PROVISION: io2 with 3000+ IOPS or gp3 with IOPS burst",
                        "🔼 ВИДІЛІТЬ: io2 з 3000+ IOPS або gp3 з burst IOPS")
                });
            }
        }
        else
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("Product Profile", "Профіль продукту"),
                Severity = "ok",
                Title = Loc("✅ Standard: baseline resource requirements", "✅ Стандарт: базові вимоги до ресурсів"),
                Description = Loc(
                    "Standard uses baseline resources. AppServers require 500 IOPS each",
                    "Стандарт використовує базові ресурси. AppServer потребує 500 IOPS кожен"),
                Action = Loc("✓ gp3 volumes sufficient for Standard workloads", "✓ gp3 диски достатні для Стандарт")
            });
        }

        return list;
    }

    public List<InfrastructureNode> BuildAiInfrastructure(ResourceRequirement req, ProjectConfig config)
    {
        var nodes = new List<InfrastructureNode>();
        var (instanceType, _, _) = RecommendInstance(
            req.WorkerNodeCount > 0 ? req.TotalCpu / req.WorkerNodeCount : 0,
            req.WorkerNodeCount > 0 ? req.TotalRamGb / req.WorkerNodeCount : 0);

        if (config.DeploymentType == DeploymentType.Kubernetes || config.DeploymentType == DeploymentType.Hybrid)
        {
            var workerCpu = req.WorkerNodeCount > 0 ? Math.Ceiling(req.TotalCpu / req.WorkerNodeCount) : 8;
            var workerRam = req.WorkerNodeCount > 0 ? Math.Ceiling(req.TotalRamGb / req.WorkerNodeCount) : 32;
            var aiWorkers = Math.Max(3, req.WorkerNodeCount + 1);

            nodes.Add(new InfrastructureNode
            {
                Name = "SQL Server",
                Os = "PaaS", Cpu = Math.Ceiling(req.TotalCpu * 0.1),
                RamGb = Math.Ceiling(req.TotalRamGb * 0.2), NodeCount = 1,
                StorageGb = Math.Max(500, req.TotalStorageGb / 3), StorageType = "Premium SSD"
            });

            nodes.Add(new InfrastructureNode
            {
                Name = "Master Node",
                Os = "Ubuntu 24.04 LTS", Cpu = 4, RamGb = 8,
                NodeCount = 3, StorageGb = 100, StorageType = "SSD"
            });

            nodes.Add(new InfrastructureNode
            {
                Name = "Worker Node",
                Os = "Ubuntu 24.04 LTS", Cpu = workerCpu, RamGb = workerRam,
                NodeCount = aiWorkers, StorageGb = 200, StorageType = instanceType
            });
        }

        if (config.DeploymentType == DeploymentType.Windows || config.DeploymentType == DeploymentType.Hybrid)
        {
            nodes.Add(new InfrastructureNode
            {
                Name = "App Server",
                Os = "Windows Server 2025", Cpu = 8, RamGb = 32,
                NodeCount = Math.Max(2, req.WorkerNodeCount / 2), StorageGb = 150, StorageType = "SSD"
            });
            nodes.Add(new InfrastructureNode
            {
                Name = "Web Server (IIS)",
                Os = "Windows Server 2025", Cpu = 4, RamGb = 16,
                NodeCount = Math.Max(2, req.WorkerNodeCount / 2), StorageGb = 150, StorageType = "SSD"
            });
        }

        return nodes;
    }

    private (string Name, string Description, double MonthlyCost) RecommendInstance(double cpu, double ram)
    {
        return (cpu, ram) switch
        {
            (<= 2, <= 4) => ("Small (2 vCPU / 4 GB)", Loc("Burstable, dev/test", "Burstable, розробка/тест"), 30),
            (<= 2, <= 8) => ("Small (2 vCPU / 8 GB)", Loc("Small workloads", "Малі навантаження"), 70),
            (<= 4, <= 16) => ("Medium (4 vCPU / 16 GB)", Loc("Balanced, most apps", "Збалансоване"), 140),
            (<= 8, <= 32) => ("Medium (8 vCPU / 32 GB)", Loc("Standard workloads", "Стандартне"), 280),
            (<= 16, <= 64) => ("Large (16 vCPU / 64 GB)", Loc("High performance", "Продуктивне"), 560),
            (<= 32, <= 128) => ("Large (32 vCPU / 128 GB)", Loc("Heavy workloads", "Важкі навантаження"), 1120),
            (<= 64, <= 256) => ("XLarge (64 vCPU / 256 GB)", Loc("Compute optimized", "Оптимізоване CPU"), 1300),
            _ => ("Large (32 vCPU / 128 GB)", Loc("General purpose", "Загальне призначення"), 1120)
        };
    }

    private (double cpu, double ram) GetAvgPodResources(ResourceRequirement req)
    {
        var pods = req.Components.Where(c => c.Cpu > 0).ToList();
        if (pods.Count == 0) return (0.5, 2);
        return (pods.Average(c => c.Cpu), pods.Average(c => c.RamGb));
    }

    private List<AiRecommendation> AnalyzeSqlConfiguration(ResourceRequirement req)
    {
        var list = new List<AiRecommendation>();
        var sqlNode = req.Infrastructure.FirstOrDefault(n => n.Name.Contains("SQL"));
        if (sqlNode == null) return list;

        var hasPageFile = sqlNode.PageFileGb > 0;
        if (req.DeploymentType == DeploymentType.Windows && !hasPageFile)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("SQL Server", "SQL Server"),
                Severity = "warning",
                Title = Loc(" No page file configured for SQL Server", " Немає файлу підкачки для SQL Server"),
                Description = Loc(
                    "SQL Server on Windows requires a page file equal to RAM size for crash dumps and memory pressure",
                    "SQL Server на Windows потребує файл підкачки = об'єму RAM для дампів пам'яті"),
                Action = Loc(
                    " SET PageFile = RAM size (min 16 GB) on dedicated disk separate from SQL data/logs",
                    " НАЛАШТУЙТЕ PageFile = RAM (мін 16 ГБ) на окремому диску від даних SQL")
            });
        }

        var diskCount = 1;
        if (sqlNode.StorageGb2 > 0) diskCount++;
        if (sqlNode.StorageGb3 > 0) diskCount++;
        if (sqlNode.StorageGb4 > 0) diskCount++;

        if (diskCount < 3 && req.TotalRamGb > 64)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("SQL Server", "SQL Server"),
                Severity = "info",
                Title = Loc($" Separate SQL disks: only {diskCount} disk(s) for {req.TotalRamGb:F0} GB RAM",
                    $" Розділення дисків SQL: лише {diskCount} диск(и) для {req.TotalRamGb:F0} ГБ RAM"),
                Description = Loc(
                    "SQL Server performs best with data, logs, and tempdb on separate physical disks",
                    "SQL Server працює найкраще, коли дані, логи та tempdb на окремих дисках"),
                Action = Loc(
                    " RESTRUCTURE: Disk1=OS/pagefile, Disk2=SQL data, Disk3=SQL logs, Disk4=tempdb",
                    " ПЕРЕРОБІТЬ: Диск1=ОС/pagefile, Диск2=дані SQL, Диск3=логи SQL, Диск4=tempdb")
            });
        }

        var sqlRamGb = sqlNode.RamGb;
        var sqlCpu = sqlNode.Cpu;
        if (sqlRamGb > 128 && sqlCpu < 16)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("SQL Server", "SQL Server"),
                Severity = "warning",
                Title = Loc($" SQL: {sqlRamGb:F0} GB RAM but only {sqlCpu:F0} vCPU",
                    $" SQL: {sqlRamGb:F0} ГБ RAM але лише {sqlCpu:F0} vCPU"),
                Description = Loc(
                    "SQL Server with >128 GB RAM needs 16+ vCPU to utilize memory effectively (max server memory setting)",
                    "SQL Server з >128 ГБ RAM потребує 16+ vCPU для ефективного використання пам'яті"),
                Action = Loc(
                    $" UPGRADE SQL vCPU: {sqlCpu:F0} → 16+ cores. Set max server memory = {sqlRamGb - 4:F0} GB",
                    $" ЗБІЛЬШІТЬ vCPU SQL: {sqlCpu:F0} → 16+. Обмежте max server memory = {sqlRamGb - 4:F0} ГБ")
            });
        }

        if (sqlRamGb > 0 && sqlRamGb < 12 && req.UserCount > 100)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("SQL Server", "SQL Server"),
                Severity = "warning",
                Title = Loc($" SQL Server under-provisioned: {sqlRamGb:F0} GB for {req.UserCount} users",
                    $" SQL Server недостатньо: {sqlRamGb:F0} ГБ для {req.UserCount} користувачів"),
                Description = Loc(
                    $"{req.UserCount} users typically need {Math.Max(16, req.UserCount / 10)} GB+ SQL RAM",
                    $"Для {req.UserCount} користувачів потрібно {Math.Max(16, req.UserCount / 10)} ГБ+ SQL RAM"),
                Action = Loc(
                    $" INCREASE SQL RAM: {sqlRamGb:F0} → {Math.Max(16, req.UserCount / 10)} GB",
                    $" ЗБІЛЬШІТЬ RAM SQL: {sqlRamGb:F0} → {Math.Max(16, req.UserCount / 10)} ГБ")
            });
        }

        return list;
    }

    private List<AiRecommendation> AnalyzeDeploymentFit(ResourceRequirement req, ProjectConfig config)
    {
        var list = new List<AiRecommendation>();
        var hasK8sModules = req.Components.Any(c => !c.Category.Contains("Windows"));
        var hasWindowsModules = req.Components.Any(c => c.Category.Contains("Windows"));

        if (config.DeploymentType == DeploymentType.Kubernetes && hasWindowsModules)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("Deployment Fit", "Відповідність розгортанню"),
                Severity = "info",
                Title = Loc(" Windows modules selected but deployment is K8s",
                    " Вибрано Windows модулі, але розгортання K8s"),
                Description = Loc(
                    "Windows Infrastructure module will be excluded in K8s calculation. Switch to Hybrid if both needed",
                    "Windows Infrastructure модуль буде виключено. Перемкніться на Гібрид якщо потрібні обидва"),
                Action = Loc(
                    " Either disable Windows Infrastructure, or switch to Hybrid deployment",
                    " Або вимкніть Windows Infrastructure, або перемкніться на Гібрид")
            });
        }

        if (config.DeploymentType == DeploymentType.Windows && hasK8sModules && !hasWindowsModules)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("Deployment Fit", "Відповідність розгортанню"),
                Severity = "info",
                Title = Loc(" K8s modules selected but deployment is Windows — result may be empty",
                    " Вибрано K8s модулі, але розгортання Windows — результат може бути пустим"),
                Description = Loc(
                    "Windows calculation only includes modules with 'Windows' in name. Enable 'Windows Infrastructure' module",
                    "Windows розрахунок враховує лише модулі з 'Windows' в назві. Увімкніть 'Windows Infrastructure'"),
                Action = Loc(
                    " Enable 'Windows Infrastructure' module or switch deployment type",
                    " Увімкніть модуль 'Windows Infrastructure' або змініть тип розгортання")
            });
        }

        return list;
    }

    private List<AiRecommendation> AnalyzeGpuRequirements(ResourceRequirement req, ProjectConfig config)
    {
        var list = new List<AiRecommendation>();
        var gpuComponents = req.Components
            .Where(c => c.Name.Contains("Video", StringComparison.OrdinalIgnoreCase)
                || c.Name.Contains("Transcod", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (gpuComponents.Count > 0)
        {
            list.Add(new AiRecommendation
            {
                Category = Loc("GPU Requirements", "Вимоги до GPU"),
                Severity = "warning",
                Title = Loc(" GPU required for video transcoding components",
                    " Потрібен GPU для компонентів відеотранскодингу"),
                Description = Loc(
                    $"Video components ({string.Join(", ", gpuComponents.Select(c => c.Name))}) need GPU acceleration",
                    $"Відео-компоненти ({string.Join(", ", gpuComponents.Select(c => c.Name))}) потребують GPU прискорення"),
                Action = Loc(
                    " ADD: 1x GPU node per 100 users (e.g. NVIDIA T4, A10, or L4). Consider GPU-optimized VMs (NCas/NV series)",
                    " ДОДАЙТЕ: 1x GPU вузол на 100 користувачів (NVIDIA T4, A10, L4). GPU-оптимізовані віртуалки (NCas/NV)")
            });
        }

        return list;
    }

    private List<AiRecommendation> AnalyzeRedisRequirements(ResourceRequirement req, ProjectConfig config)
    {
        var list = new List<AiRecommendation>();
        var redisComponents = req.Components.Where(c => c.HasRedis).ToList();

        if (redisComponents.Count > 0)
        {
            var totalRedisRam = redisComponents.Sum(c => c.RamGb);
            var userScale = config.UserCount > 1000;

            if (userScale)
            {
                list.Add(new AiRecommendation
                {
                    Category = Loc("Redis Cache", "Кеш Redis"),
                    Severity = "info",
                    Title = Loc($" Redis cluster recommended for {config.UserCount} users ({redisComponents.Count} components with Redis)",
                        $" Рекомендовано Redis кластер для {config.UserCount} користувачів ({redisComponents.Count} компонентів з Redis)"),
                    Description = Loc(
                        $"Total Redis memory needed: ~{totalRedisRam:F1} GB on pods. Consider dedicated Redis cluster",
                        $"Всього пам'яті Redis: ~{totalRedisRam:F1} GB на подах. Розгляньте виділений Redis кластер"),
                    Action = Loc(
                        " DEPLOY: dedicated Redis cluster (3+ nodes), maxmemory-policy=allkeys-lru, persistence=AOF",
                        " РОЗГОРНІТЬ: виділений Redis кластер (3+ вузли), maxmemory-policy=allkeys-lru, AOF")
                });
            }
            else
            {
                list.Add(new AiRecommendation
                {
                    Category = Loc("Redis Cache", "Кеш Redis"),
                    Severity = "ok",
                    Title = Loc($" Redis in-pod adequate for {config.UserCount} users",
                        $" Redis в поді достатній для {config.UserCount} користувачів"),
                    Description = Loc(
                        "In-pod Redis container with 0.1-0.2 GB per AS/ROBOT pod handles low-moderate cache load",
                        "Redis контейнер в поді (0.1-0.2 GB на AS/ROBOT под) витримує низько-середнє навантаження"),
                    Action = Loc(" Ensure persistent volume for Redis in production", " Забезпечте persistent volume для Redis в production")
                });
            }
        }

        return list;
    }
}
