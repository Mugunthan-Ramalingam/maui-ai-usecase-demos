using System.Collections.ObjectModel;
using SmartVehicleCare.Models;

namespace SmartVehicleCare.Services;

/// <summary>
/// Manages vehicle data with separate storage for sample (demo) and real user data.
/// Prevents mixing of demo and real data to maintain data integrity.
/// </summary>
public class VehicleDataService
{
    private static VehicleDataService? _instance;
    public static VehicleDataService Instance => _instance ??= new VehicleDataService();

    /// <summary>
    /// Indicates the current mode: Demo (sample data) or Real (user data)
    /// </summary>
    public enum DataMode { Demo, Real }

    private DataMode _currentMode = DataMode.Real;
    private bool _hasRealDataBeenAdded = false;
    private bool _hasDemoDataBeenLoaded = false;

    private VehicleDataService() { }

    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    
    /// <summary>
    /// Gets the current data mode (Demo or Real)
    /// </summary>
    public DataMode CurrentMode => _currentMode;
    
    /// <summary>
    /// Returns true if currently in Demo mode
    /// </summary>
    public bool IsDemoMode => _currentMode == DataMode.Demo;
    
    /// <summary>
    /// Returns true if currently in Real mode
    /// </summary>
    public bool IsRealMode => _currentMode == DataMode.Real;
    
    /// <summary>
    /// Returns true if real user data has been added (blocks demo mode)
    /// </summary>
    public bool HasRealDataBeenAdded => _hasRealDataBeenAdded;

    private Vehicle? _selectedVehicle;
    public Vehicle? SelectedVehicle
    {
        get => _selectedVehicle;
        set
        {
            if (_selectedVehicle == value) return;
            _selectedVehicle = value;
            SelectedVehicleChanged?.Invoke(value);
        }
    }

    public event Action<Vehicle?>? SelectedVehicleChanged;
    public event Action<DataMode>? ModeChanged;
    // Fired when any per-vehicle data changes (service/fuel/doc/reminder added)
    public event Action<int>? DataChanged;
    public void NotifyDataChanged(int vehicleId) => DataChanged?.Invoke(vehicleId);

    public void ClearAllData()
    {
        Vehicles.Clear();
        _selectedVehicle = null;
        _vehicleServices.Clear();
        _vehicleFuel.Clear();
        _vehicleReminders.Clear();
    }

    public void FullReset()
    {
        ClearAllData();
        _hasRealDataBeenAdded = false;
        _hasDemoDataBeenLoaded = false;
        _currentMode = DataMode.Real;
        ModeChanged?.Invoke(_currentMode);
    }

    /// <summary>
    /// Switches to Demo mode and loads sample data.
    /// Clears all existing data to prevent mixing.
    /// Only allowed if no real user data has been added.
    /// </summary>
    public bool TryLoadDemoData()
    {
        // Don't allow switching to demo if user has already added real data
        if (_hasRealDataBeenAdded)
        {
            System.Diagnostics.Debug.WriteLine("[VehicleDataService] Cannot load demo data - real user data exists");
            return false;
        }

        ClearAllData();
        _currentMode = DataMode.Demo;
        _hasDemoDataBeenLoaded = true;
        ModeChanged?.Invoke(_currentMode);
        
        System.Diagnostics.Debug.WriteLine("[VehicleDataService] Switched to Demo mode");
        return true;
    }

    /// <summary>
    /// Switches to Real mode with real user data.
    /// If demo data is active, it will be cleared first.
    /// Once real data is added, demo mode is locked.
    /// </summary>
    public void SwitchToRealData()
    {
        // If we were in demo mode, clear it before switching to real
        if (_currentMode == DataMode.Demo)
        {
            ClearAllData();
            System.Diagnostics.Debug.WriteLine("[VehicleDataService] Cleared demo data, switching to Real mode");
        }

        _currentMode = DataMode.Real;
        _hasRealDataBeenAdded = true;  // Lock real mode - prevents demo from loading again
        ModeChanged?.Invoke(_currentMode);
        
        System.Diagnostics.Debug.WriteLine("[VehicleDataService] Switched to Real mode (locked)");
    }

    public void AddVehicle(Vehicle vehicle)
    {
        if (vehicle == null) return;

        if (vehicle.Id <= 0)
            vehicle.Id = Vehicles.Count == 0 ? 1 : Vehicles.Max(v => v.Id) + 1;
        else if (Vehicles.Any(v => v.Id == vehicle.Id))
            vehicle.Id = Vehicles.Count == 0 ? 1 : Vehicles.Max(v => v.Id) + 1;

        Vehicles.Add(vehicle);
        SelectedVehicle ??= vehicle;
        
        // If we're in real mode, mark that real data has been added
        if (_currentMode == DataMode.Real)
        {
            _hasRealDataBeenAdded = true;
        }
    }

    /// <summary>Persist in-place edits to a vehicle already in the collection and notify listeners.</summary>
    public void UpdateVehicle(Vehicle vehicle)
    {
        if (vehicle == null) return;
        DataChanged?.Invoke(vehicle.Id);
    }

    // ── Per-vehicle service records ───────────────────────────────────────────

    private readonly Dictionary<int, List<ServiceRecord>> _vehicleServices = new();

    public void AddServiceRecord(int vehicleId, ServiceRecord record)
    {
        if (!_vehicleServices.ContainsKey(vehicleId)) _vehicleServices[vehicleId] = new();
        _vehicleServices[vehicleId].Insert(0, record);
        DataChanged?.Invoke(vehicleId);
    }

    public IReadOnlyList<ServiceRecord> GetServiceRecords(int vehicleId)
        => _vehicleServices.TryGetValue(vehicleId, out var r) ? r.AsReadOnly() : Array.Empty<ServiceRecord>();

    // ── Per-vehicle fuel entries ──────────────────────────────────────────────

    private readonly Dictionary<int, List<FuelEntry>> _vehicleFuel = new();

    public void AddFuelEntry(int vehicleId, FuelEntry entry)
    {
        if (!_vehicleFuel.ContainsKey(vehicleId)) _vehicleFuel[vehicleId] = new();
        _vehicleFuel[vehicleId].Insert(0, entry);
        DataChanged?.Invoke(vehicleId);
    }

    public IReadOnlyList<FuelEntry> GetFuelEntries(int vehicleId)
        => _vehicleFuel.TryGetValue(vehicleId, out var f) ? f.AsReadOnly() : Array.Empty<FuelEntry>();

    public void RemoveServiceRecord(int vehicleId, ServiceRecord record)
    {
        if (_vehicleServices.TryGetValue(vehicleId, out var list))
            list.Remove(record);
        DataChanged?.Invoke(vehicleId);
    }

    public void RemoveFuelEntry(int vehicleId, FuelEntry entry)
    {
        if (_vehicleFuel.TryGetValue(vehicleId, out var list))
            list.Remove(entry);
        DataChanged?.Invoke(vehicleId);
    }

    // ── Per-vehicle schedule reminders ────────────────────────────────────────

    private readonly Dictionary<int, List<ScheduleReminder>> _vehicleReminders = new();

    public void AddReminder(int vehicleId, ScheduleReminder reminder)
    {
        if (!_vehicleReminders.ContainsKey(vehicleId)) _vehicleReminders[vehicleId] = new();
        _vehicleReminders[vehicleId].Insert(0, reminder);
        DataChanged?.Invoke(vehicleId);
    }

    public IReadOnlyList<ScheduleReminder> GetReminders(int vehicleId)
        => _vehicleReminders.TryGetValue(vehicleId, out var r) ? r.AsReadOnly() : Array.Empty<ScheduleReminder>();
}
