using SmartVehicleCare.Models;
using SmartVehicleCare.Services;
using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class WelcomePage : ContentPage
{
    private WelcomeViewModel? _vm;

    public WelcomePage()
    {
        InitializeComponent();
        _vm = new WelcomeViewModel();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm?.ResetWelcomeFlow();
    }

    private async void OnWizardNextClicked(object? sender, EventArgs e)
    {
        if (_vm == null)
            return;

        _vm.NextStepCommand.Execute(null);

        if (_vm.CurrentStep == 3)
        {
            SaveVehicleToService();
            await AutoRedirectToDashboard();
        }
    }

    private void SaveVehicleToService()
    {
        if (_vm == null) return;

        // ✅ Switch to Real data mode (if in demo, this clears demo data first)
        VehicleDataService.Instance.SwitchToRealData();

        var vehicle = new Vehicle
        {
            VehicleType       = _vm.SelectedVehicleTypeIndex,
            Make              = _vm.SelectedMake,
            Model             = _vm.VehicleModel,
            Variant           = _vm.Variant,
            OdometerReading   = _vm.OdaMeterReading,
            CreatedDate       = DateTime.Now,
            IsActive          = true
        };
        VehicleDataService.Instance.AddVehicle(vehicle);

        
        System.Diagnostics.Debug.WriteLine($"[WelcomePage] Real vehicle added: {vehicle.Make} {vehicle.Model}");
    }

    private async Task AutoRedirectToDashboard()
    {
        await Task.Delay(1500);
        await Shell.Current.GoToAsync("///main");
    }

    private async void OnExploreWithSampleDataClicked(object? sender, EventArgs e)
    {
        // ✅ Try to switch to Demo mode (fails if real data already exists)
        if (!VehicleDataService.Instance.TryLoadDemoData())
        {
            // If failed (real data exists), show message
            await DisplayAlertAsync(
                "Demo Mode Unavailable",
                "You've already set up your vehicle profile. Real data and sample data cannot be mixed.",
                "OK");
            return;
        }

        LoadSampleVehicleData();
        await Shell.Current.GoToAsync("///main");
    }

    private void LoadSampleVehicleData()
    {
        var car = new Vehicle
        {
            Id = 1,
            VehicleType = 0,
            Make = "Hyundai",
            Model = "i20",
            Variant = "Sportz",
            OdometerReading = "45230",
            ServiceInterval = "Every 10,000 km",
            CreatedDate = DateTime.Now,
            IsActive = true
        };

        var bike = new Vehicle
        {
            Id = 2,
            VehicleType = 1,
            Make = "Honda",
            Model = "Activa",
            Variant = "125",
            OdometerReading = "18950",
            ServiceInterval = "Every 5,000 km",
            CreatedDate = DateTime.Now,
            IsActive = true
        };

        VehicleDataService.Instance.AddVehicle(car);
        VehicleDataService.Instance.AddVehicle(bike);
        VehicleDataService.Instance.SelectedVehicle = car;

        System.Diagnostics.Debug.WriteLine("[WelcomePage] Demo mode loaded: 2 sample vehicles added");

        VehicleDataService.Instance.AddServiceRecord(car.Id, new ServiceRecord
        {
            ServiceType = "Oil & Filter Change",
            ServiceDate = DateTime.Today.AddDays(-24),
            Workshop = "Hyundai Authorized Service",
            Amount = "₹3200",
            Mileage = "45200 km",
            Status = "Completed",
            Notes = "Routine service with filter change."
        });

        VehicleDataService.Instance.AddServiceRecord(car.Id, new ServiceRecord
        {
            ServiceType = "Brake Inspection",
            ServiceDate = DateTime.Today.AddDays(-61),
            Workshop = "City Auto Works",
            Amount = "₹1850",
            Mileage = "44600 km",
            Status = "Completed",
            Notes = "Brake pads checked; wear within limit."
        });

        VehicleDataService.Instance.AddServiceRecord(car.Id, new ServiceRecord
        {
            ServiceType = "Tyre Rotation",
            ServiceDate = DateTime.Today.AddDays(-97),
            Workshop = "WheelCare Center",
            Amount = "₹1600",
            Mileage = "44120 km",
            Status = "Completed",
            Notes = "Rotated tyres and checked alignment."
        });

        VehicleDataService.Instance.AddServiceRecord(car.Id, new ServiceRecord
        {
            ServiceType = "Battery Health Check",
            ServiceDate = DateTime.Today.AddDays(-150),
            Workshop = "AutoCare Plus",
            Amount = "₹1300",
            Mileage = "43680 km",
            Status = "Completed",
            Notes = "Battery voltage checked and terminals cleaned."
        });

        VehicleDataService.Instance.AddServiceRecord(car.Id, new ServiceRecord
        {
            ServiceType = "Cabin Filter Replacement",
            ServiceDate = DateTime.Today.AddDays(-260),
            Workshop = "City Auto Works",
            Amount = "₹950",
            Mileage = "43210 km",
            Status = "Completed",
            Notes = "Air filter replaced for cabin airflow improvement."
        });

        VehicleDataService.Instance.AddFuelEntry(car.Id, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-7),
            FuelType = "Petrol",
            Station = "Shell Rajaji Nagar",
            LitresFilled = 30,
            CostPerLitre = 102.40,
            OdometerReading = 45230,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddFuelEntry(car.Id, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-19),
            FuelType = "Petrol",
            Station = "HP Fuel Point",
            LitresFilled = 28,
            CostPerLitre = 101.80,
            OdometerReading = 44910,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddFuelEntry(car.Id, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-41),
            FuelType = "Petrol",
            Station = "Reliance Fuel",
            LitresFilled = 32,
            CostPerLitre = 100.90,
            OdometerReading = 44540,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddFuelEntry(car.Id, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-68),
            FuelType = "Petrol",
            Station = "Indian Oil - Purasawalkam",
            LitresFilled = 29,
            CostPerLitre = 103.60,
            OdometerReading = 44210,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddFuelEntry(car.Id, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-112),
            FuelType = "Petrol",
            Station = "Bharat Petroleum - Koyambedu",
            LitresFilled = 31,
            CostPerLitre = 102.90,
            OdometerReading = 43875,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddReminder(car.Id, new ScheduleReminder
        {
            Title = "Oil Change",
            ReminderType = "Service",
            DueDate = DateTime.Today.AddDays(15),
            Priority = "High"
        });

        VehicleDataService.Instance.AddReminder(car.Id, new ScheduleReminder
        {
            Title = "Tyre Pressure Check",
            ReminderType = "Inspection",
            DueDate = DateTime.Today.AddDays(20),
            Priority = "High"
        });

        VehicleDataService.Instance.AddReminder(car.Id, new ScheduleReminder
        {
            Title = "Brake Check",
            ReminderType = "Inspection",
            DueDate = DateTime.Today.AddDays(32),
            Priority = "Medium"
        });

        VehicleDataService.Instance.AddReminder(car.Id, new ScheduleReminder
        {
            Title = "Wheel Alignment Check",
            ReminderType = "Inspection",
            DueDate = DateTime.Today.AddDays(18),
            Priority = "High"
        });

        VehicleDataService.Instance.AddReminder(car.Id, new ScheduleReminder
        {
            Title = "General Service Due",
            ReminderType = "Service",
            DueDate = DateTime.Today.AddDays(45),
            Priority = "Medium"
        });

        VehicleDataService.Instance.AddServiceRecord(bike.Id, new ServiceRecord
        {
            ServiceType = "General Service",
            ServiceDate = DateTime.Today.AddDays(-95),
            Workshop = "Two Wheeler Care",
            Amount = "₹1800",
            Mileage = "18650 km",
            Status = "Completed",
            Notes = "Chain, spark plug, and engine check completed."
        });

        VehicleDataService.Instance.AddFuelEntry(bike.Id, new FuelEntry
        {
            FuelDate = DateTime.Today.AddDays(-5),
            FuelType = "Petrol",
            Station = "Bharath Fuel Hub",
            LitresFilled = 6.2,
            CostPerLitre = 106.20,
            OdometerReading = 18950,
            IsFullTank = true
        });

        VehicleDataService.Instance.AddReminder(bike.Id, new ScheduleReminder
        {
            Title = "Service Reminder",
            ReminderType = "Service",
            DueDate = DateTime.Today.AddDays(24),
            Priority = "Medium"
        });

        // Publish one final state after the complete sample dataset is loaded.
        VehicleDataService.Instance.SelectedVehicle = car;
        VehicleDataService.Instance.NotifyDataReloaded();
    }


   

    private void OnPhoneTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry || e.NewTextValue == null) return;
        var cleaned = new string(e.NewTextValue.Where(char.IsDigit).ToArray());
        if (cleaned != e.NewTextValue)
            entry.Text = cleaned;
    }

    private void OnOtpTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry || e.NewTextValue == null) return;
        var cleaned = new string(e.NewTextValue.Where(char.IsDigit).ToArray());
        if (cleaned != e.NewTextValue)
            entry.Text = cleaned;
    }

    private void OnNumericTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not Entry entry || e.NewTextValue == null) return;
        var cleaned = new string(e.NewTextValue.Where(char.IsDigit).ToArray());
        if (cleaned != e.NewTextValue)
            entry.Text = cleaned;
    }
}
