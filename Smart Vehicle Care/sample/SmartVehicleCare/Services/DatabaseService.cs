using SmartVehicleCare.Models;
using System.Text.Json;
using System.Diagnostics;

namespace SmartVehicleCare.Services;

public class DatabaseService
{
    private static readonly string VehiclesKey = "Vehicles";
    private static readonly string ActiveVehicleKey = "ActiveVehicle";

    /// <summary>
    /// Saves a vehicle to local storage
    /// </summary>
    public static void SaveVehicle(Vehicle vehicle)
    {
        try
        {
            var vehicles = GetAllVehicles();
            
            // If vehicle has no ID, assign one
            if (vehicle.Id == 0)
            {
                vehicle.Id = vehicles.Count > 0 ? vehicles.Max(v => v.Id) + 1 : 1;
            }

            // Add or update vehicle
            var existingVehicle = vehicles.FirstOrDefault(v => v.Id == vehicle.Id);
            if (existingVehicle != null)
            {
                vehicles.Remove(existingVehicle);
            }

            vehicles.Add(vehicle);

            // Save to preferences as JSON
            var json = JsonSerializer.Serialize(vehicles);
            Preferences.Default.Set(VehiclesKey, json);

            // Set as active vehicle
            Preferences.Default.Set(ActiveVehicleKey, vehicle.Id.ToString());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving vehicle: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all saved vehicles
    /// </summary>
    public static List<Vehicle> GetAllVehicles()
    {
        try
        {
            var json = Preferences.Default.Get(VehiclesKey, "[]");
            return JsonSerializer.Deserialize<List<Vehicle>>(json) ?? new();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading vehicles: {ex.Message}");
            return new();
        }
    }

    /// <summary>
    /// Gets a vehicle by ID
    /// </summary>
    public static Vehicle? GetVehicle(int id)
    {
        var vehicles = GetAllVehicles();
        return vehicles.FirstOrDefault(v => v.Id == id);
    }

    /// <summary>
    /// Gets the active/current vehicle
    /// </summary>
    public static Vehicle? GetActiveVehicle()
    {
        try
        {
            var activeId = Preferences.Default.Get(ActiveVehicleKey, "");
            if (int.TryParse(activeId, out var id))
            {
                return GetVehicle(id);
            }

            // If no active vehicle, return first one
            var vehicles = GetAllVehicles();
            if (vehicles.Count > 0)
            {
                return vehicles[0];
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting active vehicle: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Sets the active vehicle
    /// </summary>
    public static void SetActiveVehicle(int vehicleId)
    {
        Preferences.Default.Set(ActiveVehicleKey, vehicleId.ToString());
    }

    /// <summary>
    /// Deletes a vehicle
    /// </summary>
    public static void DeleteVehicle(int id)
    {
        try
        {
            var vehicles = GetAllVehicles();
            vehicles.RemoveAll(v => v.Id == id);

            var json = JsonSerializer.Serialize(vehicles);
            Preferences.Default.Set(VehiclesKey, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error deleting vehicle: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if any vehicles exist
    /// </summary>
    public static bool HasVehicles()
    {
        return GetAllVehicles().Count > 0;
    }
}
