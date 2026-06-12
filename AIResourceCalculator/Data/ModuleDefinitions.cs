using AIResourceCalculator.Models;

namespace AIResourceCalculator.Data;

public static class ModuleDefinitions
{
    public static List<ProjectModule> GetAllModules()
    {
        return new List<ProjectModule>
        {
            AppServerModule(),
            RobotModule(),
            WebModule(),
            ForceBpmModule(),
            LmsModule(),
            HrPortalModule(),
            WindowsInfraModule()
        };
    }

    private static ProjectModule AppServerModule()
    {
        return new ProjectModule
        {
            Name = "App Server",
            Description = "Core application server with local SQL and Redis cache",
            IsEnabled = true,
            Components = new List<ModuleComponent>
            {
                new() { Name = "AS (App Server)", Cpu = 1.0, RamGb = 8, PerfCpu = 1.3, PerfRamGb = 10, Formula = ReplicaFormula.Per25Users },
                new() { Name = "AS-Local SQL", Cpu = 1.0, RamGb = 3, PerfCpu = 1.0, PerfRamGb = 5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                new() { Name = "AS-Redis", Cpu = 0.1, RamGb = 0.1, PerfCpu = 0.2, PerfRamGb = 0.2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasRedis = true }
            }
        };
    }

    private static ProjectModule RobotModule()
    {
        return new ProjectModule
        {
            Name = "ROBOT",
            Description = "Robot process automation services",
            IsEnabled = true,
            Components = new List<ModuleComponent>
            {
                new() { Name = "ROBOT", Cpu = 1.0, RamGb = 8, PerfCpu = 1.3, PerfRamGb = 10, Formula = ReplicaFormula.Per100Plus1000 },
                new() { Name = "ROBOT-Local SQL", Cpu = 1.0, RamGb = 3, PerfCpu = 1.0, PerfRamGb = 5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                new() { Name = "ROBOT-Redis", Cpu = 0.1, RamGb = 0.1, PerfCpu = 0.2, PerfRamGb = 0.2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasRedis = true }
            }
        };
    }

    private static ProjectModule WebModule()
    {
        return new ProjectModule
        {
            Name = "Web",
            Description = "Web services including WebSocket and SmartID",
            IsEnabled = true,
            Components = new List<ModuleComponent>
            {
                new() { Name = "Webrmd", Cpu = 0.2, RamGb = 1.5, Formula = ReplicaFormula.Per25Users },
                new() { Name = "SmartID", Cpu = 0.2, RamGb = 0.5, Formula = ReplicaFormula.Per25Users },
                new() { Name = "WS (WebSocket)", Cpu = 0.25, RamGb = 0.5, PerfCpu = 0.35, PerfRamGb = 0.6, Formula = ReplicaFormula.Per50Plus500 },
                new() { Name = "WS-SignalR", Cpu = 0.25, RamGb = 0.5, Formula = ReplicaFormula.Per25Users }
            }
        };
    }

    private static ProjectModule ForceBpmModule()
    {
        return new ProjectModule
        {
            Name = "ForceBPM",
            Description = "Business process management engine and tools",
            IsEnabled = true,
            Components = new List<ModuleComponent>
            {
                new() { Name = "GraphQL", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Per25Users },
                new() { Name = "ForceBPM Engine", Cpu = 1.0, RamGb = 4, Formula = ReplicaFormula.OnePlusPer100, HasLocalSql = true },
                new() { Name = "ForceBPM Modeler", Cpu = 0.5, RamGb = 0.5, Formula = ReplicaFormula.Fixed, FixedReplicas = 1 },
                new() { Name = "ForceBPM Processes", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Per25Users },
                new() { Name = "ForceBPM Tasks", Cpu = 0.3, RamGb = 2, Formula = ReplicaFormula.Per25Users },
                new() { Name = "ForceBPM Tasks-Graphql", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Per25Users }
            }
        };
    }

    private static ProjectModule LmsModule()
    {
        return new ProjectModule
        {
            Name = "LMS",
            Description = "Learning management system with video utilities",
            IsEnabled = false,
            Components = new List<ModuleComponent>
            {
                new() { Name = "LMS-SmartID", Cpu = 0.006, RamGb = 0.05, Formula = ReplicaFormula.Per25Users },
                new() { Name = "LMS", Cpu = 0.3, RamGb = 1, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                new() { Name = "LMS-GraphQL", Cpu = 0.09, RamGb = 0.3, Formula = ReplicaFormula.Per25Users },
                new() { Name = "LMS-Videoutilities", Cpu = 4.0, RamGb = 6, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true, Notes = "Requires GPU for video transcoding" },
                new() { Name = "LMS-Fileserver", Cpu = 0.5, RamGb = 8, Formula = ReplicaFormula.Fixed, FixedReplicas = 1 }
            }
        };
    }

    private static ProjectModule HrPortalModule()
    {
        return new ProjectModule
        {
            Name = "HR Portal",
            Description = "HR self-service portal with modeler and player",
            IsEnabled = false,
            Components = new List<ModuleComponent>
            {
                new() { Name = "HR-SmartID", Cpu = 0.006, RamGb = 0.05, Formula = ReplicaFormula.Per100Users },
                new() { Name = "HR-GraphQL", Cpu = 0.01, RamGb = 0.06, Formula = ReplicaFormula.Per100Users },
                new() { Name = "WebAppModeler", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true },
                new() { Name = "CommonAppPlayer", Cpu = 0.5, RamGb = 2, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, HasLocalSql = true }
            }
        };
    }

    private static ProjectModule WindowsInfraModule()
    {
        return new ProjectModule
        {
            Name = "Windows Infrastructure",
            Description = "Windows App Servers and Web Servers",
            IsEnabled = true,
            Components = new List<ModuleComponent>
            {
                new() { Name = "Windows App Server", Cpu = 4.0, RamGb = 16, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, Notes = "Per Windows deployment VM" },
                new() { Name = "Windows Web Server", Cpu = 4.0, RamGb = 8, Formula = ReplicaFormula.Fixed, FixedReplicas = 1, Notes = "Per Windows deployment VM" }
            }
        };
    }
}
