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
    private readonly AccessService _access;
    private readonly IDialogService? _dialogs;
    private SizingMatrix _matrix;
    private bool _unlocked;

    // Діапазони навантаження (єдиний профіль).
    public ObservableCollection<UserLoadRange> MsSqlRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> AppServerRanges { get; private set; } = new();
    public ObservableCollection<UserLoadRange> WebServerRanges { get; private set; } = new();
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

    // Кнопок «Додати рядок» для таблиць вузлів немає свідомо: модель матриці має рівно
    // вісім слотів (NodeSlot), тож новий рядок без слота нікуди не зберігався б. Раніше
    // тут жили AddRowCommand/AddRowAsync, не прив'язані ні до одного елемента XAML —
    // мертвий код, який виглядав як робоча функція.

    // Допустимі формули реплік для колонки «Формула» (DataGridComboBoxColumn).
    public IReadOnlyList<ReplicaFormula> FormulaOptions { get; } = Enum.GetValues<ReplicaFormula>();

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

    public MatrixViewModel(ILocalizationService loc, MatrixManager matrixManager, AccessService? access = null, IDialogService? dialogs = null)
    {
        _loc = loc;
        _matrixManager = matrixManager;
        _access = access ?? new AccessService();
        _dialogs = dialogs;
        _matrix = matrixManager.Matrix;

        _access.EnsureInitialized();

        SaveMatrixCommand = new RelayCommand(_ => SaveMatrix());
        RecalculateMatrixCommand = new RelayCommand(_ => RecalculateMatrix());
        ResetMatrixCommand = new RelayCommand(_ => ResetMatrix());
    }

    // Розблоковано (пароль підтверджено) — дозволяє змінювати чутливі дані матриці.
    public bool IsUnlocked
    {
        get => _unlocked;
        private set { _unlocked = value; OnPropertyChanged(); }
    }

    public void LoadMatrixGrids()
    {
        MsSqlRanges = new ObservableCollection<UserLoadRange>(_matrix.MsSqlRanges);
        AppServerRanges = new ObservableCollection<UserLoadRange>(_matrix.AppServerRanges);
        WebServerRanges = new ObservableCollection<UserLoadRange>(_matrix.WebServerRanges);
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

    public List<string> SyncGridsToMatrix()
    {
        var errors = _matrixManager.SyncGridsToMatrix(
            MsSqlRanges.ToList(),
            AppServerRanges.ToList(),
            WebServerRanges.ToList(),
            PostgresRanges.ToList(), OracleRanges.ToList(),
            K8sDocumentFlowComponents.ToList(),
            InfraNodes.ToList(), WindowsInfraNodes.ToList(), OptionalInfraNodes.ToList(),
            Engine);
        _matrix = _matrixManager.Matrix;
        return errors;
    }

    private void SaveMatrix()
    {
        _ = SaveMatrixAsync().ContinueWith(t =>
            System.Diagnostics.Debug.WriteLine($"SaveMatrixAsync crashed: {t.Exception?.InnerException?.Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task SaveMatrixAsync()
    {
        if (!await EnsureUnlockedAsync()) return;
        var errors = SyncGridsToMatrix();
        if (errors.Count > 0)
        {
            if (_dialogs is not null)
                await _dialogs.ErrorAsync(string.Join("\n", errors.Take(10)), _loc["dialog.validationError"]);
            return;
        }
        _matrixManager.Save();
        MatrixChanged?.Invoke();
        if (_dialogs is not null) await _dialogs.InfoAsync(_loc["dialog.matrixSaved"], "Info");
    }

    // Перерахувати: застосовує змінені значення грідів до движка одразу (без запису на диск),
    // щоб вплив на розрахунок було видно без збереження матриці.
    private void RecalculateMatrix()
    {
        _ = RecalculateMatrixAsync().ContinueWith(t =>
            System.Diagnostics.Debug.WriteLine($"RecalculateMatrixAsync crashed: {t.Exception?.InnerException?.Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task RecalculateMatrixAsync()
    {
        if (!await EnsureUnlockedAsync()) return;
        var errors = SyncGridsToMatrix();
        if (errors.Count > 0)
        {
            if (_dialogs is not null)
                await _dialogs.ErrorAsync(string.Join("\n", errors.Take(10)), _loc["dialog.validationError"]);
            return;
        }
        MatrixChanged?.Invoke();
    }

    private void ResetMatrix()
    {
        _ = ResetMatrixAsync().ContinueWith(t =>
            System.Diagnostics.Debug.WriteLine($"ResetMatrixAsync crashed: {t.Exception?.InnerException?.Message}"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task ResetMatrixAsync()
    {
        // Деструктивна дія: вимагаємо розблокування і явне підтвердження.
        if (!await EnsureUnlockedAsync()) return;
        if (_dialogs is not null
            && !await _dialogs.ConfirmAsync(_loc["matrix.resetConfirm"], _loc["matrix.reset"]))
            return;
        _matrixManager.Reset();
        _matrix = _matrixManager.Matrix;
        LoadMatrixGrids();
        MatrixChanged?.Invoke();
    }

    // Розблокування паролем (один раз на сесію). Показує діалог з контактами розробника,
    // якщо пароль забуто. Помилка введення не блокує повторні спроби.
    public async Task<bool> EnsureUnlockedAsync()
    {
        if (IsUnlocked) return true;
        if (_dialogs is not null && await _dialogs.ShowPasswordDialogAsync())
        {
            IsUnlocked = true;
            return true;
        }
        return false;
    }

    // Синхронний шим прибрано: GetAwaiter().GetResult() на UI-потоці — гарантований
    // дедлок, а код-біхайнд таблиць працює через асинхронний EnsureUnlockedAsync.

    private void NotifyAllCollections()
    {
        OnPropertyChanged(nameof(MsSqlRanges));
        OnPropertyChanged(nameof(AppServerRanges));
        OnPropertyChanged(nameof(WebServerRanges));
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