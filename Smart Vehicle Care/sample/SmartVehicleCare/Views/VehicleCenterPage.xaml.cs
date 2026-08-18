using Syncfusion.Maui.DataGrid;
using Syncfusion.Maui.Maps;
using Syncfusion.Maui.TreeView;
using SmartVehicleCare.Models;
using SmartVehicleCare.ViewModels;

namespace SmartVehicleCare.Views;

public partial class VehicleCenterPage : ContentView
{
    private VehicleCenterViewModel? _vm;

    public VehicleCenterPage()
    {
        InitializeComponent();

        // On mobile: override Fill with explicit widths so the grid scrolls horizontally
        if (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS)
        {
            ServiceHistoryGrid.ColumnWidthMode = ColumnWidthMode.None;
            ServiceHistoryGrid.Columns[0].Width = 120; // Date
            ServiceHistoryGrid.Columns[1].Width = 190; // ServiceType
            ServiceHistoryGrid.Columns[2].Width = 130; // Mileage
            ServiceHistoryGrid.Columns[3].Width = 90;  // Amount
            ServiceHistoryGrid.Columns[4].Width = 100; // Status
        }
    }

    // VM lives on RootGrid (declared in XAML), not on this ContentView
    private void AttachToViewModel()
    {
        var newVm = RootGrid?.BindingContext as VehicleCenterViewModel;
        if (newVm == _vm) return;

        if (_vm != null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm.MapMarkers.CollectionChanged -= OnMapMarkersChanged;
        }

        _vm = newVm;

        if (_vm != null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
            _vm.MapMarkers.CollectionChanged += OnMapMarkersChanged;
            // Sync any markers that were added before attachment
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (DesktopMapLayer != null) DesktopMapLayer.Markers = _vm.MapMarkers;
                if (MobileMapLayer  != null) MobileMapLayer.Markers  = _vm.MapMarkers;
            });
        }
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        AttachToViewModel();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler != null)
            AttachToViewModel();
    }

    private void OnMapMarkersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_vm is null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (DesktopMapLayer != null) DesktopMapLayer.Markers = _vm.MapMarkers;
            if (MobileMapLayer  != null) MobileMapLayer.Markers  = _vm.MapMarkers;
        });
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(VehicleCenterViewModel.UserLatitude)
                                or nameof(VehicleCenterViewModel.UserLongitude)
                                or nameof(VehicleCenterViewModel.UserMapZoom)))
            return;

        if (_vm is null) return;
        var center = new MapLatLng(_vm.UserLatitude, _vm.UserLongitude);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (DesktopMapLayer != null)
            {
                DesktopMapLayer.Center = center;
                if (e.PropertyName == nameof(VehicleCenterViewModel.UserMapZoom)
                    && DesktopMapLayer.ZoomPanBehavior is { } dzpb)
                    dzpb.ZoomLevel = _vm.UserMapZoom;
            }
            if (MobileMapLayer != null)
            {
                MobileMapLayer.Center = center;
                if (e.PropertyName == nameof(VehicleCenterViewModel.UserMapZoom)
                    && MobileMapLayer.ZoomPanBehavior is { } mzpb)
                    mzpb.ZoomLevel = _vm.UserMapZoom;
            }
        });
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == nameof(IsVisible) && IsVisible && VehicleTabView != null)
        {
            VehicleTabView.SelectedIndex = 0;
        }
    }

    private void OnServiceHistoryQueryRowHeight(object sender, DataGridQueryRowHeightEventArgs e)
    {
        if (e.RowIndex > 0) // skip header row (always index 0)
        {
            e.Height = e.GetIntrinsicRowHeight(e.RowIndex);
            if (e.Height < 52) e.Height = 52;
            e.Handled = true;
        }
    }

    private void OnEditServiceDateFieldTapped(object sender, Microsoft.Maui.Controls.TappedEventArgs e)
        => EditServiceDatePicker.IsOpen = true;

    private void OnEditServiceDateOkClicked(object sender, EventArgs e)
    {
        if (EditServiceDatePicker.SelectedDate.HasValue && RootGrid.BindingContext is VehicleCenterViewModel vm)
            vm.EditServiceDate = EditServiceDatePicker.SelectedDate.Value;
    }

    private void OnEditFuelDateFieldTapped(object sender, Microsoft.Maui.Controls.TappedEventArgs e)
        => EditFuelDatePicker.IsOpen = true;

    private void OnEditFuelDateOkClicked(object sender, EventArgs e)
    {
        if (EditFuelDatePicker.SelectedDate.HasValue && RootGrid.BindingContext is VehicleCenterViewModel vm)
            vm.EditFuelDate = EditFuelDatePicker.SelectedDate.Value;
    }

    private void OnPreviousMonthTapped(object sender, Microsoft.Maui.Controls.TappedEventArgs e)
    {
        if (ScheduleCalendar == null) return;
        var current = ScheduleCalendar.DisplayDate;
        var newDate = current.AddMonths(-1);
        ScheduleCalendar.DisplayDate = newDate;

        if (BindingContext is VehicleCenterViewModel vm)
            vm.CurrentMonthDisplayDate = newDate;
    }

    private void OnNextMonthTapped(object sender, Microsoft.Maui.Controls.TappedEventArgs e)
    {
        if (ScheduleCalendar == null) return;
        var current = ScheduleCalendar.DisplayDate;
        var newDate = current.AddMonths(1);
        ScheduleCalendar.DisplayDate = newDate;

        if (BindingContext is VehicleCenterViewModel vm)
            vm.CurrentMonthDisplayDate = newDate;
    }
}
