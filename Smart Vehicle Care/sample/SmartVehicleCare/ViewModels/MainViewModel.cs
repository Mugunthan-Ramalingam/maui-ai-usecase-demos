using Microsoft.Maui.Graphics;
using SmartVehicleCare.Models;
using SmartVehicleCare.Services;
using SmartVehicleCare.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SmartVehicleCare.Models;
using SmartVehicleCare.Services;

namespace SmartVehicleCare.ViewModels;

public enum NavSection { Dashboard, VehicleCenter, AIAssist, ServiceCenter, Settings }

public class MainViewModel : INotifyPropertyChanged
{
    private NavSection _currentSection = NavSection.Dashboard;
    private bool _hasSkippedApiKeyThisSession;

    // ── API Key Gate ──────────────────────────────────────────────────────────

    private bool _isApiKeyPromptVisible;
    public bool IsApiKeyPromptVisible
    {
        get => _isApiKeyPromptVisible;
        set => SetProperty(ref _isApiKeyPromptVisible, value);
    }

    private string _promptApiKeyEntry = string.Empty;
    public string PromptApiKeyEntry
    {
        get => _promptApiKeyEntry;
        set => SetProperty(ref _promptApiKeyEntry, value);
    }

    public ICommand PromptSaveApiKeyCommand { get; }
    public ICommand PromptSkipApiKeyCommand { get; }

    internal async Task CheckApiKeyPromptAsync()
    {
        // Load the key from storage first so we don't race against the App startup fire-and-forget
        await AzureOpenAIService.LoadApiKeyFromStorageAsync();
        await Task.Delay(800);
        if (!AzureOpenAIService.HasApiKey && !_hasSkippedApiKeyThisSession)
            IsApiKeyPromptVisible = true;
    }

