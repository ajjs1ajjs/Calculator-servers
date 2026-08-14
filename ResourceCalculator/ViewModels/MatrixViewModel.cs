using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ResourceCalculator.Data;
using ResourceCalculator.Interfaces;
using ResourceCalculator.Localization;
using ResourceCalculator.Models;
using ResourceCalculator.Services;

namespace ResourceCalculator.ViewModels;

public class MatrixViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _loc;
    private readonly MatrixManager _matrixManager;
    private SizingMatrix _matrix;

    // Діапазони навантаження (усі типи/профілі).
    public ObservableCollection<UserLoadRange> MsSqlRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> MsSqlPerformanceRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> AppServerRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> AppServerPerformanceRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> WebServerRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> WebServerPerformanceRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> PostgresRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> OracleRanges { get; private set; } = new();

    // Компоненти (поди) K8s — сплющені з модулів у грид із колонкою «Категорія/Модуль».
    public ObservableCollection<ServiceComponent> K8sDocumentFlowComponents { get; private set; } = new();

    // Вузли інфраструктури (усі: K8s + Windows + опціональні).
    public ObservableCollection<InfrastructureNode> InfraNodes { get; private set; } = new();
    public ObservableCollection<InfrastructureNode> WindowsInfraNodes { get; private set; } = new();
    public ObservableCollection<InfrastructureNode> OptionalInfraNodes { get; private set; } = new();

    // Налаштування рушія (редагування через матрицю).
    public EngineSettings Engine { get; private set; } = new();

    public ICommand SaveMatrixCommand { get; }
    public ICommand RecalculateMatrixCommand { get; }
    public ICommand ResetMatrixCommand { get; }

    public event System.Action? MatrixChanged;

    public MatrixManager Manager => _matrixManager;
    public SizingMatrix Matrix
    {
        get => _matrix;
        set { _matrix = value; LoadMatrixGrids(); }
    }

    public MatrixViewModel(ILocalizationService loc, MatrixManager matrixManager)
    {
        _loc = loc;
        _matrixManager = matrixManager;
        _matrix = matrixManager.Matrix;

        SaveMatrixCommand = new RelayCommand(_ => SaveMatrix());
        RecalculateMatrixCommand = new RelayCommand(_ => RecalculateMatrix());
        ResetMatrixCommand = new RelayCommand(_ => ResetMatrix());
    }

    public void LoadMatrixGrids()
    {
        MsSqlRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlRanges);
        MsSqlPerformanceRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlPerformanceRanges);
        AppServerRanges = new ObservableCollection<UserLoadRange>(_matrix.AppServerRanges);
        AppServerPerformanceRanges = new ObservableCollection<UserLoadRange>(_matrix.AppServerPerformanceRanges);
        WebServerRanges = new ObservableCollection<UserLoadRange>(_matrix.WebServerRanges);
        WebServerPerformanceRanges = new ObservableCollection<UserLoadRange>(_matrix.WebServerPerformanceRanges);
        PostgresRanges = new ObservableCollection<UserLoadRange>(_matrix.PostgresRanges);
        OracleRanges = new ObservableCollection<UserLoadRange>(_matrix.OracleRanges);

        K8sDocumentFlowComponents = new ObservableCollection<ServiceComponent>(
            _matrix.DocumentFlowModules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb, PerfCpu = c.PerfCpu, PerfRamGb = c.PerfRamGb,
                Replicas = c.FixedReplicas, FixedReplicas = c.FixedReplicas,
                Formula = c.Formula, Category = m.Name, HasLocalSql = c.HasLocalSql, HasRedis = c.HasRedis
            }))
        );

        InfraNodes = new ObservableCollection<InfrastructureNode>();
        if (_matrix.DefaultK8sSql != null) InfraNodes.Add(_matrix.DefaultK8sSql);
        if (_matrix.DefaultK8sMaster != null) InfraNodes.Add(_matrix.DefaultK8sMaster);
        if (_matrix.DefaultK8sWorker != null) InfraNodes.Add(_matrix.DefaultK8sWorker);

        WindowsInfraNodes = new ObservableCollection<InfrastructureNode>();
        if (_matrix.DefaultWindowsSql != null) WindowsInfraNodes.Add(_matrix.DefaultWindowsSql);
        if (_matrix.DefaultWindowsApp != null) WindowsInfraNodes.Add(_matrix.DefaultWindowsApp);
        if (_matrix.DefaultWindowsWeb != null) WindowsInfraNodes.Add(_matrix.DefaultWindowsWeb);

        OptionalInfraNodes = new ObservableCollection<InfrastructureNode>();
        if (_matrix.DefaultReportingServer != null) OptionalInfraNodes.Add(_matrix.DefaultReportingServer);
        if (_matrix.DefaultHaProxy != null) OptionalInfraNodes.Add(_matrix.DefaultHaProxy);

        Engine = _matrix.Engine?.Clone() ?? new EngineSettings();

        NotifyAllCollections();
        OnPropertyChanged(nameof(Engine));
    }

    public void SyncGridsToMatrix()
    {
        _matrixManager.SyncGridsToMatrix(
            MsSqlRanges.ToList(), MsSqlPerformanceRanges.ToList(),
            AppServerRanges.ToList(), AppServerPerformanceRanges.ToList(),
            WebServerRanges.ToList(), WebServerPerformanceRanges.ToList(),
            PostgresRanges.ToList(), OracleRanges.ToList(),
            K8sDocumentFlowComponents.ToList(),
            InfraNodes.ToList(), WindowsInfraNodes.ToList(), OptionalInfraNodes.ToList(),
            Engine);
        _matrix = _matrixManager.Matrix;
    }

    private void SaveMatrix()
    {
        SyncGridsToMatrix();
        _matrixManager.Save();
        MatrixChanged?.Invoke();
        System.Windows.MessageBox.Show(_loc["dialog.matrixSaved"], "Info",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    // Перерахувати: застосовує змінені значення грідів до движка одразу (без запису на диск),
    // щоб вплив на розрахунок було видно без збереження матриці.
    private void RecalculateMatrix()
    {
        SyncGridsToMatrix();
        MatrixChanged?.Invoke();
    }

    private void ResetMatrix()
    {
        _matrixManager.Reset();
        _matrix = _matrixManager.Matrix;
        LoadMatrixGrids();
        MatrixChanged?.Invoke();
    }

    private void NotifyAllCollections()
    {
        OnPropertyChanged(nameof(MsSqlRanges));
        OnPropertyChanged(nameof(MsSqlPerformanceRanges));
        OnPropertyChanged(nameof(AppServerRanges));
        OnPropertyChanged(nameof(AppServerPerformanceRanges));
        OnPropertyChanged(nameof(WebServerRanges));
        OnPropertyChanged(nameof(WebServerPerformanceRanges));
        OnPropertyChanged(nameof(PostgresRanges));
        OnPropertyChanged(nameof(OracleRanges));
        OnPropertyChanged(nameof(K8sDocumentFlowComponents));
        OnPropertyChanged(nameof(InfraNodes));
        OnPropertyChanged(nameof(WindowsInfraNodes));
        OnPropertyChanged(nameof(OptionalInfraNodes));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
