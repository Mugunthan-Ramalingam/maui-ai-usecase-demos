using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using Newtonsoft.Json.Linq;
using Syncfusion.Maui.Maps;
using Syncfusion.Maui.Scheduler;
using SmartVehicleCare.Models;
using SmartVehicleCare.Services;

namespace SmartVehicleCare.ViewModels;

public class VehicleCenterViewModel : INotifyPropertyChanged
{
    public VehicleCenterViewModel()
    {
        SetDayViewCommand   = new Command(() => CurrentSchedulerView = SchedulerView.Day);
        SetWeekViewCommand  = new Command(() => CurrentSchedulerView = SchedulerView.Week);
        SetMonthViewCommand = new Command(() => CurrentSchedulerView = SchedulerView.Month);

        AddScheduleCommand = new Command(() =>
        {
            AddScheduleViewModel.VehicleLabel = $"{SelectedVehicleName} • {SelectedVehicleRegistration}";
            if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                IsAddScheduleDrawerVisible = true;
            else
                IsAddScheduleBottomSheetVisible = true;
        });

        CloseAddScheduleCommand = new Command(() =>
        {
            IsAddScheduleDrawerVisible = false;
            IsAddScheduleBottomSheetVisible = false;
            AddScheduleViewModel.Reset();
        });

        AddScheduleViewModel.OnScheduleSaved += reminder =>
        {
            AddAppointmentFromReminder(reminder);
            RebuildScheduleAlerts();
            IsAddScheduleDrawerVisible = false;
            IsAddScheduleBottomSheetVisible = false;
        };
        AddScheduleViewModel.OnCloseRequested += () =>
        {
            IsAddScheduleDrawerVisible = false;
            IsAddScheduleBottomSheetVisible = false;
        };

        ToggleNearbyMapTypeCommand = new Command<string>(mode =>
        {
            if (string.IsNullOrWhiteSpace(mode)) return;

            _nearbyMapMode = string.Equals(mode.Trim(), "Fuel", StringComparison.OrdinalIgnoreCase)
                ? "Fuel"
                : "Service";

            OnPropertyChanged(nameof(IsServiceNetworkSelected));
            OnPropertyChanged(nameof(IsFuelNetworkSelected));
            OnPropertyChanged(nameof(NearbyMapTitle));
            OnPropertyChanged(nameof(NearbyMapSummary));
            _ = RefreshNearbyNetworkAsync();
        });

        // Auto-refresh whenever the user saves/clears the API key in Settings
        AzureOpenAIService.ApiKeyChanged += () =>
            MainThread.BeginInvokeOnMainThread(() => _ = RefreshNearbyNetworkAsync());


        // Add Service
        AddServiceCommand = new Command(() =>
        {
            AddServiceViewModel.VehicleLabel = $"{SelectedVehicleName} • {SelectedVehicleRegistration}";
            if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                IsAddServiceDrawerVisible = true;
            else
                IsAddServiceBottomSheetVisible = true;
        });
        CloseAddServiceCommand = new Command(() =>
        {
            IsAddServiceDrawerVisible = false;
            IsAddServiceBottomSheetVisible = false;
            AddServiceViewModel.Reset();
        });
        AddServiceViewModel.OnServiceSaved += record =>
        {
            var vid = SelectedVehicle?.Id ?? 0;
            if (!_serviceByVehicle.ContainsKey(vid)) _serviceByVehicle[vid] = new();
            _serviceByVehicle[vid].Insert(0, record);
            ServiceHistory.Insert(0, record);
            VehicleDataService.Instance.AddServiceRecord(vid, record);
            RebuildExpenseHistory();
            IsAddServiceDrawerVisible = false;
            IsAddServiceBottomSheetVisible = false;
            AddServiceViewModel.Reset();
        };
        AddServiceViewModel.OnCloseRequested += () =>
        {
            IsAddServiceDrawerVisible = false;
            IsAddServiceBottomSheetVisible = false;
        };

        ShowServiceLogDetailsCommand = new Command(() =>
        {
            ServiceLogPopupRows.Clear();
            foreach (var item in ServiceHistory)
                ServiceLogPopupRows.Add(item);
            OnPropertyChanged(nameof(ServiceLogPopupHeight));
            IsServiceLogPopupVisible = true;
        });

        CloseServiceLogPopupCommand = new Command(() => IsServiceLogPopupVisible = false);

        ShowFuelSpendingDetailsCommand = new Command(() =>
        {
            FuelPopupRows.Clear();
            foreach (var item in FuelEntries)
                FuelPopupRows.Add(item);
            OnPropertyChanged(nameof(FuelPopupHeight));
            IsFuelSpendingPopupVisible = true;
        });

        CloseFuelSpendingPopupCommand = new Command(() => IsFuelSpendingPopupVisible = false);

        // Add Fuel
        AddFuelCommand = new Command(() =>
        {
            AddFuelViewModel.VehicleLabel = $"\U0001F697 {SelectedVehicleName} \u2022 {SelectedVehicleRegistration}";
            if (DeviceInfo.Platform == DevicePlatform.WinUI || DeviceInfo.Platform == DevicePlatform.MacCatalyst)
                IsAddFuelDrawerVisible = true;
            else
                IsAddFuelBottomSheetVisible = true;
        });
        CloseAddFuelCommand = new Command(() =>
        {
            IsAddFuelDrawerVisible = false;
            IsAddFuelBottomSheetVisible = false;
            AddFuelViewModel.Reset();
        });
        AddFuelViewModel.OnFuelEntrySaved += entry =>
        {
            var vid = SelectedVehicle?.Id ?? 0;
            if (!_fuelByVehicle.ContainsKey(vid)) _fuelByVehicle[vid] = new();
            _fuelByVehicle[vid].Insert(0, entry);
            FuelEntries.Insert(0, entry);
            VehicleDataService.Instance.AddFuelEntry(vid, entry);
            RebuildExpenseHistory();
            IsAddFuelDrawerVisible = false;
            IsAddFuelBottomSheetVisible = false;
            AddFuelViewModel.Reset();
        };
        AddFuelViewModel.OnCloseRequested += () =>
        {
            IsAddFuelDrawerVisible = false;
            IsAddFuelBottomSheetVisible = false;
        };

        SelectEditStatusCommand  = new Command<string>(s => EditServiceStatus = s);
        SelectEditTypeCommand    = new Command<string>(t => EditServiceType   = t);
        SelectEditFuelTypeCommand = new Command<string>(t => EditFuelType     = t);

        // Edit service
        EditServiceCommand = new Command<ServiceRecord>(record =>
        {
            if (record == null) return;
            _editingServiceRecord = record;
            EditServiceDate     = record.ServiceDate;
            EditServiceType     = record.ServiceType;
            EditServiceAmount   = record.Amount.Replace("\u20b9", "").Replace(",", "").Trim();
            EditServiceWorkshop = record.Workshop;
            EditServiceMileage  = record.Mileage.Replace(" km", "").Trim();
            EditServiceNotes    = record.Notes;
            EditServiceStatus   = record.Status;
            IsServiceLogPopupVisible   = false;
            IsEditServiceDrawerVisible = true;
        });

        SaveServiceCommand = new Command(() =>
        {
            if (_editingServiceRecord == null) return;
            _editingServiceRecord.ServiceDate = EditServiceDate;
            _editingServiceRecord.ServiceType = EditServiceType;
            _editingServiceRecord.Amount      = $"\u20b9{EditServiceAmount}";
            _editingServiceRecord.Workshop    = EditServiceWorkshop;
            _editingServiceRecord.Mileage     = string.IsNullOrWhiteSpace(EditServiceMileage) ? "\u2014"
                                                : EditServiceMileage.Contains("km") ? EditServiceMileage : $"{EditServiceMileage} km";
            _editingServiceRecord.Notes       = EditServiceNotes;
            _editingServiceRecord.Status      = EditServiceStatus;
            ComputeStatusColors(EditServiceStatus, out var sc, out var sbg);
            _editingServiceRecord.StatusColor   = sc;
            _editingServiceRecord.StatusBgColor = sbg;
            // Re-sort by date and force full collection refresh
            var sorted = ServiceHistory.OrderByDescending(r => r.ServiceDate).ToList();
            ServiceHistory.Clear();
            foreach (var r in sorted) ServiceHistory.Add(r);
            RebuildExpenseHistory();
            OnPropertyChanged(nameof(ServiceLogPopupHeight));
            IsEditServiceDrawerVisible = false;
            _editingServiceRecord = null;
            VehicleDataService.Instance.NotifyDataChanged(SelectedVehicle?.Id ?? 0);
        });

        CancelEditServiceCommand = new Command(() =>
        {
            IsEditServiceDrawerVisible = false;
            _editingServiceRecord = null;
        });

        DeleteServiceCommand = new Command<ServiceRecord>(record =>
        {
            if (record == null) return;
            var vid = SelectedVehicle?.Id ?? 0;
            ServiceHistory.Remove(record);
            if (_serviceByVehicle.TryGetValue(vid, out var slist)) slist.Remove(record);
            VehicleDataService.Instance.RemoveServiceRecord(vid, record);
            IsServiceLogPopupVisible = false;
            OnPropertyChanged(nameof(ServiceLogPopupHeight));
            RebuildExpenseHistory();
        });

        // Edit fuel
        EditFuelCommand = new Command<FuelEntry>(entry =>
        {
            if (entry == null) return;
            _editingFuelEntry    = entry;
            EditFuelDate         = entry.FuelDate;
            EditFuelType         = entry.FuelType;
            EditFuelStation      = entry.Station;
            EditFuelLitres       = entry.LitresFilled.ToString("N1");
            EditFuelCostPerLitre = entry.CostPerLitre.ToString("N2");
            EditFuelOdometer     = entry.OdometerReading > 0 ? entry.OdometerReading.ToString() : string.Empty;
            EditFuelIsFullTank   = entry.IsFullTank;
            IsFuelSpendingPopupVisible = false;
            IsEditFuelDrawerVisible = true;
        });

        SaveFuelCommand = new Command(() =>
        {
            if (_editingFuelEntry == null) return;
            _editingFuelEntry.FuelDate = EditFuelDate;
            _editingFuelEntry.FuelType = EditFuelType;
            _editingFuelEntry.Station  = EditFuelStation;
            if (double.TryParse(EditFuelLitres, out var lit))       _editingFuelEntry.LitresFilled    = lit;
            if (double.TryParse(EditFuelCostPerLitre, out var cpl)) _editingFuelEntry.CostPerLitre    = cpl;
            if (int.TryParse(EditFuelOdometer, out var odo))        _editingFuelEntry.OdometerReading = odo;
            _editingFuelEntry.IsFullTank = EditFuelIsFullTank;
            // Re-sort by date and force full collection refresh
            var sorted = FuelEntries.OrderByDescending(f => f.FuelDate).ToList();
            FuelEntries.Clear();
            foreach (var f in sorted) FuelEntries.Add(f);
            RebuildExpenseHistory();
            OnPropertyChanged(nameof(FuelPopupHeight));
            IsEditFuelDrawerVisible = false;
            _editingFuelEntry = null;
            VehicleDataService.Instance.NotifyDataChanged(SelectedVehicle?.Id ?? 0);
        });

        CancelEditFuelCommand = new Command(() =>
        {
            IsEditFuelDrawerVisible = false;
            _editingFuelEntry = null;
        });

        DeleteFuelCommand = new Command<FuelEntry>(entry =>
        {
            if (entry == null) return;
            var vid = SelectedVehicle?.Id ?? 0;
            FuelEntries.Remove(entry);
            if (_fuelByVehicle.TryGetValue(vid, out var flist)) flist.Remove(entry);
            VehicleDataService.Instance.RemoveFuelEntry(vid, entry);
            IsFuelSpendingPopupVisible = false;
            OnPropertyChanged(nameof(FuelPopupHeight));
            RebuildExpenseHistory();
        });

        ServiceHistory.CollectionChanged += (_, _) =>
        {
            RefreshVisibleCollections();
            OnPropertyChanged(nameof(TotalServices));
            OnPropertyChanged(nameof(LastServiceText));
            OnPropertyChanged(nameof(LifetimeSpend));
            OnPropertyChanged(nameof(NextServiceDays));
            OnPropertyChanged(nameof(NextServiceDetail));
            OnPropertyChanged(nameof(HasServiceHistory));
            OnPropertyChanged(nameof(NoServiceHistory));
            OnPropertyChanged(nameof(HasExpenseHistory));
            OnPropertyChanged(nameof(NoExpenseHistory));
        };

        FuelEntries.CollectionChanged += (_, _) =>
        {
            RefreshVisibleCollections();
            OnPropertyChanged(nameof(HasFuelEntries));
            OnPropertyChanged(nameof(NoFuelEntries));
            OnPropertyChanged(nameof(LifetimeSpend));
            OnPropertyChanged(nameof(HasExpenseHistory));
            OnPropertyChanged(nameof(NoExpenseHistory));
        };

        ExpenseHistory.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasExpenseHistory));
            OnPropertyChanged(nameof(NoExpenseHistory));
        };

        SelectExpenseDurationCommand = new Command<string>(months =>
        {
            if (int.TryParse(months, out var m)) _expenseDurationMonths = m;
            OnPropertyChanged(nameof(IsExpense3MSelected));
            OnPropertyChanged(nameof(IsExpense6MSelected));
            OnPropertyChanged(nameof(IsExpense1YSelected));
            OnPropertyChanged(nameof(IsExpense3YSelected));
            OnPropertyChanged(nameof(IsExpense5YSelected));
            RebuildExpenseHistory();
        });


        InitializeData();
    }

    // ── Page header ───────────────────────────────────────────────────────────

    public string PageTitle    => "Vehicle Center";
    public string PageSubtitle => "Manage your vehicle from one workspace.";

    private DateTime _currentMonthDisplayDate = DateTime.Today;
    public DateTime CurrentMonthDisplayDate
    {
        get => _currentMonthDisplayDate;
        set => SetProperty(ref _currentMonthDisplayDate, value);
    }

    // ── Vehicle selector (mirrors MainViewModel; share via service in future) ─

    public ObservableCollection<Vehicle> Vehicles { get; } = new();

    private Vehicle _selectedVehicle = null!;
    public Vehicle SelectedVehicle
    {
        get => _selectedVehicle;
        set
        {
            if (SetProperty(ref _selectedVehicle, value))
            {
                VehicleDataService.Instance.SelectedVehicle = value;
                RefreshVehicleData();
            }
        }
    }

    public string SelectedVehicleName         => SelectedVehicle?.MakeModel          ?? "—";
    public string SelectedVehicleRegistration => SelectedVehicle?.RegistrationNumber ?? "—";

    // ── Maintenance tab — KPI cards (dynamic) ─────────────────────────────────

    public string TotalServices => ServiceHistory.Count > 0 ? ServiceHistory.Count.ToString() : "0";

    public string LastServiceText
    {
        get
        {
            if (ServiceHistory.Count == 0) return "No records";
            var latest = ServiceHistory.OrderByDescending(s => s.ServiceDate).First();
            var diff = DateTime.Today - latest.ServiceDate;
            if (diff.TotalDays < 1)  return "Today";
            if (diff.TotalDays < 7)  return $"{(int)diff.TotalDays} days ago";
            if (diff.TotalDays < 31) return $"{(int)(diff.TotalDays / 7)} weeks ago";
            return $"{(int)(diff.TotalDays / 30)} months ago";
        }
    }

    public string LifetimeSpend
    {
        get
        {
            double total = 0;
            foreach (var s in ServiceHistory)
            {
                var raw = s.Amount.Replace("₹", "").Replace(",", "").Trim();
                if (double.TryParse(raw, out var v)) total += v;
            }
            foreach (var f in FuelEntries)
                total += f.TotalCost;
            return total > 0 ? $"₹{total:N0}" : "₹0";
        }
    }

    public string NextServiceDays  => ComputeNextServiceDaysText();
    public string NextServiceDetail => ComputeNextServiceDetail();

    private string ComputeNextServiceDaysText()
    {
        var next = ComputeNextServiceDate(out _);
        if (next == DateTime.MinValue) return "Not scheduled";
        var days = (int)(next - DateTime.Today).TotalDays;
        return days <= 0 ? "Today" : $"~{days} days";
    }

    private string ComputeNextServiceDetail()
    {
        var next = ComputeNextServiceDate(out string source);
        if (next == DateTime.MinValue) return "No upcoming service";
        return $"{source} · {next:dd MMM yyyy}";
    }

    // ── Maintenance tab — AI Insights ─────────────────────────────────────────

    public ObservableCollection<AIInsightItem> AIInsights { get; } = new();

    public string SpendingOptimizationTitle => "Spending Optimization";
    public string SpendingOptimizationSummary { get; private set; } = "Add service or fuel records to generate spending insights.";

    // ── Maintenance tab — Health Analysis ─────────────────────────────────────

    public int    OverallHealthValue    => 88;
    public string OverallHealthText     => "88%";
    public string HealthConditionLabel  => "Vehicle is in Good Condition";
    public string HealthConditionDetail =>
        "Regular maintenance is keeping your vehicle running smoothly. Address the AI insights to maintain this score.";

    public List<HealthGaugeSegment> HealthGaugeData { get; } = new()
    {
        new HealthGaugeSegment { Category = "Health",    Value = 88 },
        new HealthGaugeSegment { Category = "Remaining", Value = 12 }
    };

    public ObservableCollection<HealthMetric> HealthMetrics { get; } = new();

    // ── Maintenance tab — Suggested tasks ────────────────────────────────────

    public ObservableCollection<string> SuggestedTasks { get; } = new()
    {
        "Oil Change", "Brake Check", "Wheel Alignment", "Battery Check", "Tire Rotation"
    };

    // ── Maintenance tab — Service history ────────────────────────────────────

    public ObservableCollection<ServiceRecord> ServiceHistory { get; } = new();

    public ObservableCollection<ServiceRecord> VisibleServiceHistory { get; } = new();
    public ObservableCollection<ServiceRecord> ServiceLogPopupRows { get; } = new();

    public ObservableCollection<FuelEntry> FuelEntries { get; } = new();
    public ObservableCollection<FuelEntry> VisibleFuelEntries { get; } = new();
    public ObservableCollection<FuelEntry> FuelPopupRows { get; } = new();

    public bool IsServiceLogPopupVisible
    {
        get => _isServiceLogPopupVisible;
        set => SetProperty(ref _isServiceLogPopupVisible, value);
    }

    public bool IsFuelSpendingPopupVisible
    {
        get => _isFuelSpendingPopupVisible;
        set => SetProperty(ref _isFuelSpendingPopupVisible, value);
    }

    public double ServiceLogPopupHeight => Math.Clamp(ServiceLogPopupRows.Count * 42 + 140, 280, 640);
    public double FuelPopupHeight       => Math.Clamp(FuelPopupRows.Count * 42 + 140, 280, 640);

    private bool _isServiceLogPopupVisible;
    private bool _isFuelSpendingPopupVisible;

    public ICommand ShowServiceLogDetailsCommand { get; private set; } = default!;
    public ICommand CloseServiceLogPopupCommand { get; private set; } = default!;
    public ICommand ShowFuelSpendingDetailsCommand { get; private set; } = default!;
    public ICommand CloseFuelSpendingPopupCommand { get; private set; } = default!;

    // Per-vehicle storage (vehicle.Id → list)
    private readonly Dictionary<int, List<ServiceRecord>> _serviceByVehicle = new();
    private readonly Dictionary<int, List<FuelEntry>>    _fuelByVehicle    = new();

    public bool HasServiceHistory => ServiceHistory.Count > 0;
    public bool NoServiceHistory  => ServiceHistory.Count == 0;
    public bool HasFuelEntries    => FuelEntries.Count > 0;
    public bool NoFuelEntries     => FuelEntries.Count == 0;

    // ── Maintenance tab — Expense chart ──────────────────────────────────

    public ObservableCollection<ExpenseDataPoint> ExpenseHistory { get; } = new();

    private int _expenseDurationMonths = 3;
    public bool IsExpense3MSelected  => _expenseDurationMonths == 3;
    public bool IsExpense6MSelected  => _expenseDurationMonths == 6;
    public bool IsExpense1YSelected  => _expenseDurationMonths == 12;
    public bool IsExpense3YSelected  => _expenseDurationMonths == 36;
    public bool IsExpense5YSelected  => _expenseDurationMonths == 60;
    public ICommand SelectExpenseDurationCommand { get; private set; } = default!;
    // Chart buckets are pre-filled with zero for every month in range, so use the underlying
    // service/fuel records (not ExpenseHistory.Count) to detect a true "no data" state.
    public bool HasExpenseHistory => ServiceHistory.Count > 0 || FuelEntries.Count > 0;
    public bool NoExpenseHistory  => !HasExpenseHistory;

    // ── Maintenance tab — Health radar chart ────────────────────────────

    public ObservableCollection<RadarDataPoint> HealthRadarData { get; } = new();

    // ── Maintenance tab — Nearby network ────────────────────────────────

    private const double DefaultLatitude = 13.0827;
    private const double DefaultLongitude = 80.2707;
    private const double MaxNearbyDistanceKm = 10.0;
    private string _nearbyMapMode = "Fuel";

    private double _userLatitude = DefaultLatitude;
    private double _userLongitude = DefaultLongitude;
    private double _userMapZoom = 13;
    public double UserLatitude  { get => _userLatitude;  set => SetProperty(ref _userLatitude, value); }
    public double UserLongitude { get => _userLongitude; set => SetProperty(ref _userLongitude, value); }
    public double UserMapZoom   { get => _userMapZoom;   set => SetProperty(ref _userMapZoom, value); }

    public ObservableCollection<NearbyNetworkItem> NearbyNetworkItems { get; } = new();
    public ObservableCollection<MapMarker>          MapMarkers         { get; } = new();
    public bool IsServiceNetworkSelected => _nearbyMapMode == "Service";
    public bool IsFuelNetworkSelected    => _nearbyMapMode == "Fuel";
    public string NearbyMapTitle   => IsServiceNetworkSelected ? "Service Centers" : "Fuel Stations";
    public string NearbyMapSummary => "Within 10 km of your current location";

    private bool _isNearbyEmpty;
    public bool IsNearbyEmpty
    {
        get => _isNearbyEmpty;
        set => SetProperty(ref _isNearbyEmpty, value);
    }

    public ICommand ToggleNearbyMapTypeCommand { get; }

    // ── Add Service drawer ───────────────────────────────────────────────
    public AddServiceViewModel AddServiceViewModel { get; } = new();

    private bool _isAddServiceDrawerVisible;
    public bool IsAddServiceDrawerVisible
    {
        get => _isAddServiceDrawerVisible;
        set => SetProperty(ref _isAddServiceDrawerVisible, value);
    }

    private bool _isAddServiceBottomSheetVisible;
    public bool IsAddServiceBottomSheetVisible
    {
        get => _isAddServiceBottomSheetVisible;
        set => SetProperty(ref _isAddServiceBottomSheetVisible, value);
    }

    public ICommand AddServiceCommand      { get; private set; } = default!;
    public ICommand CloseAddServiceCommand { get; private set; } = default!;

    // ── Add Fuel drawer ──────────────────────────────────────────────────
    public AddFuelViewModel AddFuelViewModel { get; } = new();

    private bool _isAddFuelDrawerVisible;
    public bool IsAddFuelDrawerVisible
    {
        get => _isAddFuelDrawerVisible;
        set => SetProperty(ref _isAddFuelDrawerVisible, value);
    }

    private bool _isAddFuelBottomSheetVisible;
    public bool IsAddFuelBottomSheetVisible
    {
        get => _isAddFuelBottomSheetVisible;
        set => SetProperty(ref _isAddFuelBottomSheetVisible, value);
    }

    public ICommand AddFuelCommand      { get; private set; } = default!;
    public ICommand CloseAddFuelCommand { get; private set; } = default!;

    // ── Edit / Delete Service ─────────────────────────────────────────────────
    private ServiceRecord? _editingServiceRecord;
    private bool _isEditServiceDrawerVisible;
    public bool IsEditServiceDrawerVisible
    {
        get => _isEditServiceDrawerVisible;
        set => SetProperty(ref _isEditServiceDrawerVisible, value);
    }

    private string _editServiceType = string.Empty;
    public string EditServiceType
    {
        get => _editServiceType;
        set
        {
            if (SetProperty(ref _editServiceType, value))
            {
                OnPropertyChanged(nameof(IsEditOilChangeSelected));
                OnPropertyChanged(nameof(IsEditTyreRotationSelected));
                OnPropertyChanged(nameof(IsEditBrakeServiceSelected));
                OnPropertyChanged(nameof(IsEditElectricalSelected));
                OnPropertyChanged(nameof(IsEditGeneralInspectionSelected));
                OnPropertyChanged(nameof(IsEditCustomTypeSelected));
            }
        }
    }
    public bool IsEditOilChangeSelected         => EditServiceType == "Oil Change";
    public bool IsEditTyreRotationSelected      => EditServiceType == "Tyre Rotation";
    public bool IsEditBrakeServiceSelected      => EditServiceType == "Brake Service";
    public bool IsEditElectricalSelected        => EditServiceType == "Electrical";
    public bool IsEditGeneralInspectionSelected => EditServiceType == "General Inspection";
    public bool IsEditCustomTypeSelected        => EditServiceType is not ("Oil Change" or "Tyre Rotation" or "Brake Service" or "Electrical" or "General Inspection");
    private string _editServiceAmount = string.Empty;
    public string EditServiceAmount { get => _editServiceAmount; set => SetProperty(ref _editServiceAmount, value); }
    private string _editServiceWorkshop = string.Empty;
    public string EditServiceWorkshop { get => _editServiceWorkshop; set => SetProperty(ref _editServiceWorkshop, value); }
    private string _editServiceMileage = string.Empty;
    public string EditServiceMileage { get => _editServiceMileage; set => SetProperty(ref _editServiceMileage, value); }
    private string _editServiceNotes = string.Empty;
    public string EditServiceNotes { get => _editServiceNotes; set => SetProperty(ref _editServiceNotes, value); }
    private DateTime _editServiceDate = DateTime.Today;
    public DateTime EditServiceDate
    {
        get => _editServiceDate;
        set { if (SetProperty(ref _editServiceDate, value)) OnPropertyChanged(nameof(EditServiceDateDisplay)); }
    }
    public string EditServiceDateDisplay => _editServiceDate.ToString("dd MMM yyyy");
    private string _editServiceStatus = "Completed";
    public string EditServiceStatus
    {
        get => _editServiceStatus;
        set
        {
            if (SetProperty(ref _editServiceStatus, value))
            {
                OnPropertyChanged(nameof(IsEditStatusCompleted));
                OnPropertyChanged(nameof(IsEditStatusPending));
                OnPropertyChanged(nameof(IsEditStatusScheduled));
            }
        }
    }
    public bool IsEditStatusCompleted => EditServiceStatus == "Completed";
    public bool IsEditStatusPending   => EditServiceStatus == "Pending";
    public bool IsEditStatusScheduled => EditServiceStatus == "Scheduled";

    public ICommand EditServiceCommand        { get; private set; } = default!;
    public ICommand SaveServiceCommand        { get; private set; } = default!;
    public ICommand CancelEditServiceCommand  { get; private set; } = default!;
    public ICommand DeleteServiceCommand      { get; private set; } = default!;
    public ICommand SelectEditStatusCommand   { get; private set; } = default!;
    public ICommand SelectEditTypeCommand     { get; private set; } = default!;
    public ICommand SelectEditFuelTypeCommand { get; private set; } = default!;

    // ── Edit / Delete Fuel ────────────────────────────────────────────────────
    private FuelEntry? _editingFuelEntry;
    private bool _isEditFuelDrawerVisible;
    public bool IsEditFuelDrawerVisible
    {
        get => _isEditFuelDrawerVisible;
        set => SetProperty(ref _isEditFuelDrawerVisible, value);
    }

    private string _editFuelType = string.Empty;
    public string EditFuelType
    {
        get => _editFuelType;
        set
        {
            if (SetProperty(ref _editFuelType, value))
            {
                OnPropertyChanged(nameof(IsEditFuelPetrolSelected));
                OnPropertyChanged(nameof(IsEditFuelDieselSelected));
                OnPropertyChanged(nameof(IsEditFuelCNGSelected));
                OnPropertyChanged(nameof(IsEditFuelElectricSelected));
            }
        }
    }
    public bool IsEditFuelPetrolSelected   => EditFuelType == "Petrol";
    public bool IsEditFuelDieselSelected   => EditFuelType == "Diesel";
    public bool IsEditFuelCNGSelected      => EditFuelType == "CNG";
    public bool IsEditFuelElectricSelected => EditFuelType == "Electric";
    private string _editFuelStation = string.Empty;
    public string EditFuelStation { get => _editFuelStation; set => SetProperty(ref _editFuelStation, value); }
    private string _editFuelLitres = string.Empty;
    public string EditFuelLitres
    {
        get => _editFuelLitres;
        set { if (SetProperty(ref _editFuelLitres, value)) OnPropertyChanged(nameof(EditFuelTotalCostDisplay)); }
    }
    private string _editFuelCostPerLitre = string.Empty;
    public string EditFuelCostPerLitre
    {
        get => _editFuelCostPerLitre;
        set { if (SetProperty(ref _editFuelCostPerLitre, value)) OnPropertyChanged(nameof(EditFuelTotalCostDisplay)); }
    }
    public string EditFuelTotalCostDisplay
    {
        get
        {
            if (double.TryParse(_editFuelLitres, out var l) && double.TryParse(_editFuelCostPerLitre, out var c))
                return $"₹{l * c:N2}";
            return "₹0.00";
        }
    }
    private DateTime _editFuelDate = DateTime.Today;
    public DateTime EditFuelDate
    {
        get => _editFuelDate;
        set { if (SetProperty(ref _editFuelDate, value)) OnPropertyChanged(nameof(EditFuelDateDisplay)); }
    }
    public string EditFuelDateDisplay => _editFuelDate.ToString("dd MMM yyyy");
    private string _editFuelOdometer = string.Empty;
    public string EditFuelOdometer { get => _editFuelOdometer; set => SetProperty(ref _editFuelOdometer, value); }
    private bool _editFuelIsFullTank = true;
    public bool EditFuelIsFullTank { get => _editFuelIsFullTank; set => SetProperty(ref _editFuelIsFullTank, value); }

    public ICommand EditFuelCommand       { get; private set; } = default!;
    public ICommand SaveFuelCommand       { get; private set; } = default!;
    public ICommand CancelEditFuelCommand { get; private set; } = default!;
    public ICommand DeleteFuelCommand     { get; private set; } = default!;

    // Search
    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    // Add schedule drawer
    public AddScheduleViewModel AddScheduleViewModel { get; } = new();

    private bool _isAddScheduleDrawerVisible;
    public bool IsAddScheduleDrawerVisible
    {
        get => _isAddScheduleDrawerVisible;
        set => SetProperty(ref _isAddScheduleDrawerVisible, value);
    }

    private bool _isAddScheduleBottomSheetVisible;
    public bool IsAddScheduleBottomSheetVisible
    {
        get => _isAddScheduleBottomSheetVisible;
        set => SetProperty(ref _isAddScheduleBottomSheetVisible, value);
    }

    public ICommand AddScheduleCommand      { get; }   // initialized in constructor
    public ICommand CloseAddScheduleCommand { get; }   // initialized in constructor


    // ── Schedule tab ─────────────────────────────────────────────────────────

    public ObservableCollection<SchedulerAppointment> ServiceAppointments { get; } = new();
    public ObservableCollection<UpcomingTask>          UpcomingTasks       { get; } = new();
    public ObservableCollection<ReminderItem>          Reminders           { get; } = new();
    public ObservableCollection<ScheduleAlert>         ScheduleAlerts      { get; } = new();

    public string PredictiveServiceDate
    {
        get
        {
            var next = ComputeNextServiceDate(out string source);
            return next != DateTime.MinValue
                ? $"{next:MMM dd} \u2022 {(string.IsNullOrWhiteSpace(source) ? "Service" : source)}"
                : "Not available";
        }
    }
    public string PredictiveServiceDescription
    {
        get
        {
            var next = ComputeNextServiceDate(out _);
            return next != DateTime.MinValue
                ? $"Next service estimated by {next:MMMM dd, yyyy}. Add a reminder to the schedule to track it."
                : "Add a service record or schedule reminder to see predictions here.";
        }
    }
    public string PredictiveMileage
    {
        get
        {
            if (int.TryParse(SelectedVehicle?.OdometerReading, out var km) && km > 0)
                return $"{km:N0} km";
            return "42,850 km";
        }
    }

    // Returns next service date + how it was derived (scheduled / estimated / MinValue = none)
    private DateTime ComputeNextServiceDate(out string source)
    {
        // 1. Any future scheduled appointment
        var nextAppt = ServiceAppointments
            .Where(a => a.StartTime > DateTime.Now)
            .OrderBy(a => a.StartTime)
            .FirstOrDefault();
        if (nextAppt != null)
        {
            source = $"Scheduled: {nextAppt.Subject}";
            return nextAppt.StartTime;
        }

        // 2. Estimate from latest service record (+90 days standard interval)
        var lastService = ServiceHistory
            .OrderByDescending(r => r.ServiceDate)
            .FirstOrDefault();
        if (lastService != null)
        {
            source = $"Est. from {lastService.ServiceType}";
            return lastService.ServiceDate.AddDays(90);
        }

        source = string.Empty;
        return DateTime.MinValue;
    }

    public ICommand BookServiceSlotCommand { get; } = new Command(() => { /* TODO */ });

    private SchedulerView _schedulerView = SchedulerView.Month;
    public SchedulerView CurrentSchedulerView
    {
        get => _schedulerView;
        set
        {
            if (SetProperty(ref _schedulerView, value))
            {
                OnPropertyChanged(nameof(IsDayViewActive));
                OnPropertyChanged(nameof(IsWeekViewActive));
                OnPropertyChanged(nameof(IsMonthViewActive));
            }
        }
    }

    public bool IsDayViewActive   => CurrentSchedulerView == SchedulerView.Day;
    public bool IsWeekViewActive  => CurrentSchedulerView == SchedulerView.Week;
    public bool IsMonthViewActive => CurrentSchedulerView == SchedulerView.Month;

    public ICommand SetDayViewCommand   { get; }
    public ICommand SetWeekViewCommand  { get; }
    public ICommand SetMonthViewCommand { get; }

    public DateTime CalendarSelectedDate { get; } = DateTime.Today;

    // ── Init ─────────────────────────────────────────────────────────────────

    private void InitializeData()
    {
        // Sync selected vehicle when changed externally (e.g., from MainViewModel)
        VehicleDataService.Instance.SelectedVehicleChanged += v =>
        {
            if (v == null) return;
            var match = Vehicles.FirstOrDefault(x => x.Id == v.Id);
            if (match != null && match != _selectedVehicle)
            {
                _selectedVehicle = match; // set backing field to avoid re-firing DataService event
                OnPropertyChanged(nameof(SelectedVehicle));
                RefreshVehicleData();
            }
        };

        // Prefer vehicles from VehicleDataService; only use demo fallback in Demo mode.
        if (VehicleDataService.Instance.Vehicles.Count > 0)
        {
            foreach (var v in VehicleDataService.Instance.Vehicles)
                Vehicles.Add(v);
            SelectedVehicle = VehicleDataService.Instance.SelectedVehicle ?? Vehicles[0];
        }
        else if (VehicleDataService.Instance.IsDemoMode)
        {
            Vehicles.Add(new Vehicle { Id = 1, Make = "Hyundai", Model = "i20", VehicleType = 0 });
            Vehicles.Add(new Vehicle { Id = 2, Make = "Honda", Model = "Activa", VehicleType = 1 });
            SelectedVehicle = Vehicles[0];
        }
        else
        {
            SelectedVehicle = null;
        }

        InitHealthMetrics();
        InitServiceHistory();
        InitExpenseHistory();
        InitHealthRadar();
        InitNearbyNetwork();
        InitSchedule();
        RefreshVehicleData();
        _ = RefreshSpendingOptimizationAsync();
    }

    private async Task RefreshSpendingOptimizationAsync()
    {
        var vehicle = SelectedVehicle ?? VehicleDataService.Instance.SelectedVehicle;
        if (vehicle == null)
        {
            SpendingOptimizationSummary = "Add a vehicle and log service or fuel records to generate spending insights.";
            OnPropertyChanged(nameof(SpendingOptimizationSummary));
            return;
        }

        var services = ServiceHistory.OrderByDescending(s => s.ServiceDate).ToList();
        var fuels = FuelEntries.OrderByDescending(f => f.FuelDate).ToList();
        if (!services.Any() && !fuels.Any())
        {
            SpendingOptimizationSummary = "No spending data available yet. Add service or fuel records to get AI insights.";
            OnPropertyChanged(nameof(SpendingOptimizationSummary));
            return;
        }

        var serviceSpend = services.Sum(s => ParseCurrency(s.Amount));
        var fuelSpend = fuels.Sum(f => f.TotalCost);
        var currentWindowStart = DateTime.Today.AddDays(-90);
        var previousWindowStart = DateTime.Today.AddDays(-180);
        var recentServiceSpend = services.Where(s => s.ServiceDate >= currentWindowStart).Sum(s => ParseCurrency(s.Amount));
        var previousServiceSpend = services.Where(s => s.ServiceDate >= previousWindowStart && s.ServiceDate < currentWindowStart).Sum(s => ParseCurrency(s.Amount));
        var recentFuelSpend = fuels.Where(f => f.FuelDate >= currentWindowStart).Sum(f => f.TotalCost);
        var previousFuelSpend = fuels.Where(f => f.FuelDate >= previousWindowStart && f.FuelDate < currentWindowStart).Sum(f => f.TotalCost);

        var recentTotal = recentServiceSpend + recentFuelSpend;
        var previousTotal = previousServiceSpend + previousFuelSpend;
        var spendChange = previousTotal > 0 ? ((recentTotal - previousTotal) / previousTotal) * 100 : 0;

        var deltaText = previousTotal > 0 ? $"{(spendChange >= 0 ? "up" : "down")} ₹{Math.Abs(recentTotal - previousTotal):N0}" : "stable";
        var fuelSaving = Math.Round(recentFuelSpend * 0.10);
        var spendContext = previousTotal > 0
            ? $"Your {vehicle.Make} {vehicle.Model} spent ₹{recentTotal:N0} in the last 90 days ({deltaText} from ₹{previousTotal:N0} the prior period) — fuel ₹{recentFuelSpend:N0}, maintenance ₹{recentServiceSpend:N0}."
            : $"Your {vehicle.Make} {vehicle.Model} spent ₹{recentTotal:N0} in the last 90 days — fuel ₹{recentFuelSpend:N0}, maintenance ₹{recentServiceSpend:N0}.";

        var fuelTrendDirection = recentFuelSpend > previousFuelSpend ? "higher" : recentFuelSpend < previousFuelSpend ? "lower" : "stable";
        var avgFuelCostPerFill = fuels.Count > 0 ? recentFuelSpend / fuels.Count : 0;
        var recommendation = recentFuelSpend >= recentServiceSpend
            ? $"To improve fuel consumption, keep tyre pressure correct, avoid unnecessary short trips during peak traffic, and service the air filter/engine on schedule to save roughly ₹{fuelSaving:N0} over the next 90 days."
            : $"Keep tyre pressure in check and avoid stop-start driving patterns to maintain fuel efficiency and protect your maintenance budget over the next 90 days.";

        var summaryText = $"{spendContext} {recommendation}";

        var odometer = vehicle.OdometerReading;
        var lastServiceDate = services.FirstOrDefault()?.ServiceDate.ToString("dd MMM yyyy") ?? "unknown";
        var lastFuelDate    = fuels.FirstOrDefault()?.FuelDate.ToString("dd MMM yyyy")     ?? "unknown";
        var avgFuelCost = fuels.Any() ? fuels.Average(f => f.CostPerLitre) : 0;
        var prompt = $"""
Vehicle: {vehicle.Make} {vehicle.Model} {vehicle.Year}, odometer {odometer} km
Last 90-day fuel spend: ₹{recentFuelSpend:N0}; previous 90-day fuel spend: ₹{previousFuelSpend:N0}; fuel trend: {fuelTrendDirection}
Last 90-day maintenance spend: ₹{recentServiceSpend:N0}; previous 90-day maintenance spend: ₹{previousServiceSpend:N0}
Total spends: ₹{recentTotal:N0} in last 90 days vs ₹{previousTotal:N0} in previous 90 days
Average fuel cost per litre: ₹{avgFuelCost:N2}; average fuel spend per fill-up: ₹{avgFuelCostPerFill:N0}; last service: {lastServiceDate}; last refuel: {lastFuelDate}

Write exactly 2 sentences in plain English:
1. State whether fuel consumption or fuel cost is rising or falling using the actual numbers above, and explain the trend in a realistic way for an Indian vehicle owner.
2. Give one specific, practical recommendation to improve fuel economy for this vehicle, such as tyre pressure, route planning, short-trip reduction, or maintenance timing. Focus on everyday fuel-saving actions that are realistic and easy to follow.
Use Indian Rupee symbol ₹ for all amounts. Do not add bullet points, headers, or markdown.
""";

        var aiResponse = await new AzureOpenAIService().GetResultsFromAI(
            prompt,
            "You are a practical automotive fuel-efficiency advisor for Indian vehicle owners. Use only the exact numbers provided. Explain the fuel trend clearly and give one realistic, daily-driving fuel-saving recommendation. Write 2 plain-English sentences only with no markdown, no bullets, no placeholders.");

        var finalText = string.IsNullOrWhiteSpace(aiResponse)
            ? summaryText
            : aiResponse;

        SpendingOptimizationSummary = NormalizeInsightLines(finalText);
        OnPropertyChanged(nameof(SpendingOptimizationSummary));
    }

    private static string NormalizeInsightLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "No spending insight available yet.";

        var lines = text
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0) return text.Trim();
        return string.Join(Environment.NewLine, lines.Take(3));
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

    private void InitHealthMetrics()
    {
        HealthMetrics.Add(new HealthMetric { Label = "Engine",       Value = 95, BarColor = Color.FromArgb("#3B5BDB") });
        HealthMetrics.Add(new HealthMetric { Label = "Battery",      Value = 88, BarColor = Color.FromArgb("#3B5BDB") });
        HealthMetrics.Add(new HealthMetric { Label = "Brake System", Value = 76, BarColor = Color.FromArgb("#EF4444"), HasWarning = true });
        HealthMetrics.Add(new HealthMetric { Label = "Cooling",      Value = 90, BarColor = Color.FromArgb("#3B5BDB") });
        HealthMetrics.Add(new HealthMetric { Label = "Transmission", Value = 92, BarColor = Color.FromArgb("#3B5BDB") });
    }

    private void InitServiceHistory()
    {
        // ✅ Only add demo data if in demo mode. Real vehicles should have NO demo data.
        if (!VehicleDataService.Instance.IsDemoMode) return;
        
        if (VehicleDataService.Instance.GetServiceRecords(1).Any()) return;

        VehicleDataService.Instance.AddServiceRecord(1, new ServiceRecord
        {
            ServiceType = "Oil & Filter Change",
            ServiceDate = DateTime.Today.AddDays(-75),
            Workshop = "Hyundai Authorized Service",
            Amount = "₹3200",
            Mileage = "45200 km",
            Status = "Completed",
            Notes = "Routine maintenance"
        });

        VehicleDataService.Instance.AddServiceRecord(1, new ServiceRecord
        {
            ServiceType = "Tyre Rotation",
            ServiceDate = DateTime.Today.AddDays(-190),
            Workshop = "WheelCare Center",
            Amount = "₹1800",
            Mileage = "44300 km",
            Status = "Completed",
            Notes = "Rotated all four tyres"
        });

        VehicleDataService.Instance.AddFuelEntry(1, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-3),
            FuelType = "Petrol",
            Station = "Shell Rajaji Nagar",
            LitresFilled = 30,
            CostPerLitre = 102.4,
            OdometerReading = 45230,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddFuelEntry(1, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-18),
            FuelType = "Petrol",
            Station = "HP Fuel Point",
            LitresFilled = 28,
            CostPerLitre = 101.8,
            OdometerReading = 44910,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddReminder(1, new ScheduleReminder
        {
            Title = "Oil Change",
            ReminderType = "Service",
            DueDate = DateTime.Today.AddDays(15),
            Priority = "High"
        });

        VehicleDataService.Instance.AddReminder(1, new ScheduleReminder
        {
            Title = "Tyre Pressure Check",
            ReminderType = "Inspection",
            DueDate = DateTime.Today.AddDays(20),
            Priority = "High"
        });
    }

    private void InitExpenseHistory()
    {
        // Built dynamically from ServiceHistory when records are added
    }

    private void RefreshVisibleCollections()
    {
        VisibleServiceHistory.Clear();
        foreach (var record in ServiceHistory.Take(5))
            VisibleServiceHistory.Add(record);

        ServiceLogPopupRows.Clear();
        foreach (var record in ServiceHistory)
            ServiceLogPopupRows.Add(record);

        VisibleFuelEntries.Clear();
        foreach (var record in FuelEntries.Take(5))
            VisibleFuelEntries.Add(record);

        FuelPopupRows.Clear();
        foreach (var record in FuelEntries)
            FuelPopupRows.Add(record);
    }

    private void RebuildExpenseHistory()
    {
        ExpenseHistory.Clear();

        var firstDayOfCurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var firstMonthInRange = firstDayOfCurrentMonth.AddMonths(-(_expenseDurationMonths - 1));

        var monthly = new Dictionary<(int Year, int Month), double>();
        var monthCursor = new DateTime(firstMonthInRange.Year, firstMonthInRange.Month, 1);
        var finalMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        while (monthCursor <= finalMonth)
        {
            monthly[(monthCursor.Year, monthCursor.Month)] = 0;
            monthCursor = monthCursor.AddMonths(1);
        }

        foreach (var s in ServiceHistory.Where(s => s.ServiceDate >= firstMonthInRange && s.ServiceDate <= finalMonth.AddMonths(1).AddDays(-1)))
        {
            var raw = s.Amount.Replace("₹", "").Replace(",", "").Trim();
            if (double.TryParse(raw, out var v))
            {
                var k = (s.ServiceDate.Year, s.ServiceDate.Month);
                if (monthly.ContainsKey(k))
                    monthly[k] += v;
            }
        }

        foreach (var f in FuelEntries.Where(f => f.FuelDate >= firstMonthInRange && f.FuelDate <= finalMonth.AddMonths(1).AddDays(-1)))
        {
            var k = (f.FuelDate.Year, f.FuelDate.Month);
            if (monthly.ContainsKey(k))
                monthly[k] += f.TotalCost;
        }

        foreach (var kv in monthly.OrderBy(k => k.Key.Year).ThenBy(k => k.Key.Month))
        {
            var label = new DateTime(kv.Key.Year, kv.Key.Month, 1).ToString("MMM yy");
            ExpenseHistory.Add(new ExpenseDataPoint { Month = label, Amount = Math.Round(kv.Value) });
        }
    }

    private void InitHealthRadar()
    {
        HealthRadarData.Add(new RadarDataPoint { Category = "Engine",     Value = 92 });
        HealthRadarData.Add(new RadarDataPoint { Category = "Brakes",     Value = 76 });
        HealthRadarData.Add(new RadarDataPoint { Category = "Fluids",     Value = 88 });
        HealthRadarData.Add(new RadarDataPoint { Category = "Electrical", Value = 84 });
        HealthRadarData.Add(new RadarDataPoint { Category = "Tyres",      Value = 90 });
    }

    private void InitNearbyNetwork()
    {
        RefreshNearbyNetwork();
    }

    private void RefreshNearbyNetwork()
    {
        _ = RefreshNearbyNetworkAsync();
    }

    private async Task RefreshNearbyNetworkAsync()
    {
        NearbyNetworkItems.Clear();
        MapMarkers.Clear();
        IsNearbyEmpty = false;

        double latitude  = DefaultLatitude;
        double longitude = DefaultLongitude;

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status == PermissionStatus.Granted)
            {
                var loc = await Geolocation.GetLastKnownLocationAsync();
                loc ??= await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
                if (loc != null)
                {
                    latitude      = loc.Latitude;
                    longitude     = loc.Longitude;
                    UserLatitude  = latitude;
                    UserLongitude = longitude;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NearbyNetwork] GPS error: {ex.Message}");
        }

        JArray? items = null;

        // Ensure the API key is loaded (guards against the App startup race condition)
        if (!AzureOpenAIService.HasApiKey)
            await AzureOpenAIService.LoadApiKeyFromStorageAsync();

        // ── Primary: Overpass OSM API (real data from OpenStreetMap) ─────────────
        {
            var helper = new ServiceCenterDataHelper();
            var data   = await helper.GetNearbyPlacesAsync(latitude, longitude, 10, 8, _nearbyMapMode);
            items      = data?["markercollections"] as JArray;
        }

        // ── Fallback 1: Azure OpenAI (if Overpass returned nothing) ──────────────
        if (items == null || items.Count == 0)
        {
            if (AzureOpenAIService.HasApiKey)
            {
                var (aiItems, _) = await GetNearbyPlacesFromAIAsync(latitude, longitude);
                items = aiItems;
            }
        }

        // ── Fallback 2: Static sample data (when both Overpass and AI unavailable) ─
        if (items == null || items.Count == 0)
        {
            var staticData = ServiceCenterDataHelper.GetStaticFallback(latitude, longitude, _nearbyMapMode, 8);
            items = staticData["markercollections"] as JArray;
        }

        if (items == null || items.Count == 0)
        {
            IsNearbyEmpty = true;
            return;
        }

        PopulateNearbyFromResults(items, latitude, longitude);
    }

    // Builds a prompt following the blog pattern and calls Azure OpenAI.
    // Returns (parsed items, raw response text) so the caller can display the full response.
    private async Task<(JArray? Items, string RawResponse)> GetNearbyPlacesFromAIAsync(double latitude, double longitude)
    {
        var isServiceMode = string.Equals(_nearbyMapMode, "Service", StringComparison.OrdinalIgnoreCase);
        var modeLabel     = isServiceMode
            ? "auto service centers, car repair workshops, and vehicle repair shops"
            : "fuel stations, petrol pumps, and gas stations";

        // Prompt asks for realistic places including open hours; coordinates scatter ±0.08°.
        var prompt = $$"""
            Location: latitude {{latitude:F6}}, longitude {{longitude:F6}}.

            Return a JSON array of 8 to 10 {{modeLabel}} near this location.
            Use your regional knowledge. If exact names are unknown, suggest realistic names typical for this area.

            Each item must include these exact fields:
            - "Name": the business name (never "Error")
            - "OpenTime": typical operating hours, e.g. "Open 24 Hrs", "6:00 AM – 10:00 PM", "Mon–Sat 8:00 AM – 7:00 PM"
            - "Address": street name, area name, or nearest landmark
            - "Latitude": decimal value within ±0.08° of {{latitude:F6}}
            - "Longitude": decimal value within ±0.08° of {{longitude:F6}}

            Rules:
            - Always return 8–10 items. Never return an empty array.
            - Spread the coordinates realistically; do not cluster all pins on the same spot.
            - Return ONLY the raw JSON array — no markdown fences, no explanations.
            - On a new line after the array write exactly: Zoom: 13
            """;

        try
        {
            var ai       = new AzureOpenAIService();
            var response = await ai.GetResultsFromAI(prompt,
                "You are a location data assistant. Return only valid JSON with no markdown or explanation.");

            if (string.IsNullOrWhiteSpace(response))
            {
                var detail = !string.IsNullOrWhiteSpace(ai.LastError)
                    ? $"API error: {ai.LastError}"
                    : "(empty response — no error detail)";
                if (!string.IsNullOrWhiteSpace(ai.LastRawResponse))
                    detail += $"\n\nRaw API body:\n{ai.LastRawResponse[..Math.Min(600, ai.LastRawResponse.Length)]}";
                return (null, detail);
            }

            // Strip code fences (same as Flutter sample)
            var clean = response
                .Replace("```json", string.Empty)
                .Replace("```", string.Empty)
                .Trim();

            // Extract and remove the Zoom hint (may be on its own line OR embedded in the array)
            var zoomMatch = System.Text.RegularExpressions.Regex.Match(clean, @"[\{,]?\s*""?[Zz]oom""?\s*[:]\s*(\d+)\s*\}?");
            if (!zoomMatch.Success)
                zoomMatch = System.Text.RegularExpressions.Regex.Match(clean, @"Zoom:\s*(\d+)");
            if (zoomMatch.Success)
            {
                clean = clean.Replace(zoomMatch.Value, string.Empty).Trim().TrimEnd(',').Trim();
                if (double.TryParse(zoomMatch.Groups[1].Value, out var zoom))
                    UserMapZoom = Math.Clamp(zoom, 12, 16);
            }

            // Parse — expect raw array; fall back to object with any common wrapper key
            JArray? parsed = null;
            try   { parsed = JArray.Parse(clean); }
            catch
            {
                try
                {
                    var obj = JObject.Parse(clean);
                    parsed  = (obj["markercollections"]
                            ?? obj["places"] ?? obj["locations"]
                            ?? obj["results"] ?? obj["stations"]
                            ?? obj["centers"] ?? obj["data"]) as JArray;
                }
                catch (Exception parseEx)
                {
                    return (null, $"(JSON parse error: {parseEx.Message})\n\nRaw response:\n{response}");
                }
            }

            // Filter out non-place items the model may have embedded (Zoom objects, error objects)
            if (parsed != null)
            {
                var valid = new JArray(parsed
                    .OfType<JObject>()
                    .Where(o =>
                        o["Latitude"] != null && o["Longitude"] != null  // must have coords
                        && o["Name"] != null                              // must have a name
                        && !string.Equals(o["Name"]?.ToString(), "Error", StringComparison.OrdinalIgnoreCase)
                        && o["Zoom"] == null));                           // discard stray Zoom objects
                parsed = valid;
            }

            return (parsed, response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NearbyNetwork-AI] {ex.Message}");
            return (null, $"(AI request error: {ex.Message})");
        }
    }

    private void PopulateNearbyFromResults(JArray items, double userLat, double userLon)
    {
        var isServiceMode = string.Equals(_nearbyMapMode, "Service", StringComparison.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var name    = item["Name"]?.ToString()    ?? "Nearby location";
            var details = item["Details"]?.ToString() ?? string.Empty;
            var address = item["Address"]?.ToString() ?? string.Empty;
            var type    = item["Type"]?.ToString()    ?? (isServiceMode ? "Service Center" : "Fuel Station");

            var markerLat = item["Latitude"]?.Value<double?>()  ?? userLat;
            var markerLon = item["Longitude"]?.Value<double?>() ?? userLon;

            // Compute distance if not already in the item
            var distanceKm = item["DistanceKm"]?.Value<double?>()
                          ?? HaversineKm(userLat, userLon, markerLat, markerLon);

            var isFuelItem = string.Equals(type, "Fuel Station", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(type, "fuel", StringComparison.OrdinalIgnoreCase);

            var openTime = item["OpenTime"]?.ToString()
                        ?? (isFuelItem ? "Open 24 Hrs" : "Mon–Sat: 8:00 AM – 7:00 PM");

            // Map marker (pin on the tile layer)
            MapMarkers.Add(new CustomMarker
            {
                Latitude  = markerLat,
                Longitude = markerLon,
                Name      = name,
                Details   = string.IsNullOrWhiteSpace(details) ? (isFuelItem ? "Fuel station" : "Service center") : details,
                Address   = string.IsNullOrWhiteSpace(address) ? null : address,
            });

            // List card below the map
            var distanceLabel = distanceKm > 0 ? $" • {distanceKm:0.0} km" : string.Empty;
            var subLabel      = string.IsNullOrWhiteSpace(address) ? $"{type}{distanceLabel}" : $"{address}{distanceLabel}";

            NearbyNetworkItems.Add(new NearbyNetworkItem
            {
                Name          = name,
                TypeLabel     = subLabel,
                OpenTime      = "🕐 " + openTime,
                IconText      = isFuelItem ? "⛽" : "🔧",
                IconBgColor   = Color.FromArgb("#0D2135"),
                IconTextColor = isFuelItem ? Color.FromArgb("#FCD34D") : Color.FromArgb("#8ED3FF")
            });
        }
    }

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private void InitSchedule()
    {
        // No static data — schedule is fully driven by user-added reminders.
        // Alerts and appointments are built dynamically via AddAppointmentFromReminder / RebuildScheduleAlerts.
    }

    private void AddAppointmentFromReminder(ScheduleReminder reminder)
    {
        VehicleDataService.Instance.AddReminder(SelectedVehicle?.Id ?? 0, reminder);
        var startTime = reminder.DueDate.Add(reminder.DueTime);
        var endTime   = startTime.AddHours(1);

        var color = reminder.Priority switch
        {
            "Critical" => Color.FromArgb("#DC2626"),
            "High"     => Color.FromArgb("#D97706"),
            "Medium"   => Color.FromArgb("#3B5BDB"),
            _          => Color.FromArgb("#22C55E"),
        };

        ServiceAppointments.Add(new SchedulerAppointment
        {
            StartTime  = startTime,
            EndTime    = endTime,
            Subject    = reminder.Title,
            Notes      = $"{reminder.ReminderType} \u2022 {reminder.Priority} priority",
            Background = new SolidColorBrush(color),
            TextColor  = Colors.White,
        });

        // Refresh predictive fields after a new appointment is added
        OnPropertyChanged(nameof(PredictiveServiceDate));
        OnPropertyChanged(nameof(PredictiveServiceDescription));
        OnPropertyChanged(nameof(PredictiveMileage));
    }

    private void RebuildScheduleAlerts()
    {
        ScheduleAlerts.Clear();
        UpcomingTasks.Clear();
        Reminders.Clear();

        var vehicleId = SelectedVehicle?.Id ?? 0;
        var reminders = VehicleDataService.Instance.GetReminders(vehicleId)
            .OrderBy(r => r.DueDate.Date)
            .ThenBy(r => r.DueTime)
            .ToList();

        var now = DateTime.Now;
        var soon = now.AddDays(14);

        foreach (var reminder in reminders)
        {
            var startTime = reminder.DueDate.Add(reminder.DueTime);
            var isOverdue = startTime < now;
            var isDueSoon = !isOverdue && startTime <= soon;
            var badge = isOverdue ? "OVERDUE" : isDueSoon ? "DUE SOON" : "UPCOMING";
            var badgeColor = isOverdue ? Color.FromArgb("#DC2626") : isDueSoon ? Color.FromArgb("#D97706") : Color.FromArgb("#3B5BDB");
            var badgeBg = isOverdue ? Color.FromArgb("#FEE2E2") : isDueSoon ? Color.FromArgb("#FEF3C7") : Color.FromArgb("#DBEAFE");
            var leftAccent = isOverdue ? Color.FromArgb("#DC2626") : isDueSoon ? Color.FromArgb("#D97706") : Color.FromArgb("#3B5BDB");
            var actionText = isOverdue ? "Take Action →" : "View Details →";
            var description = isOverdue
                ? $"Missed by {Math.Abs((int)(now - startTime).TotalDays)} day(s)."
                : isDueSoon
                    ? $"Due in {(int)(startTime - now).TotalDays + 1} day(s)."
                    : $"Scheduled for {startTime:dd MMM yyyy} at {startTime:hh:mm tt}.";

            ScheduleAlerts.Add(new ScheduleAlert
            {
                Badge           = badge,
                BadgeColor      = badgeColor,
                BadgeBgColor    = badgeBg,
                LeftAccentColor = leftAccent,
                Date            = startTime.ToString("MMM dd"),
                Title           = reminder.Title,
                Description     = $"{reminder.ReminderType} • {description}",
                ActionText      = actionText,
                ActionColor     = badgeColor,
            });

            if (!isOverdue && startTime <= soon)
            {
                var daysLeft = (int)(startTime - now).TotalDays + 1;
                UpcomingTasks.Add(new UpcomingTask
                {
                    Title        = $"{reminder.Title} ({daysLeft} Days)",
                    Badge        = isDueSoon ? "DUE" : "PLAN",
                    BadgeColor   = badgeColor,
                    BadgeBgColor = badgeBg,
                    Description  = $"{reminder.ReminderType} • {reminder.Priority} priority",
                    IconText     = "🛠",
                    IconBgColor  = Color.FromArgb("#EBF5FF"),
                });
            }

            Reminders.Add(new ReminderItem
            {
                Title = reminder.Title,
                Subtitle = $"{reminder.ReminderType} • {reminder.Priority} • {startTime:dd MMM yyyy}",
                AccentColor = leftAccent,
                ActionIconText = "→"
            });
        }

        OnPropertyChanged(nameof(HasScheduleAlerts));
        OnPropertyChanged(nameof(NoScheduleAlerts));
    }

    public bool HasScheduleAlerts => ScheduleAlerts.Count > 0;
    public bool NoScheduleAlerts  => ScheduleAlerts.Count == 0;

    // Reloads all vehicle-specific data when the selected vehicle changes
    private void RefreshVehicleData()
    {
        var vid = SelectedVehicle?.Id ?? 0;

        // Service history
        _serviceByVehicle[vid] = VehicleDataService.Instance.GetServiceRecords(vid)
            .OrderByDescending(r => r.ServiceDate).ToList();
        ServiceHistory.Clear();
        foreach (var r in _serviceByVehicle[vid]) ServiceHistory.Add(r);
        RebuildExpenseHistory();
        RefreshVisibleCollections();

        // Fuel entries
        _fuelByVehicle[vid] = VehicleDataService.Instance.GetFuelEntries(vid)
            .OrderByDescending(f => f.FuelDate).ToList();
        FuelEntries.Clear();
        foreach (var f in _fuelByVehicle[vid]) FuelEntries.Add(f);
        RefreshVisibleCollections();

        // Schedule from reminders
        ServiceAppointments.Clear();
        foreach (var reminder in VehicleDataService.Instance.GetReminders(vid))
        {
            var startTime = reminder.DueDate.Add(reminder.DueTime);
            var endTime = startTime.AddHours(1);
            var color = reminder.Priority switch
            {
                "Critical" => Color.FromArgb("#DC2626"),
                "High" => Color.FromArgb("#D97706"),
                "Medium" => Color.FromArgb("#3B5BDB"),
                _ => Color.FromArgb("#22C55E")
            };

            ServiceAppointments.Add(new SchedulerAppointment
            {
                StartTime = startTime,
                EndTime = endTime,
                Subject = reminder.Title,
                Notes = $"{reminder.ReminderType} • {reminder.Priority} priority",
                Background = new SolidColorBrush(color),
                TextColor = Colors.White,
            });
        }

        RebuildScheduleAlerts();

        OnPropertyChanged(nameof(SelectedVehicleName));
        OnPropertyChanged(nameof(SelectedVehicleRegistration));
        OnPropertyChanged(nameof(TotalServices));
        OnPropertyChanged(nameof(LastServiceText));
        OnPropertyChanged(nameof(LifetimeSpend));
        OnPropertyChanged(nameof(NextServiceDays));
        OnPropertyChanged(nameof(NextServiceDetail));
        OnPropertyChanged(nameof(PredictiveServiceDate));
        OnPropertyChanged(nameof(PredictiveServiceDescription));
        OnPropertyChanged(nameof(PredictiveMileage));

        _ = RefreshSpendingOptimizationAsync();
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    private static void ComputeStatusColors(string status, out Color color, out Color bgColor)
    {
        switch (status)
        {
            case "Pending":
                color   = Color.FromArgb("#D97706");
                bgColor = Color.FromArgb("#FEF3C7");
                break;
            case "Scheduled":
                color   = Color.FromArgb("#3B5BDB");
                bgColor = Color.FromArgb("#EEF2FF");
                break;
            default:
                color   = Color.FromArgb("#16A34A");
                bgColor = Color.FromArgb("#DCFCE7");
                break;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T store, T value, [CallerMemberName] string prop = "")
    {
        if (EqualityComparer<T>.Default.Equals(store, value)) return false;
        store = value;
        OnPropertyChanged(prop);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string prop = "")
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