    public MainViewModel()
    {
        PromptSaveApiKeyCommand = new Command(async () =>
        {
            if (!string.IsNullOrWhiteSpace(_promptApiKeyEntry))
                await AzureOpenAIService.SaveApiKeyToStorageAsync(_promptApiKeyEntry);
            PromptApiKeyEntry = string.Empty;
            IsApiKeyPromptVisible = false;
            _hasSkippedApiKeyThisSession = true;
        });

        PromptSkipApiKeyCommand = new Command(() =>
        {
            IsApiKeyPromptVisible = false;
            _hasSkippedApiKeyThisSession = true;
        });

        OilChangeCommand = new Command(async () => await Shell.Current.DisplayAlertAsync("Oil Change", "Next oil change scheduled on Jun 27.\nEstimated cost: ₹1,200.", "OK"));
        LastFuelCommand = new Command(async () => await Shell.Current.DisplayAlertAsync("Last Fuel Log", "Last refuel: 3 days ago.\nAmount: 30 L · ₹2,850.", "OK"));
        AddActionCommand = new Command(async () => await Shell.Current.DisplayAlertAsync("Add Action", "Log a new action such as fuel or service.", "OK"));

        ShowAddVehicleCommand = new Command(() =>
        {
            if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                IsAddVehicleDrawerVisible = true;
            else
                IsAddVehicleBottomSheetVisible = true;
        });

        ShowAllRecordsCommand = new Command(() => IsAllRecordsPopupVisible = true);
        CloseAllRecordsCommand = new Command(() => IsAllRecordsPopupVisible = false);
        ShowRecentActivityAllCommand = new Command(() => ShowPopupTable("Recent Activity", AllRecentActivityRows));
        ShowFuelExpenseAllCommand = new Command(() => ShowPopupTable("Fuel Expense", AllFuelExpenseRows));
        ShowServiceExpenseAllCommand = new Command(() => ShowPopupTable("Service Expense", AllServiceExpenseRows));

        AddVehicleViewModel.OnAddVehicleRequested += HandleAddVehicle;
        AddVehicleViewModel.OnCloseRequested += HandleCloseAddVehicle;

        // Re-run AI features when the API key is saved or cleared in Settings
        AzureOpenAIService.ApiKeyChanged += () =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var vid = _selectedVehicle?.Id ?? 0;
                _ = RefreshAiInsightsAsync(vid);
                _ = RefreshAiHealthSummaryAsync(vid);
            });

        // ✅ Subscribe to demo mode changes
        VehicleDataService.Instance.ModeChanged += OnDataModeChanged;

        InitializeData();
    }

    /// <summary>
    /// Called when data mode changes (Demo <-> Real)
    /// </summary>
    private void OnDataModeChanged(VehicleDataService.DataMode newMode)
    {
        OnPropertyChanged(nameof(IsDemoMode));
        OnPropertyChanged(nameof(DemoBadgeVisibility));
        OnPropertyChanged(nameof(DemoBadgeText));
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] Data mode changed to: {newMode}");
    }

    /// <summary>
    /// Returns true if app is currently in Demo mode (sample data)
    /// </summary>
    public bool IsDemoMode => VehicleDataService.Instance.IsDemoMode;

    /// <summary>
    /// Visibility for demo badge in header
    /// </summary>
    public bool DemoBadgeVisibility => IsDemoMode;

    /// <summary>
    /// Text for demo badge
    /// </summary>
    public string DemoBadgeText => IsDemoMode ? "DEMO MODE" : "";

    private void ShowPopupTable(string title, IEnumerable<DashboardTableRow> rows)
    {
        CurrentPopupTitle = title;
        CurrentPopupRows.Clear();
        foreach (var row in rows)
            CurrentPopupRows.Add(row);
        OnPropertyChanged(nameof(AllRecordsPopupHeight));
        IsAllRecordsPopupVisible = true;
    }

    private void HandleAddVehicle()
    {
        var newVehicle = new Vehicle
        {
            Id = 0, // let VehicleDataService auto-assign to avoid collisions with demo IDs
            VehicleType = GetVehicleTypeIndex(AddVehicleViewModel.SelectedVehicleType),
            Make = AddVehicleViewModel.Make,
            Model = AddVehicleViewModel.Model,
            Variant = AddVehicleViewModel.Variant,
            OdometerReading = AddVehicleViewModel.OdaMeterReading,
            CreatedDate = DateTime.Now,
            IsActive = true
        };

        VehicleDataService.Instance.AddVehicle(newVehicle);
        Vehicles.Add(newVehicle);

        // Set as selected vehicle (will show in dashboard)
        SelectedVehicle = newVehicle;
        VehicleDataService.Instance.SelectedVehicle = newVehicle;

        // Notify all vehicle-dependent display properties
        OnPropertyChanged(nameof(HasVehicles));
        OnPropertyChanged(nameof(IsNoVehiclesPlaceholderVisible));
        OnPropertyChanged(nameof(SelectedVehicleLabel));
        OnPropertyChanged(nameof(VehicleName));
        OnPropertyChanged(nameof(VehicleModel));
        OnPropertyChanged(nameof(Registration));
        OnPropertyChanged(nameof(VehicleOdometer));
        OnPropertyChanged(nameof(LastServiceText));
        OnPropertyChanged(nameof(ConditionText));

        // Close the UI
        HandleCloseAddVehicle();
    }

    private void HandleCloseAddVehicle()
    {
        IsAddVehicleDrawerVisible = false;
        IsAddVehicleBottomSheetVisible = false;
        AddVehicleViewModel.Make = string.Empty;
        AddVehicleViewModel.Model = string.Empty;
        AddVehicleViewModel.Variant = string.Empty;
        AddVehicleViewModel.OdaMeterReading = string.Empty;
        AddVehicleViewModel.SelectedVehicleType = string.Empty;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public NavSection CurrentSection
    {
        get => _currentSection;
        set
        {
            if (SetProperty(ref _currentSection, value))
            {
                OnPropertyChanged(nameof(IsDashboardVisible));
                OnPropertyChanged(nameof(IsVehicleCenterVisible));
                OnPropertyChanged(nameof(IsAIAssistVisible));
                OnPropertyChanged(nameof(IsServiceCenterVisible));
                OnPropertyChanged(nameof(IsSettingsVisible));
                OnPropertyChanged(nameof(IsDashNavActive));
                OnPropertyChanged(nameof(IsVehicleNavActive));
                OnPropertyChanged(nameof(IsAINavActive));
                OnPropertyChanged(nameof(IsServiceNavActive));
                OnPropertyChanged(nameof(IsSettingsNavActive));
                OnPropertyChanged(nameof(PageTitle));
                OnPropertyChanged(nameof(IsAddVehicleButtonVisible));
                OnPropertyChanged(nameof(IsMobileAddVehicleButtonVisible));
                OnPropertyChanged(nameof(IsDesktopHeaderVisible));
                OnPropertyChanged(nameof(IsMobileHeaderVisible));
            }
        }
    }

    public bool IsDashboardVisible => CurrentSection == NavSection.Dashboard;
    public bool IsVehicleCenterVisible => CurrentSection == NavSection.VehicleCenter;
    public bool IsAIAssistVisible => CurrentSection == NavSection.AIAssist;
    public bool IsServiceCenterVisible => CurrentSection == NavSection.ServiceCenter;
    public bool IsSettingsVisible => CurrentSection == NavSection.Settings;
    public bool IsDashNavActive => CurrentSection == NavSection.Dashboard;
    public bool IsVehicleNavActive => CurrentSection == NavSection.VehicleCenter;
    public bool IsAINavActive => CurrentSection == NavSection.AIAssist;
    public bool IsServiceNavActive => CurrentSection == NavSection.ServiceCenter;
    public bool IsSettingsNavActive => CurrentSection == NavSection.Settings;
    // Show header for all sections including AI Assist (with vehicle selector)
    public bool IsDesktopHeaderVisible =>
        (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst);
    public bool IsMobileHeaderVisible =>
        (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS);
    public bool IsMobileAddVehicleButtonVisible => IsMobileHeaderVisible && IsDashboardVisible;

    // ── Vehicle selector ──────────────────────────────────────────────────────

    public ObservableCollection<Vehicle> Vehicles { get; } = new();

    private Vehicle _selectedVehicle;
    public Vehicle SelectedVehicle
    {
        get => _selectedVehicle ?? new Vehicle
        {
            Make = string.Empty,
            Model = string.Empty,
            Variant = string.Empty,
            OdometerReading = string.Empty
        };
        set
        {
            if (SetProperty(ref _selectedVehicle, value))
            {
                VehicleDataService.Instance.SelectedVehicle = value;
                OnPropertyChanged(nameof(SelectedVehicleLabel));
                OnPropertyChanged(nameof(VehicleName));
                OnPropertyChanged(nameof(VehicleModel));
                OnPropertyChanged(nameof(VehicleImageSource));
                OnPropertyChanged(nameof(Registration));
                OnPropertyChanged(nameof(VehicleOdometer));
                OnPropertyChanged(nameof(LastServiceText));
                OnPropertyChanged(nameof(ConditionText));
                RebuildDashboard();
            }
        }
    }

    // ── Header & identity ─────────────────────────────────────────────────────

    public string UserName => Preferences.Default.Get("UserDisplayName", "Vehicle Owner");
    public string UserInitial => UserName.Length > 0 ? UserName[0].ToString().ToUpper() : "V";
    public string PageTitle => CurrentSection switch
    {
        NavSection.Dashboard => "Dashboard",
        NavSection.VehicleCenter => "Vehicle Center",
        NavSection.AIAssist => "AI Assist",
        NavSection.ServiceCenter => "Service Centers",
        NavSection.Settings => "Settings",
        _ => "Vehicle Command Center"
    };
    public bool IsAddVehicleButtonVisible => CurrentSection == NavSection.Dashboard;
    public bool HasVehicles => Vehicles.Count > 0;
    public bool IsNoVehiclesPlaceholderVisible => !HasVehicles;

    // ── Vehicle hero card (dynamic, binds to SelectedVehicle) ─────────────────

    public string SelectedVehicleLabel => HasVehicles ? (SelectedVehicle?.MakeModel ?? "No vehicle") : "No vehicle";
    public string VehicleName => HasVehicles ? (SelectedVehicle?.DisplayName ?? "—") : "No vehicle added";
    public string VehicleModel => HasVehicles ? (string.IsNullOrEmpty(SelectedVehicle?.Variant) ? "—" : SelectedVehicle!.Variant) : "Add your first vehicle";
    public string VehicleImageSource => _selectedVehicle?.VehicleType == 1 ? "bike_dashboard.jpg" : "car_dashboard.jpg";
    public string Registration => HasVehicles ? (string.IsNullOrEmpty(SelectedVehicle?.RegistrationNumber) ? "Not set" : SelectedVehicle!.RegistrationNumber) : "—";
    public string VehicleOdometer => HasVehicles
        ? (string.IsNullOrEmpty(SelectedVehicle?.OdometerReading) ? "Odometer not set" : $"{SelectedVehicle!.OdometerReading} km")
        : "—";
    public string LastServiceText
    {
        get
        {
            if (_selectedVehicle == null) return "No service history";
            var records = VehicleDataService.Instance.GetServiceRecords(_selectedVehicle.Id);
            if (!records.Any()) return "No service records";
            var latest = records.OrderByDescending(r => r.ServiceDate).First();
            var diff = DateTime.Today - latest.ServiceDate;
            if (diff.TotalDays < 1) return "Serviced today";
            if (diff.TotalDays < 7) return $"Last service: {(int)diff.TotalDays} days ago";
            if (diff.TotalDays < 31) return $"Last service: {(int)(diff.TotalDays / 7)} weeks ago";
            return $"Last service: {(int)(diff.TotalDays / 30)} months ago";
        }
    }
    public string ConditionText => HasVehicles ? (_healthLabel == "EXCELLENT" ? "● EXCELLENT CONDITION" : $"● {_healthLabel}") : "—";

    public string NextServiceDueText
    {
        get
        {
            if (_selectedVehicle == null) return "—";
            var records = VehicleDataService.Instance.GetServiceRecords(_selectedVehicle.Id);
            if (!records.Any()) return "Not scheduled";
            var latest = records.OrderByDescending(r => r.ServiceDate).First();
            // Use vehicle's service interval (default 90 days / ~5000 km)
            var intervalDays = _selectedVehicle.ServiceInterval switch
            {
                "3 months / 3,000 km" => 90,
                "6 months / 5,000 km" => 180,
                "1 year / 10,000 km" => 365,
                _ => 90
            };
            var dueDate = latest.ServiceDate.AddDays(intervalDays);
            var daysLeft = (dueDate - DateTime.Today).TotalDays;
            if (daysLeft < 0) return "Overdue";
            if (daysLeft == 0) return "Due today";
            if (daysLeft <= 30) return $"In {(int)daysLeft} days";
            return $"In ~{(int)(daysLeft / 30)} months";
        }
    }

    public string MonthlySpendText => _currentTotal > 0 ? $"₹{_currentTotal:N0}" : "No spend";

    // ── Overall Health (computed from real data) ──────────────────────────────

    private double _healthScore;
    private string _healthLabel = "—";
    private string _healthDescription = "Add a vehicle and log service records to see health metrics.";

    public double HealthScore => _healthScore;
    public string HealthLabel => _healthLabel;
    public string HealthTrend => HealthItems.Count > 0 ? $"{HealthItems.Count} metrics tracked" : "No data yet";
    public string HealthDescription => _healthDescription;
    public string NextCheckupText => _selectedVehicle != null ? $"Next service due in ~90 days from last service" : "Add vehicle to track";

    // ── Expense Intelligence ───────────────────────────────────────────────────

    private double _currentMonthFuel;
    private double _currentMonthService;
    private double _allTimeFuel;
    private double _allTimeService;
    private int _trackedMonths;
    private double _allTimeCostPerKm;

    private double _currentTotal => _currentMonthFuel + _currentMonthService;
    private double _allTimeTotal => _allTimeFuel + _allTimeService;
    private double _avgMonthly => _trackedMonths > 0 ? _allTimeTotal / _trackedMonths : 0;

    public string CurrentMonthName => DateTime.Today.ToString("MMMM yyyy").ToUpper();
    public string CurrentMonthTotal => _currentTotal > 0 ? $"₹{_currentTotal:N0}" : "₹0";
    public string CurrentMonthFuelLabel => _currentMonthFuel > 0 ? $"₹{_currentMonthFuel:N0}" : "₹0";
    public string CurrentMonthServiceLabel => _currentMonthService > 0 ? $"₹{_currentMonthService:N0}" : "₹0";
    public double CurrentFuelProgress => _currentTotal > 0 ? Math.Round(_currentMonthFuel / _currentTotal * 100) : 0;
    public double CurrentServiceProgress => _currentTotal > 0 ? Math.Round(_currentMonthService / _currentTotal * 100) : 0;
    public string CurrentFuelPercent => $"{CurrentFuelProgress:0}%";
    public string CurrentServicePercent => $"{CurrentServiceProgress:0}%";
    public string AvgMonthlyLabel => _avgMonthly > 0 ? $"₹{_avgMonthly:N0}/mo" : "No data";
    public string CostPerKmLabel => _allTimeCostPerKm > 0 ? $"₹{_allTimeCostPerKm:N1}/km" : "—";
    public string VsAvgLabel
    {
        get
        {
            if (_avgMonthly <= 0 || _trackedMonths <= 1) return "First month";
            var diff = (_currentTotal - _avgMonthly) / _avgMonthly * 100;
            return diff > 5 ? $"↑ {Math.Abs(diff):0}% vs avg"
                 : diff < -5 ? $"↓ {Math.Abs(diff):0}% vs avg"
                 : "On track";
        }
    }
    public Color VsAvgColor => VsAvgLabel.StartsWith("↑") ? Color.FromArgb("#EF4444")
                             : VsAvgLabel.StartsWith("↓") ? Color.FromArgb("#22C55E")
                             : Color.FromArgb("#3B82F6");

    // Legacy aliases so other bindings don't break
    public string ThisMonthExpense => CurrentMonthTotal;
    public string ExpenseTrend => AvgMonthlyLabel;
    public string MonthlyTotal => CurrentMonthTotal;
    public string MonthlyTrend => AvgMonthlyLabel;
    public double FuelProgress => CurrentFuelProgress;
    public double ServiceProgress => CurrentServiceProgress;
    public string FuelPercentLabel => CurrentFuelPercent;
    public string ServicePercentLabel => CurrentServicePercent;
    public string FuelAmountLabel => CurrentMonthFuelLabel;
    public string ServiceAmountLabel => CurrentMonthServiceLabel;
    // Legacy aliases kept for other bindings
    public string TotalSpendLabel => CurrentMonthTotal;

    // ── Analytics chart data ──────────────────────────────────────────────────

    public ObservableCollection<ExpenseDataPoint> FuelExpenses { get; } = new();
    public ObservableCollection<ExpenseDataPoint> MaintenanceExpenses { get; } = new();

    // ── Active Intelligence ───────────────────────────────────────────────────

    public ObservableCollection<AiInsightItem> AiInsights { get; } = new();
    public bool HasAiInsights => AiInsights.Count > 0;

    private bool _isAiInsightsLoading;
    public bool IsAiInsightsLoading
    {
        get => _isAiInsightsLoading;
        private set { _isAiInsightsLoading = value; OnPropertyChanged(); }
    }

    // ── Quick Actions ─────────────────────────────────────────────────────────

    public ObservableCollection<QuickActionItem> QuickActions { get; } = new();

    // ── Activities & Health ───────────────────────────────────────────────────

    public ObservableCollection<ActivityItem> RecentActivities { get; } = new();
    public ObservableCollection<DashboardTableRow> RecentActivityTableRows { get; } = new();
    public bool HasRecentActivity => RecentActivityTableRows.Count > 0;
    public bool NoRecentActivity => !HasRecentActivity;
    public ObservableCollection<DashboardTableRow> AllRecentActivityRows { get; } = new();
    public ObservableCollection<DashboardTableRow> FuelExpenseRows { get; } = new();
    public ObservableCollection<DashboardTableRow> AllFuelExpenseRows { get; } = new();
    public ObservableCollection<DashboardTableRow> ServiceExpenseRows { get; } = new();
    public ObservableCollection<DashboardTableRow> AllServiceExpenseRows { get; } = new();
    public ObservableCollection<DashboardTableRow> CurrentPopupRows { get; } = new();
    public double AllRecordsPopupHeight => Math.Clamp(CurrentPopupRows.Count * 42 + 140, 280, 640);
    public ObservableCollection<HealthItem> HealthItems { get; } = new();
    public ObservableCollection<HealthIssueItem> HealthIssueItems { get; } = new();

    private bool _isAllRecordsPopupVisible;
    public bool IsAllRecordsPopupVisible
    {
        get => _isAllRecordsPopupVisible;
        set => SetProperty(ref _isAllRecordsPopupVisible, value);
    }

    private string _currentPopupTitle = "All records";
    public string CurrentPopupTitle
    {
        get => _currentPopupTitle;
        set => SetProperty(ref _currentPopupTitle, value);
    }

    public ICommand ShowAllRecordsCommand { get; }
    public ICommand CloseAllRecordsCommand { get; }
    public ICommand ShowRecentActivityAllCommand { get; }
    public ICommand ShowFuelExpenseAllCommand { get; }
    public ICommand ShowServiceExpenseAllCommand { get; }

    private string _aiHealthSummary = string.Empty;
    private bool _isAiHealthLoading;
    private string _scoreBoostHint = string.Empty;

    public string AiHealthSummary => _aiHealthSummary;
    public bool IsAiHealthLoading => _isAiHealthLoading;
    public string ScoreBoostHint => _scoreBoostHint;
    public bool HasScoreBoostHint => !string.IsNullOrWhiteSpace(_scoreBoostHint);

    // ── Quick Action Commands ─────────────────────────────────────────────────

    public ICommand OilChangeCommand { get; }
    public ICommand LastFuelCommand { get; }
    public ICommand AddActionCommand { get; }
    // ── Add Vehicle ───────────────────────────────────────────────────────────

    private bool _isAddVehicleDrawerVisible = false;
    public bool IsAddVehicleDrawerVisible
    {
        get => _isAddVehicleDrawerVisible;
        set => SetProperty(ref _isAddVehicleDrawerVisible, value);
    }

    private bool _isAddVehicleBottomSheetVisible = false;
    public bool IsAddVehicleBottomSheetVisible
    {
        get => _isAddVehicleBottomSheetVisible;
        set => SetProperty(ref _isAddVehicleBottomSheetVisible, value);
    }

    public AddVehicleViewModel AddVehicleViewModel { get; } = new();

    public ICommand ShowAddVehicleCommand { get; }
    // ── Init ──────────────────────────────────────────────────────────────────

    private int GetVehicleTypeIndex(string vehicleType)
    {
        return vehicleType switch
        {
            "Car" => 0,
            "Motorcycle" => 1,
            "Commercial Vehicle" => 2,
            "Electric Vehicle" => 3,
            _ => 0
        };
    }
    private void InitializeData()
    {
        foreach (var v in VehicleDataService.Instance.Vehicles)
            if (!Vehicles.Contains(v)) Vehicles.Add(v);

        Vehicles.CollectionChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(HasVehicles));
            OnPropertyChanged(nameof(IsNoVehiclesPlaceholderVisible));
        };

        // Sync vehicle selection from VehicleCenterViewModel changes
        VehicleDataService.Instance.SelectedVehicleChanged += v =>
        {
            if (v != null && v != _selectedVehicle)
            {
                _selectedVehicle = v;
                OnPropertyChanged(nameof(SelectedVehicle));
                OnPropertyChanged(nameof(SelectedVehicleLabel));
                OnPropertyChanged(nameof(VehicleName));
                OnPropertyChanged(nameof(VehicleModel));
                OnPropertyChanged(nameof(VehicleImageSource));
                OnPropertyChanged(nameof(Registration));
                OnPropertyChanged(nameof(VehicleOdometer));
                OnPropertyChanged(nameof(LastServiceText));
                OnPropertyChanged(nameof(ConditionText));
                RebuildDashboard();
            }
        };

        // Rebuild dashboard when any per-vehicle data changes
        VehicleDataService.Instance.DataChanged += vid =>
        {
            if (_selectedVehicle?.Id != vid) return;

            var updatedVehicle = Vehicles.FirstOrDefault(v => v.Id == vid);
            if (updatedVehicle != null)
            {
                _selectedVehicle = null;
                OnPropertyChanged(nameof(SelectedVehicle));
                _selectedVehicle = updatedVehicle;
                OnPropertyChanged(nameof(SelectedVehicle));
            }

            RebuildDashboard();
        };

        if (VehicleDataService.Instance.SelectedVehicle != null)
            SelectedVehicle = VehicleDataService.Instance.SelectedVehicle;
        else if (Vehicles.Count > 0)
            SelectedVehicle = Vehicles[0];
        else
            RebuildDashboard(); // no vehicle selected — still populate empty-state dashboard
    }

    private void RebuildDashboard()
    {
        var vid = _selectedVehicle?.Id ?? 0;

        // Expense Intelligence — current month vs all-time for selected vehicle
        var now = DateTime.Today;
        var allFuels = VehicleDataService.Instance.GetFuelEntries(vid).ToList();
        var allServices = VehicleDataService.Instance.GetServiceRecords(vid).ToList();

        _currentMonthFuel = allFuels.Where(f => f.FuelDate.Year == now.Year && f.FuelDate.Month == now.Month).Sum(f => f.TotalCost);
        _currentMonthService = allServices.Where(s => s.ServiceDate.Year == now.Year && s.ServiceDate.Month == now.Month).Sum(s => ParseCurrency(s.Amount));
        _allTimeFuel = allFuels.Sum(f => f.TotalCost);
        _allTimeService = allServices.Sum(s => ParseCurrency(s.Amount));

        var allDates = allFuels.Select(f => f.FuelDate).Concat(allServices.Select(s => s.ServiceDate)).ToList();
        _trackedMonths = allDates.Count > 0
            ? Math.Max(1, (now.Year - allDates.Min().Year) * 12 + now.Month - allDates.Min().Month + 1)
            : 0;

        if (double.TryParse(_selectedVehicle?.OdometerReading, out var odo) && odo > 0 && _allTimeTotal > 0)
            _allTimeCostPerKm = Math.Round(_allTimeTotal / odo, 1);
        else
            _allTimeCostPerKm = 0;

        OnPropertyChanged(nameof(CurrentMonthName));
        OnPropertyChanged(nameof(CurrentMonthTotal));
        OnPropertyChanged(nameof(CurrentMonthFuelLabel));
        OnPropertyChanged(nameof(CurrentMonthServiceLabel));
        OnPropertyChanged(nameof(CurrentFuelProgress));
        OnPropertyChanged(nameof(CurrentServiceProgress));
        OnPropertyChanged(nameof(CurrentFuelPercent));
        OnPropertyChanged(nameof(CurrentServicePercent));
        OnPropertyChanged(nameof(AvgMonthlyLabel));
        OnPropertyChanged(nameof(CostPerKmLabel));
        OnPropertyChanged(nameof(VsAvgLabel));
        OnPropertyChanged(nameof(VsAvgColor));
        OnPropertyChanged(nameof(TotalSpendLabel));
        OnPropertyChanged(nameof(ThisMonthExpense));
        OnPropertyChanged(nameof(ExpenseTrend));
        OnPropertyChanged(nameof(FuelProgress));
        OnPropertyChanged(nameof(ServiceProgress));
        OnPropertyChanged(nameof(FuelPercentLabel));
        OnPropertyChanged(nameof(ServicePercentLabel));
        OnPropertyChanged(nameof(FuelAmountLabel));
        OnPropertyChanged(nameof(ServiceAmountLabel));
        OnPropertyChanged(nameof(LastServiceText));

        // Refresh vehicle identity display properties in case vehicle details were edited externally
        OnPropertyChanged(nameof(SelectedVehicle));       // forces SfComboBox to re-read DisplayMemberPath
        OnPropertyChanged(nameof(SelectedVehicleLabel));
        OnPropertyChanged(nameof(VehicleName));
        OnPropertyChanged(nameof(VehicleModel));
        OnPropertyChanged(nameof(VehicleImageSource));
        OnPropertyChanged(nameof(Registration));
        OnPropertyChanged(nameof(VehicleOdometer));
        OnPropertyChanged(nameof(ConditionText));
        OnPropertyChanged(nameof(NextServiceDueText));
        OnPropertyChanged(nameof(MonthlySpendText));

        _ = RefreshAiInsightsAsync(vid);
        RebuildRecentActivities(vid);
        RebuildHealthScore(vid);
    }

    private async Task RefreshAiInsightsAsync(int vehicleId)
    {
        AiInsights.Clear();
        IsAiInsightsLoading = true;
        OnPropertyChanged(nameof(HasAiInsights));

        // Ensure key is loaded from storage before checking
        if (!AzureOpenAIService.HasApiKey)
            await AzureOpenAIService.LoadApiKeyFromStorageAsync();

        var services = VehicleDataService.Instance.GetServiceRecords(vehicleId)
            .OrderByDescending(s => s.ServiceDate)
            .ToList();
        var fuels = VehicleDataService.Instance.GetFuelEntries(vehicleId)
            .OrderByDescending(f => f.FuelDate)
            .ToList();
        var reminders = VehicleDataService.Instance.GetReminders(vehicleId).ToList();

        if (!services.Any() && !fuels.Any() && !reminders.Any())
        {
            AiInsights.Add(new AiInsightItem
            {
                AccentColor = Color.FromArgb("#60A5FA"),
                Title = "Add your first service record",
                Subtitle = "Service history helps track maintenance schedules and vehicle health.",
                ActionLabel = "Add",
                ActionBgColor = Color.FromArgb("#1E3A5F"),
                ActionTextColor = Color.FromArgb("#93C5FD")
            });
            AiInsights.Add(new AiInsightItem
            {
                AccentColor = Color.FromArgb("#F59E0B"),
                Title = "Log a fuel entry",
                Subtitle = "Track fuel consumption to monitor mileage efficiency and spending patterns.",
                ActionLabel = "Track",
                ActionBgColor = Color.FromArgb("#451A03"),
                ActionTextColor = Color.FromArgb("#FCD34D")
            });
            IsAiInsightsLoading = false;
            OnPropertyChanged(nameof(HasAiInsights));
            return;
        }

        if (!AzureOpenAIService.HasApiKey)
        {
            // No API key — surface the limitation
            AiInsights.Add(new AiInsightItem
            {
                AccentColor = Color.FromArgb("#94A3B8"),
                Title = "API key not configured",
                Subtitle = "Add your Azure OpenAI key in Settings to get AI-powered maintenance insights.",
                ActionLabel = "Settings",
                ActionBgColor = Color.FromArgb("#1E293B"),
                ActionTextColor = Color.FromArgb("#94A3B8")
            });
            IsAiInsightsLoading = false;
            OnPropertyChanged(nameof(HasAiInsights));
            return;
        }

        // ── AI-powered insights ──────────────────────────────────────────────────
        var vehicle = _selectedVehicle;
        var lastService = services.FirstOrDefault();
        var lastFuel = fuels.FirstOrDefault();
        var daysSinceService = lastService != null ? (int)(DateTime.Today - lastService.ServiceDate).TotalDays : -1;
        var daysSinceFuel = lastFuel != null ? (int)(DateTime.Today - lastFuel.FuelDate).TotalDays : -1;
        var avgFuelSpend = fuels.Any() ? fuels.Average(f => f.TotalCost) : 0;
        var upcomingReminder = reminders
            .Where(r => r.DueDate >= DateTime.Today && r.DueDate <= DateTime.Today.AddDays(60))
            .OrderBy(r => r.DueDate).FirstOrDefault();

        var prompt = $"""
 Vehicle: {vehicle?.Make} {vehicle?.Model} {vehicle?.Year}
 Service records: {services.Count}, last service {daysSinceService} days ago ({lastService?.ServiceType ?? "N/A"})
 Fuel entries: {fuels.Count}, last refuel {daysSinceFuel} days ago, avg ₹{avgFuelSpend:N0}/fill
 Upcoming reminder: {(upcomingReminder != null ? $"{upcomingReminder.Title} due in {(upcomingReminder.DueDate - DateTime.Today).Days} days" : "None")}

 Generate 3 concise, actionable vehicle maintenance insights from the data above.
 For each insight, respond in exactly this format (no extra text):
 TITLE: <alert title, max 7 words>
 SUBTITLE: <one realistic sentence with specific numbers, max 20 words>
 SEVERITY: low|medium|high

 Separate each insight with a blank line.
 """;

        var aiResponse = await new AzureOpenAIService().GetResultsFromAI(
            prompt,
            "You are a concise vehicle maintenance advisor. Generate exactly 3 insights using the exact format specified. Use only the supplied vehicle data.");

        var parsed = ParseAiInsightItems(aiResponse);
        if (parsed.Count > 0)
        {
            foreach (var item in parsed)
                AiInsights.Add(item);
        }
        else
        {
            // AI returned nothing useful — fall back to rule-based items
            BuildRuleBasedInsights(services, fuels, reminders);
        }

        IsAiInsightsLoading = false;
        OnPropertyChanged(nameof(HasAiInsights));
    }

    private static List<AiInsightItem> ParseAiInsightItems(string aiResponse)
    {
        var result = new List<AiInsightItem>();
        if (string.IsNullOrWhiteSpace(aiResponse)) return result;

        var blocks = aiResponse.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string title = string.Empty, subtitle = string.Empty, severity = "low";
            foreach (var line in lines)
            {
                if (line.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
                    title = line[6..].Trim();
                else if (line.StartsWith("SUBTITLE:", StringComparison.OrdinalIgnoreCase))
                    subtitle = line[9..].Trim();
                else if (line.StartsWith("SEVERITY:", StringComparison.OrdinalIgnoreCase))
                    severity = line[9..].Trim().ToLowerInvariant();
            }
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(subtitle)) continue;

            var (accent, actionBg, actionText) = severity switch
            {
                "high"   => (Color.FromArgb("#EF4444"), Color.FromArgb("#3F0E0E"), Color.FromArgb("#FCA5A5")),
                "medium" => (Color.FromArgb("#F59E0B"), Color.FromArgb("#451A03"), Color.FromArgb("#FCD34D")),
                _        => (Color.FromArgb("#3B82F6"), Color.FromArgb("#1E3A5F"), Color.FromArgb("#93C5FD")),
            };
            result.Add(new AiInsightItem
            {
                AccentColor     = accent,
                Title           = title,
                Subtitle        = subtitle,
                ActionLabel     = severity == "high" ? "Review" : severity == "medium" ? "Check" : "OK",
                ActionBgColor   = actionBg,
                ActionTextColor = actionText
            });
        }
        return result;
    }

    private void BuildRuleBasedInsights(List<ServiceRecord> services, List<FuelEntry> fuels, List<ScheduleReminder> reminders)
    {
        if (!services.Any() && !fuels.Any() && !reminders.Any()) return;

        if (services.Any()) AiInsights.Add(BuildServiceInsight(services));
        if (fuels.Any())    AiInsights.Add(BuildFuelInsight(fuels));

        var typeInsight = BuildMaintenanceTypeInsight(services);
        if (typeInsight != null) AiInsights.Add(typeInsight);

        var reminderInsight = BuildReminderInsight(reminders);
        if (reminderInsight != null) AiInsights.Add(reminderInsight);
    }

    private AiInsightItem? BuildMaintenanceTypeInsight(List<ServiceRecord> services)
    {
        var oilChangeServices = services.Where(s => s.ServiceType?.Contains("Oil", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
        var tireServices      = services.Where(s => s.ServiceType?.Contains("Tire", StringComparison.OrdinalIgnoreCase) ?? false).ToList();
        var brakeServices     = services.Where(s => s.ServiceType?.Contains("Brake", StringComparison.OrdinalIgnoreCase) ?? false).ToList();

        if (oilChangeServices.Any())
        {
            var daysSince = (DateTime.Today - oilChangeServices.Max(s => s.ServiceDate)).TotalDays;
            if (daysSince > 180)
                return new AiInsightItem
                {
                    AccentColor = Color.FromArgb("#EF4444"),
                    Title = "Oil change may be due",
                    Subtitle = $"Last oil change was {Math.Round(daysSince / 30, 0)} months ago. Check your odometer and schedule if needed.",
                    ActionLabel = "Schedule",
                    ActionBgColor = Color.FromArgb("#3F0E0E"),
                    ActionTextColor = Color.FromArgb("#FCA5A5")
                };
        }

        if (tireServices.Any())
        {
            var daysSince = (DateTime.Today - tireServices.Max(s => s.ServiceDate)).TotalDays;
            if (daysSince > 180)
                return new AiInsightItem
                {
                    AccentColor = Color.FromArgb("#F59E0B"),
                    Title = "Tire rotation recommended",
                    Subtitle = $"Tires should be rotated every 6 months for even wear. Last rotation was {Math.Round(daysSince / 30, 0)} months ago.",
                    ActionLabel = "Book",
                    ActionBgColor = Color.FromArgb("#451A03"),
                    ActionTextColor = Color.FromArgb("#FCD34D")
                };
        }

        if (brakeServices.Any())
        {
            var daysSince = (DateTime.Today - brakeServices.Max(s => s.ServiceDate)).TotalDays;
            if (daysSince > 365)
                return new AiInsightItem
                {
                    AccentColor = Color.FromArgb("#EC4899"),
                    Title = "Brake inspection overdue",
                    Subtitle = $"Brake pads and fluid should be checked annually. Last inspection was {Math.Round(daysSince / 30, 0)} months ago.",
                    ActionLabel = "Inspect",
                    ActionBgColor = Color.FromArgb("#501425"),
                    ActionTextColor = Color.FromArgb("#F472B6")
                };
        }

        return null;
    }

    private static AiInsightItem? BuildReminderInsight(List<ScheduleReminder> reminders)
    {
        var upcoming = reminders
            .Where(r => r.DueDate >= DateTime.Today && r.DueDate <= DateTime.Today.AddDays(30))
            .OrderBy(r => r.DueDate)
            .FirstOrDefault();

        if (upcoming == null) return null;

        var daysLeft = (upcoming.DueDate - DateTime.Today).Days;
        var (accent, actionBg) = upcoming.Priority switch
        {
            "High"   => (Color.FromArgb("#EF4444"), Color.FromArgb("#3F0E0E")),
            "Medium" => (Color.FromArgb("#F59E0B"), Color.FromArgb("#451A03")),
            _        => (Color.FromArgb("#3B82F6"), Color.FromArgb("#1E3A5F")),
        };
        var actionText = upcoming.Priority == "High" ? Color.FromArgb("#FCA5A5")
                       : upcoming.Priority == "Medium" ? Color.FromArgb("#FCD34D")
                       : Color.FromArgb("#93C5FD");

        return new AiInsightItem
        {
            AccentColor     = accent,
            Title           = $"{upcoming.ReminderType} expiring soon",
            Subtitle        = $"{upcoming.Title} is due in {daysLeft} days. Renew or schedule before expiry.",
            ActionLabel     = daysLeft <= 7 ? "Renew Now" : "Review",
            ActionBgColor   = actionBg,
            ActionTextColor = actionText
        };
    }

    private static AiInsightItem BuildServiceInsight(List<ServiceRecord> services)
    {
        var latest = services.OrderByDescending(s => s.ServiceDate).First();
        var daysSince = (DateTime.Today - latest.ServiceDate).TotalDays;

        if (daysSince > 180)
        {
            return new AiInsightItem
            {
                AccentColor = Color.FromArgb("#EF4444"),
                Title = "Service is overdue",
                Subtitle = $"Last service was {Math.Round(daysSince / 30, 0)} months ago. Book a maintenance check soon.",
                ActionLabel = "Review",
                ActionBgColor = Color.FromArgb("#3F0E0E"),
                ActionTextColor = Color.FromArgb("#FCA5A5")
            };
        }

        if (daysSince > 90)
        {
            return new AiInsightItem
            {
                AccentColor = Color.FromArgb("#F59E0B"),
                Title = "Service due soon",
                Subtitle = $"Your last service was {Math.Round(daysSince / 30, 1)} months ago and should be scheduled soon.",
                ActionLabel = "Book",
                ActionBgColor = Color.FromArgb("#451A03"),
                ActionTextColor = Color.FromArgb("#FCD34D")
            };
        }

        return new AiInsightItem
        {
            AccentColor = Color.FromArgb("#22C55E"),
            Title = "Service is healthy",
            Subtitle = $"The latest service was {Math.Round(daysSince / 30, 1)} months ago and is still within the recommended range.",
            ActionLabel = "OK",
            ActionBgColor = Color.FromArgb("#052E16"),
            ActionTextColor = Color.FromArgb("#86EFAC")
        };
    }

    private static AiInsightItem BuildFuelInsight(List<FuelEntry> fuels)
    {
        var latest = fuels.OrderByDescending(f => f.FuelDate).First();
        var daysSinceFuel = (DateTime.Today - latest.FuelDate).TotalDays;
        var avgFuelSpend = fuels.Average(f => f.TotalCost);

        if (daysSinceFuel > 30)
        {
            return new AiInsightItem
            {
                AccentColor = Color.FromArgb("#F59E0B"),
                Title = "Fuel tracking is stale",
                Subtitle = $"Last refuel was {Math.Round(daysSinceFuel, 0)} days ago. Add a fresh refill entry to monitor efficiency.",
                ActionLabel = "Log",
                ActionBgColor = Color.FromArgb("#451A03"),
                ActionTextColor = Color.FromArgb("#FCD34D")
            };
        }

        return new AiInsightItem
        {
            AccentColor = Color.FromArgb("#3B82F6"),
            Title = "Fuel pattern is healthy",
            Subtitle = $"Recent refill data is active and average spend is about ₹{avgFuelSpend:N0} per fill.",
            ActionLabel = "Track",
            ActionBgColor = Color.FromArgb("#1E3A5F"),
            ActionTextColor = Color.FromArgb("#93C5FD")
        };
    }

    private void RebuildRecentActivities(int vehicleId)
    {
        RecentActivities.Clear();
        RecentActivityTableRows.Clear();
        AllRecentActivityRows.Clear();
        FuelExpenseRows.Clear();
        AllFuelExpenseRows.Clear();
        ServiceExpenseRows.Clear();
        AllServiceExpenseRows.Clear();

        var items = new List<(DateTime key, ActivityItem item)>();
        var activityRows = new List<DashboardTableRow>();

        foreach (var s in VehicleDataService.Instance.GetServiceRecords(vehicleId))
        {
            var row = new DashboardTableRow
            {
                Date = s.ServiceDate.ToString("dd MMM yyyy"),
                Type = "Service",
                Detail = s.ServiceType,
                Status = s.Status
            };

            activityRows.Add(row);
            items.Add((s.ServiceDate, new ActivityItem
            {
                IconGlyph = "\uE86D",
                IconColor = Color.FromArgb("#22C55E"),
                IconBgColor = Color.FromArgb("#0D3321"),
                Title = s.ServiceType,
                Subtitle = string.IsNullOrWhiteSpace(s.Workshop) ? s.Amount : $"{s.Workshop} · {s.Amount}",
                Timestamp = FormatTimestamp(s.ServiceDate),
                TimestampColor = GetTimestampColor(s.ServiceDate)
            }));
        }

        foreach (var f in VehicleDataService.Instance.GetFuelEntries(vehicleId))
        {
            var row = new DashboardTableRow
            {
                Date = f.FuelDate.ToString("dd MMM yyyy"),
                Type = "Fuel",
                Detail = $"{f.FuelType} · {f.Station}",
                Status = $"₹{f.TotalCost:N0}"
            };

            activityRows.Add(row);
            items.Add((f.FuelDate, new ActivityItem
            {
                IconGlyph = "\uE901",
                IconColor = Color.FromArgb("#3B82F6"),
                IconBgColor = Color.FromArgb("#1E3A5F"),
                Title = $"Fuel Refill · {f.FuelType}",
                Subtitle = $"{f.LitresDisplay} · {f.TotalCostDisplay}{(string.IsNullOrEmpty(f.Station) ? "" : $" · {f.Station}")}",
                Timestamp = FormatTimestamp(f.FuelDate),
                TimestampColor = GetTimestampColor(f.FuelDate)
            }));
        }

        foreach (var r in VehicleDataService.Instance.GetReminders(vehicleId))
        {
            var row = new DashboardTableRow
            {
                Date = r.DueDate.ToString("dd MMM yyyy"),
                Type = r.ReminderType,
                Detail = r.Title,
                Status = r.Priority
            };

            activityRows.Add(row);
            items.Add((r.DueDate, new ActivityItem
            {
                IconGlyph = "\uE878",
                IconColor = Color.FromArgb("#A78BFA"),
                IconBgColor = Color.FromArgb("#2E1065"),
                Title = r.Title,
                Subtitle = $"Scheduled · {r.ReminderType} · {r.DueDate:dd MMM yyyy}",
                Timestamp = FormatTimestamp(r.DueDate),
                TimestampColor = GetTimestampColor(r.DueDate)
            }));
        }

        foreach (var row in activityRows.OrderByDescending(r => DateTime.Parse(r.Date == "—" ? DateTime.Today.ToString("dd MMM yyyy") : r.Date)).ToList())
            AllRecentActivityRows.Add(row);

        foreach (var row in AllRecentActivityRows.Take(3))
            RecentActivityTableRows.Add(row);

        foreach (var fuel in VehicleDataService.Instance.GetFuelEntries(vehicleId).OrderByDescending(f => f.FuelDate))
        {
            var row = new DashboardTableRow
            {
                Date = fuel.FuelDate.ToString("dd MMM yyyy"),
                Type = fuel.FuelType,
                Detail = fuel.Station,
                Status = $"₹{fuel.TotalCost:N0}"
            };
            AllFuelExpenseRows.Add(row);
            if (FuelExpenseRows.Count < 3)
                FuelExpenseRows.Add(row);
        }

        foreach (var service in VehicleDataService.Instance.GetServiceRecords(vehicleId).OrderByDescending(s => s.ServiceDate))
        {
            var row = new DashboardTableRow
            {
                Date = service.ServiceDate.ToString("dd MMM yyyy"),
                Type = service.ServiceType,
                Detail = string.IsNullOrWhiteSpace(service.Workshop) ? "Workshop not set" : service.Workshop,
                Status = service.Amount
            };
            AllServiceExpenseRows.Add(row);
            if (ServiceExpenseRows.Count < 3)
                ServiceExpenseRows.Add(row);
        }

        OnPropertyChanged(nameof(HasRecentActivity));
        OnPropertyChanged(nameof(NoRecentActivity));

        if (items.Count == 0)
        {
            RecentActivities.Add(new ActivityItem
            {
                IconGlyph = "\uE88A",
                IconColor = Color.FromArgb("#64748B"),
                IconBgColor = Color.FromArgb("#1E3A5F"),
                Title = "No activity yet",
                Subtitle = "Add fuel and service records to see them here",
                Timestamp = "—",
                TimestampColor = Color.FromArgb("#475569")
            });
            return;
        }

        foreach (var (_, item) in items.OrderByDescending(x => x.key).Take(10))
            RecentActivities.Add(item);
    }

    private void RebuildHealthScore(int vehicleId)
    {
        HealthItems.Clear();
        HealthIssueItems.Clear();

        var services = VehicleDataService.Instance.GetServiceRecords(vehicleId).OrderByDescending(s => s.ServiceDate).ToList();
        var fuels = VehicleDataService.Instance.GetFuelEntries(vehicleId).OrderByDescending(f => f.FuelDate).ToList();
        var reminders = VehicleDataService.Instance.GetReminders(vehicleId).OrderBy(r => r.DueDate).ToList();
        var hasAnyData = services.Any() || fuels.Any() || reminders.Any();

        if (!hasAnyData)
        {
            _healthScore = 0;
            _healthLabel = "NO DATA";
            _healthDescription = "Add service, fuel, or reminder data to calculate a vehicle health score.";
            HealthIssueItems.Add(new HealthIssueItem
            {
                Prefix = "ℹ",
                PrefixColor = Color.FromArgb("#64748B"),
                Text = "No data available yet. Add your first service record or fuel log to generate health insights.",
                IsWarning = false
            });

            OnPropertyChanged(nameof(HealthScore));
            OnPropertyChanged(nameof(HealthLabel));
            OnPropertyChanged(nameof(HealthTrend));
            OnPropertyChanged(nameof(HealthDescription));
            OnPropertyChanged(nameof(ConditionText));
            OnPropertyChanged(nameof(ScoreBoostHint));
            OnPropertyChanged(nameof(HasScoreBoostHint));

            _scoreBoostHint = string.Empty;
            _ = RefreshAiHealthSummaryAsync(vehicleId);
            return;
        }

        var categories = new List<(double score, double weight, string label, string icon, Color color)>();

        if (services.Any())
        {
            var lastService = services.First();
            var days = Math.Max(0, (DateTime.Today - lastService.ServiceDate).TotalDays);
            double score = 100;
            if (days > 365) score = 20;
            else if (days > 180) score = 50;
            else if (days > 90) score = 80;

            categories.Add((score, 40, "Maintenance", "\uE86D", score >= 80 ? Color.FromArgb("#22C55E") : Color.FromArgb("#F59E0B")));
        }

        if (reminders.Any())
        {
            var overdue = reminders.Count(r => r.DueDate.Date < DateTime.Today);
            double score = overdue == 0 ? 90 : 60;
            categories.Add((score, 15, "Schedule", "\uE8B5", score >= 80 ? Color.FromArgb("#22C55E") : Color.FromArgb("#F59E0B")));
        }

        if (fuels.Any())
        {
            var recentFuelCount = fuels.Count(f => f.FuelDate >= DateTime.Today.AddDays(-30));
            double score = recentFuelCount >= 2 ? 90 : recentFuelCount == 1 ? 70 : 50;
            categories.Add((score, 10, "Fuel", "\uE901", score >= 80 ? Color.FromArgb("#22C55E") : Color.FromArgb("#F59E0B")));
        }

        if (services.Any() || fuels.Any())
        {
            var recentWindowStart = DateTime.Today.AddMonths(-2);
            var previousWindowStart = DateTime.Today.AddMonths(-4);
            var recentSpend = services.Where(s => s.ServiceDate >= recentWindowStart).Sum(s => ParseCurrency(s.Amount)) +
                             fuels.Where(f => f.FuelDate >= recentWindowStart).Sum(f => f.TotalCost);
            var previousSpend = services.Where(s => s.ServiceDate >= previousWindowStart && s.ServiceDate < recentWindowStart).Sum(s => ParseCurrency(s.Amount)) +
                               fuels.Where(f => f.FuelDate >= previousWindowStart && f.FuelDate < recentWindowStart).Sum(f => f.TotalCost);

            double score = 90;
            if (previousSpend > 0)
            {
                var ratio = recentSpend / previousSpend;
                if (ratio > 1.5) score = 40;
                else if (ratio > 1.2) score = 65;
            }

            categories.Add((score, 10, "Expense", "\uE8C8", score >= 80 ? Color.FromArgb("#22C55E") : Color.FromArgb("#F59E0B")));
        }

        var totalWeight = categories.Sum(c => c.weight);
        var overallScore = totalWeight > 0 ? Math.Round(categories.Sum(c => c.score * c.weight) / totalWeight) : 0;
        _healthScore = overallScore;
        _healthLabel = _healthScore >= 85 ? "EXCELLENT" : _healthScore >= 70 ? "GOOD" : _healthScore >= 50 ? "FAIR" : "POOR";
        _healthDescription = _healthScore >= 85 ? "Your vehicle is in excellent shape with healthy recent data."
            : _healthScore >= 70 ? "Your vehicle is generally healthy, with a few follow-ups to keep it on track."
            : _healthScore >= 50 ? "A few maintenance items need attention."
            : "Several items need attention to restore vehicle health.";

        foreach (var c in categories)
        {
            HealthItems.Add(new HealthItem
            {
                IconGlyph = c.icon,
                Label = c.label,
                Value = c.score,
                BarColor = c.color,
                PercentageColor = c.color,
                StatusText = c.score >= 80 ? "Healthy" : c.score >= 60 ? "Watch" : "Needs attention",
                StatusColor = c.color,
                StatusBgColor = c.score >= 80 ? Color.FromArgb("#DCFCE7") : Color.FromArgb("#FEF3C7")
            });
        }

        var issueBuilder = new List<HealthIssueItem>();

        if (services.Any())
        {
            var lastService = services.First();
            var days = Math.Max(0, (DateTime.Today - lastService.ServiceDate).TotalDays);
            var ok = days <= 90;
            issueBuilder.Add(new HealthIssueItem
            {
                Prefix = ok ? "✅" : "⚠",
                PrefixColor = ok ? Color.FromArgb("#22C55E") : Color.FromArgb("#F59E0B"),
                Text = ok ? "Service history is current and healthy" : $"Last service was {days:0} days ago",
                IsWarning = !ok
            });
        }

        if (fuels.Any())
        {
            var recentFuelCount = fuels.Count(f => f.FuelDate >= DateTime.Today.AddDays(-30));
            var ok = recentFuelCount >= 2;
            issueBuilder.Add(new HealthIssueItem
            {
                Prefix = ok ? "✅" : "⚠",
                PrefixColor = ok ? Color.FromArgb("#22C55E") : Color.FromArgb("#F59E0B"),
                Text = ok ? "Fuel tracking is active and up to date" : "Fuel records are not recent enough",
                IsWarning = !ok
            });
        }

        if (reminders.Any())
        {
            var overdue = reminders.Count(r => r.DueDate.Date < DateTime.Today);
            if (overdue > 0)
            {
                issueBuilder.Add(new HealthIssueItem
                {
                    Prefix = "⚠",
                    PrefixColor = Color.FromArgb("#F59E0B"),
                    Text = $"{overdue} reminder(s) are overdue",
                    IsWarning = true
                });
            }
            else
            {
                issueBuilder.Add(new HealthIssueItem
                {
                    Prefix = "✅",
                    PrefixColor = Color.FromArgb("#22C55E"),
                    Text = "No missed tasks — reminders are on track",
                    IsWarning = false
                });
            }
        }

        foreach (var item in issueBuilder.Take(4))
            HealthIssueItems.Add(item);

        var issueCount = HealthIssueItems.Count(i => i.IsWarning);
        var boostPoints = 0;
        var boostAction = "Maintain the current service schedule";

        if (services.Any())
        {
            var lastService = services.First();
            var days = Math.Max(0, (DateTime.Today - lastService.ServiceDate).TotalDays);
            if (days > 90)
            {
                boostPoints = Math.Max(boostPoints, 15);
                boostAction = "Complete the next service";
            }
        }

        if (reminders.Any())
        {
            var overdue = reminders.Count(r => r.DueDate.Date < DateTime.Today);
            if (overdue > 0)
            {
                boostPoints = Math.Max(boostPoints, 10);
                boostAction = "Clear the overdue reminder";
            }
        }

        if (fuels.Any())
        {
            var recentFuelCount = fuels.Count(f => f.FuelDate >= DateTime.Today.AddDays(-30));
            if (recentFuelCount < 2)
            {
                boostPoints = Math.Max(boostPoints, 8);
                boostAction = "Log a recent fuel entry";
            }
        }

        if (boostPoints > 0 && issueCount > 0)
        {
            var boostedScore = Math.Min(100, _healthScore + boostPoints);
            _scoreBoostHint = $"{boostAction} → score could reach ~{boostedScore}/100";
        }
        else
        {
            _scoreBoostHint = string.Empty;
        }

        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(HealthLabel));
        OnPropertyChanged(nameof(HealthTrend));
        OnPropertyChanged(nameof(HealthDescription));
        OnPropertyChanged(nameof(ConditionText));
        OnPropertyChanged(nameof(ScoreBoostHint));
        OnPropertyChanged(nameof(HasScoreBoostHint));

        _ = RefreshAiHealthSummaryAsync(vehicleId);
    }

    private async Task RefreshAiHealthSummaryAsync(int vehicleId)
    {
        var vehicle = VehicleDataService.Instance.Vehicles.FirstOrDefault(v => v.Id == vehicleId) ?? _selectedVehicle;
        if (vehicle == null) return;

        var hasAnyData = VehicleDataService.Instance.GetServiceRecords(vehicleId).Any() ||
                         VehicleDataService.Instance.GetFuelEntries(vehicleId).Any() ||
                         VehicleDataService.Instance.GetReminders(vehicleId).Any();

        if (!hasAnyData)
        {
            _isAiHealthLoading = false;
            _aiHealthSummary = "No data available yet. Add a service record or fuel log to generate AI health insights.";
            OnPropertyChanged(nameof(AiHealthSummary));
            OnPropertyChanged(nameof(IsAiHealthLoading));
            return;
        }

        // Ensure key is loaded before checking
        if (!AzureOpenAIService.HasApiKey)
            await AzureOpenAIService.LoadApiKeyFromStorageAsync();

        if (!AzureOpenAIService.HasApiKey)
        {
            _isAiHealthLoading = false;
            _aiHealthSummary = "Configure your Azure OpenAI API key in Settings to get AI-powered health analysis.";
            OnPropertyChanged(nameof(AiHealthSummary));
            OnPropertyChanged(nameof(IsAiHealthLoading));
            return;
        }
        _aiHealthSummary = string.Empty;
        OnPropertyChanged(nameof(IsAiHealthLoading));
        OnPropertyChanged(nameof(AiHealthSummary));

        var issues    = HealthIssueItems.Where(i =>  i.IsWarning).Select(i => $"- {i.Text}").ToList();
        var positives = HealthIssueItems.Where(i => !i.IsWarning).Select(i => $"- {i.Text}").ToList();

        // Tone of the response depends on the health score so the message feels appropriate
        var tone = _healthScore >= 85
            ? "The vehicle is in excellent shape. Acknowledge what is working well, then give one specific proactive tip to keep it that way."
            : _healthScore >= 70
            ? "The vehicle is generally healthy but has a minor item to watch. Briefly say what is going well, then clearly state the one thing the owner should act on next."
            : "The vehicle needs attention. Lead with the most important issue the owner must fix, explain why it matters, and give a clear next step.";        

        var prompt = $"""
Vehicle: {vehicle.Make} {vehicle.Model} ({vehicle.Year})
Health score: {_healthScore}/100 ({_healthLabel})
What is working well:
{(positives.Any() ? string.Join(Environment.NewLine, positives) : "- No positives recorded yet")}
Items that need attention:
{(issues.Any() ? string.Join(Environment.NewLine, issues) : "- None")}
Next recommended action: {(_scoreBoostHint.Length > 0 ? _scoreBoostHint : "Keep the service schedule current and log fuel entries regularly.")}

Write exactly 2 complete sentences in plain, friendly English for a vehicle owner.
{tone}
Use the owner's vehicle name ({vehicle.Make} {vehicle.Model}) naturally.
Do not use bullet points, headers, bold text, or markdown. Do not start with words like "Biggest risk" or "Summary".
""";

        var result = await new AzureOpenAIService().GetResultsFromAI(
            prompt,
            "You are a friendly vehicle health advisor writing a short summary for a car owner. Use plain English, complete sentences, and only the data provided. Never use markdown or bullet points.");
        var summary = string.IsNullOrWhiteSpace(result)
            ? $"Your {vehicle.Make} {vehicle.Model} is in {_healthLabel.ToLower()} condition (score {_healthScore}/100). {(_scoreBoostHint.Length > 0 ? _scoreBoostHint : "Keep your service schedule current and log fuel entries regularly.")}"
            : result;

        _aiHealthSummary = SimplifyAiHealthSummary(summary);

        _isAiHealthLoading = false;
        OnPropertyChanged(nameof(AiHealthSummary));
        OnPropertyChanged(nameof(IsAiHealthLoading));
    }

    private static string SimplifyAiHealthSummary(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Vehicle health is being updated.";

        // Collapse line breaks and extra spaces left by the model
        return System.Text.RegularExpressions.Regex
            .Replace(text.Replace("\r\n", " ").Replace("\n", " "), @" {2,}", " ")
            .Trim();
    }

    private static double ParseCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        var cleaned = value.Replace("₹", "")
                           .Replace("₹ ", "")
                           .Replace(",", "")
                           .Trim();

        return double.TryParse(cleaned, out var parsed) ? parsed : 0;
    }

    private static string FormatTimestamp(DateTime date)
    {
        var diff = (DateTime.Today - date.Date).TotalDays;
        if (diff < 0) return date.ToString("dd MMM").ToUpper();
        if (diff < 1) return "TODAY";
        if (diff < 2) return "YESTERDAY";
        if (diff < 7) return $"{(int)diff}D AGO";
        return date.ToString("dd MMM").ToUpper();
    }

    private static Color GetTimestampColor(DateTime date)
    {
        var diff = (DateTime.Today - date.Date).TotalDays;
        if (diff < 1) return Color.FromArgb("#3B82F6");
        if (diff < 2) return Color.FromArgb("#22C55E");
        return Color.FromArgb("#94A3B8");
    }


    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string prop = "")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        OnPropertyChanged(prop);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string prop = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
