using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SmartVehicleCare.Models;

namespace SmartVehicleCare.ViewModels;

public class WelcomeViewModel : INotifyPropertyChanged
{
    #region Wizard Navigation

    private int _currentStep = 0;

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsWizardVisible));
                OnPropertyChanged(nameof(IsLandingVisible));

                OnPropertyChanged(nameof(IsStep1Visible));
                OnPropertyChanged(nameof(IsStep2Visible));
                OnPropertyChanged(nameof(IsStep3Visible));

                OnPropertyChanged(nameof(StepText));
                OnPropertyChanged(nameof(IsBackEnabled));
                OnPropertyChanged(nameof(NextButtonText));

                OnPropertyChanged(nameof(Dot1Active));
                OnPropertyChanged(nameof(Dot2Active));
                OnPropertyChanged(nameof(Dot3Active));

                OnPropertyChanged(nameof(CanProceedToNext));
                OnPropertyChanged(nameof(IsStep1CloseVisible));
            }
        }
    }

    public bool IsWizardVisible => CurrentStep > 0;
    public bool IsLandingVisible => CurrentStep == 0 && !_isLoginPhoneVisible && !_isLoginOtpVisible;

    public bool IsStep1Visible => CurrentStep == 1;
    public bool IsStep2Visible => CurrentStep == 2;
    public bool IsStep3Visible => CurrentStep == 3;

    public bool IsBackEnabled => CurrentStep > 1 && CurrentStep < 3;

    public string StepText =>
        CurrentStep > 0
            ? $"Step {CurrentStep} of 3"
            : string.Empty;

    public string NextButtonText => CurrentStep switch
    {
        1 => "Vehicle Details →",
        2 => "Finish Setup →",
        _ => "Continue →"
    };

    public bool Dot1Active => CurrentStep >= 1;
    public bool Dot2Active => CurrentStep >= 2;
    public bool Dot3Active => CurrentStep >= 3;

    #endregion

    #region Step 3 — Documents

  

    #endregion

    #region Step 1 - Vehicle Type

    private int _selectedVehicleTypeIndex = -1;

    public int SelectedVehicleTypeIndex
    {
        get => _selectedVehicleTypeIndex;
        set
        {
            if (SetProperty(ref _selectedVehicleTypeIndex, value))
            {
                OnPropertyChanged(nameof(IsCarSelected));
                OnPropertyChanged(nameof(IsMotoSelected));
                OnPropertyChanged(nameof(MakeHint));
                OnPropertyChanged(nameof(ModelHint));
                OnPropertyChanged(nameof(VariantHint));

                OnPropertyChanged(nameof(IsStep1Valid));
                OnPropertyChanged(nameof(CanProceedToNext));
            }
        }
    }

    public bool IsCarSelected => SelectedVehicleTypeIndex == 0;
    public bool IsMotoSelected => SelectedVehicleTypeIndex == 1;
    public bool IsTruckSelected => SelectedVehicleTypeIndex == 2;
    public bool IsEvSelected => SelectedVehicleTypeIndex == 3;

    public string MakeHint => IsMotoSelected ? "e.g. Honda, Yamaha, Royal Enfield" : "e.g. Toyota, Honda, BMW";
    public string ModelHint => IsMotoSelected ? "e.g. CBR600, FZ25, Thunderbird" : "e.g. Camry, Civic, Swift";
    public string VariantHint => IsMotoSelected ? "e.g. Standard, Sport, Cruiser" : "e.g. ZXI, VXI";

    #endregion

    #region Step 2 - Vehicle Details

    private string _selectedMake = string.Empty;

    public string SelectedMake
    {
        get => _selectedMake;
        set
        {
            if (SetProperty(ref _selectedMake, value))
            {
                NotifyStep2Validation();
            }
        }
    }

    private string _vehicleModel = string.Empty;

    public string VehicleModel
    {
        get => _vehicleModel;
        set
        {
            if (SetProperty(ref _vehicleModel, value))
            {
                NotifyStep2Validation();
            }
        }
    }

    private string _variant = string.Empty;

    public string Variant
    {
        get => _variant;
        set => SetProperty(ref _variant, value);
    }

    private string _odaMeterReading = string.Empty;

    public string OdaMeterReading
    {
        get => _odaMeterReading;
        set
        {
            if (SetProperty(ref _odaMeterReading, value))
            {
                NotifyStep2Validation();
            }
        }
    }

    #endregion

    #region Validation

    public bool IsStep1Valid =>
        SelectedVehicleTypeIndex >= 0;

    public bool IsStep1CloseVisible => CurrentStep == 1;

    public bool IsOdaMeterReadingValid =>
        int.TryParse(OdaMeterReading, out int odaMeterReading) &&
        odaMeterReading >= 0;

    public bool IsStep2Valid =>
        !string.IsNullOrWhiteSpace(SelectedMake) &&
        !string.IsNullOrWhiteSpace(VehicleModel) &&
        !string.IsNullOrWhiteSpace(OdaMeterReading) &&
        IsOdaMeterReadingValid;

    public bool CanProceedToNext =>
        CurrentStep switch
        {
            1 => IsStep1Valid,
            2 => IsStep2Valid,
            3 => true,
            _ => true
        };

    private void NotifyStep2Validation()
    {
        OnPropertyChanged(nameof(IsStep2Valid));
        OnPropertyChanged(nameof(IsOdaMeterReadingValid));
        OnPropertyChanged(nameof(CanProceedToNext));
    }

    #endregion

    #region Mobile OTP Login

    private bool _isLoginPhoneVisible;
    public bool IsLoginPhoneVisible
    {
        get => _isLoginPhoneVisible;
        set
        {
            if (SetProperty(ref _isLoginPhoneVisible, value))
                OnPropertyChanged(nameof(IsLandingVisible));
        }
    }

    private bool _isLoginOtpVisible;
    public bool IsLoginOtpVisible
    {
        get => _isLoginOtpVisible;
        set
        {
            if (SetProperty(ref _isLoginOtpVisible, value))
                OnPropertyChanged(nameof(IsLandingVisible));
        }
    }

    private string _loginPhone = string.Empty;
    public string LoginPhone
    {
        get => _loginPhone;
        set
        {
            if (SetProperty(ref _loginPhone, value))
            {
                OnPropertyChanged(nameof(IsLoginPhoneValid));
                ((Command)SendOtpCommand).ChangeCanExecute();
            }
        }
    }

    public bool IsLoginPhoneValid => _loginPhone.Length == 10;

    private string _loginOtp = string.Empty;
    public string LoginOtp
    {
        get => _loginOtp;
        set
        {
            if (SetProperty(ref _loginOtp, value))
            {
                OnPropertyChanged(nameof(IsOtpValid));
                ((Command)VerifyOtpCommand).ChangeCanExecute();
            }
        }
    }

    public bool IsOtpValid => _loginOtp.Length == 6 && _loginOtp.All(char.IsDigit);

    private string _otpSentMessage = string.Empty;
    public string OtpSentMessage
    {
        get => _otpSentMessage;
        set => SetProperty(ref _otpSentMessage, value);
    }

    private string _resendOtpText = "Resend OTP";
    public string ResendOtpText
    {
        get => _resendOtpText;
        set => SetProperty(ref _resendOtpText, value);
    }

    private bool _isResendEnabled = true;
    public bool IsResendEnabled
    {
        get => _isResendEnabled;
        set => SetProperty(ref _isResendEnabled, value);
    }

    private string _simulatedOtp = string.Empty;

    public ICommand ShowLoginCommand { get; private set; } = null!;
    public ICommand SendOtpCommand { get; private set; } = null!;
    public ICommand VerifyOtpCommand { get; private set; } = null!;
    public ICommand BackFromLoginCommand { get; private set; } = null!;
    public ICommand BackToPhoneCommand { get; private set; } = null!;
    public ICommand ResendOtpCommand { get; private set; } = null!;

    private async Task StartResendCountdownAsync()
    {
        IsResendEnabled = false;
        ((Command)ResendOtpCommand).ChangeCanExecute();
        for (int i = 30; i > 0; i--)
        {
            ResendOtpText = $"Resend in {i}s";
            await Task.Delay(1000);
        }
        ResendOtpText = "Resend OTP";
        IsResendEnabled = true;
        ((Command)ResendOtpCommand).ChangeCanExecute();
    }

    #endregion

    #region Commands

    public ICommand GoToWizardCommand { get; }

    public ICommand NextStepCommand { get; }

    public ICommand BackStepCommand { get; }

    public ICommand CloseWizardCommand { get; }

    public ICommand SelectVehicleTypeCommand { get; }

    #endregion

    public void ResetWelcomeFlow()
    {
        CurrentStep = 0;

        IsLoginPhoneVisible = false;
        IsLoginOtpVisible = false;
        LoginPhone = string.Empty;
        LoginOtp = string.Empty;
        OtpSentMessage = string.Empty;
        ResendOtpText = "Resend OTP";
        IsResendEnabled = true;

        SelectedVehicleTypeIndex = -1;
        SelectedMake = string.Empty;
        VehicleModel = string.Empty;
        Variant = string.Empty;
        OdaMeterReading = string.Empty;

    }

    public WelcomeViewModel()
    {
        ShowLoginCommand = new Command(() =>
        {
            IsLoginPhoneVisible = true;
            IsLoginOtpVisible = false;
            LoginPhone = string.Empty;
            LoginOtp = string.Empty;
            OtpSentMessage = string.Empty;
        });

        SendOtpCommand = new Command(
            execute: async () =>
            {
                // TODO: Replace with real OTP API (e.g., Firebase Phone Auth, Twilio Verify)
                _simulatedOtp = new Random().Next(100000, 999999).ToString();
                OtpSentMessage = $"OTP sent to +91 {_loginPhone[..5]} {_loginPhone[5..]}. Enter the code below.";
                System.Diagnostics.Debug.WriteLine($"[Demo OTP] {_simulatedOtp}");
                LoginOtp = string.Empty;
                IsLoginPhoneVisible = false;
                IsLoginOtpVisible = true;
                _ = StartResendCountdownAsync();
            },
            canExecute: () => IsLoginPhoneValid);

        VerifyOtpCommand = new Command(
            execute: async () =>
            {
                // TODO: Replace with real OTP verification API call
                await Shell.Current.GoToAsync("///main");
            },
            canExecute: () => IsOtpValid);

        BackFromLoginCommand = new Command(() =>
        {
            IsLoginPhoneVisible = false;
            IsLoginOtpVisible = false;
            LoginPhone = string.Empty;
            LoginOtp = string.Empty;
            OtpSentMessage = string.Empty;
            ResendOtpText = "Resend OTP";
            IsResendEnabled = true;
        });

        BackToPhoneCommand = new Command(() =>
        {
            IsLoginOtpVisible = false;
            IsLoginPhoneVisible = true;
            LoginOtp = string.Empty;
            OtpSentMessage = string.Empty;
        });

        ResendOtpCommand = new Command(
            execute: async () =>
            {
                // TODO: Replace with real OTP resend API call
                _simulatedOtp = new Random().Next(100000, 999999).ToString();
                System.Diagnostics.Debug.WriteLine($"[Demo OTP] Resent: {_simulatedOtp}");
                LoginOtp = string.Empty;
                _ = StartResendCountdownAsync();
            },
            canExecute: () => IsResendEnabled);

        GoToWizardCommand = new Command(() =>
        {
            CurrentStep = 1;
        });

        NextStepCommand = new Command(() =>
        {
            if (CurrentStep < 3)
            {
                CurrentStep++;
            }
        });

        BackStepCommand = new Command(() =>
        {
            if (CurrentStep > 1)
            {
                CurrentStep--;
            }
        });

        CloseWizardCommand = new Command(() =>
        {
            CurrentStep = 0;
        });

        SelectVehicleTypeCommand = new Command<string>(index =>
        {
            if (int.TryParse(index, out int selectedIndex))
            {
                SelectedVehicleTypeIndex = selectedIndex;
            }
        });

    }

    #region Vehicle Type Display

    public string VehicleTypeName =>
        SelectedVehicleTypeIndex switch
        {
            0 => "Car / Sedan",
            1 => "Motorcycle",
            2 => "Truck",
            3 => "Electric Vehicle",
            _ => "Vehicle"
        };

    public string VehicleTypeIcon =>
        SelectedVehicleTypeIndex switch
        {
            0 => "\uE531",
            1 => "\uE91B",
            2 => "\uE558",
            3 => "\uEBD1",
            _ => "\uE531"
        };

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(
        ref T backingStore,
        T value,
        [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
        {
            return false;
        }

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged(
        [CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}