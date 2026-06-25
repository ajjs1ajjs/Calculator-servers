using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AIResourceCalculator.Data;
using AIResourceCalculator.Interfaces;
using AIResourceCalculator.Localization;
using AIResourceCalculator.Models;
using AIResourceCalculator.Services;

namespace AIResourceCalculator.ViewModels;

public class MatrixViewModel : INotifyPropertyChanged
{
    private readonly ILocalizationService _loc;
    private readonly MatrixManager _matrixManager;
    private SizingMatrix _matrix;

    public ObservableCollection<UserLoadRange> MsSqlRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> MsSqlPerformanceRanges { get; private set; } = new();
    public ObservableCollection<ServiceComponent> K8sStandardComponents { get; private set; } = new();
    public ObservableCollection<ServiceComponent> K8sDocumentFlowComponents { get; private set; } = new();
    public ObservableCollection<InfrastructureNode> InfraNodes { get; private set; } = new();

    public ICommand SaveMatrixCommand { get; }
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
        ResetMatrixCommand = new RelayCommand(_ => ResetMatrix());
    }

    public void LoadMatrixGrids()
    {
        MsSqlRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlRanges);
        MsSqlPerformanceRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlPerformanceRanges);

        K8sStandardComponents = new ObservableCollection<ServiceComponent>(
            _matrix.StandardModules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                Replicas = c.FixedReplicas, FixedReplicas = c.FixedReplicas,
                Formula = c.Formula, Category = m.Name
            }))
        );

        K8sDocumentFlowComponents = new ObservableCollection<ServiceComponent>(
            _matrix.DocumentFlowModules.SelectMany(m => m.Components.Select(c => new ServiceComponent
            {
                Name = c.Name, Cpu = c.Cpu, RamGb = c.RamGb,
                Replicas = c.FixedReplicas, FixedReplicas = c.FixedReplicas,
                Formula = c.Formula, Category = m.Name
            }))
        );

        InfraNodes = new ObservableCollection<InfrastructureNode>();
        if (_matrix.DefaultK8sSql != null) InfraNodes.Add(_matrix.DefaultK8sSql);
        if (_matrix.DefaultK8sMaster != null) InfraNodes.Add(_matrix.DefaultK8sMaster);
        if (_matrix.DefaultK8sWorker != null) InfraNodes.Add(_matrix.DefaultK8sWorker);

        OnPropertyChanged(nameof(MsSqlRanges));
        OnPropertyChanged(nameof(MsSqlPerformanceRanges));
        OnPropertyChanged(nameof(K8sStandardComponents));
        OnPropertyChanged(nameof(K8sDocumentFlowComponents));
        OnPropertyChanged(nameof(InfraNodes));
    }

    public void SyncGridsToMatrix()
    {
        _matrixManager.SyncGridsToMatrix(
            MsSqlRanges.ToList(), MsSqlPerformanceRanges.ToList(),
            K8sStandardComponents.ToList(), K8sDocumentFlowComponents.ToList(),
            new List<ServiceComponent>(), InfraNodes.ToList());
        _matrix = _matrixManager.Matrix;
    }

    private void SaveMatrix()
    {
        SyncGridsToMatrix();
        _matrixManager.Save();
        System.Windows.MessageBox.Show(_loc["dialog.matrixSaved"], "Info",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    private void ResetMatrix()
    {
        _matrixManager.Reset();
        _matrix = _matrixManager.Matrix;
        LoadMatrixGrids();
        MatrixChanged?.Invoke();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}