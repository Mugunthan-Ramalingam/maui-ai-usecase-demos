using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using SmartAIDatePicker.AIService;

namespace SmartAIDatePicker.ViewModels;

public sealed class DatePickerViewModel : INotifyPropertyChanged
{
    private readonly IAzureOpenAIService azureAIService;

    private string dateRequest = string.Empty;
    private DateTime? selectedDate = DateTime.Today;
    private DateTime? pickerDate = DateTime.Today;
    private bool isBusy;
    private bool isPickerOpen;
    private bool isApplyingAIResult;
    private string placeholder = "Try: next leap day, Next christmas...";

    private sealed record DateResolutionResult(DateTime? Date, string? ErrorMessage)
    {
        public bool IsSuccess => Date.HasValue;
    }

    public DatePickerViewModel(IAzureOpenAIService azureAIService)
    {
        this.azureAIService = azureAIService;

        SearchCommand = new Command(async () => await ResolveDateRequestAsync());
        OpenPickerCommand = new Command(() => IsPickerOpen = true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SearchCommand { get; }

    public ICommand OpenPickerCommand { get; }

    public string DateRequest
    {
        get => dateRequest;
        set => SetProperty(ref dateRequest, value ?? string.Empty);
    }

    public DateTime? SelectedDate
    {
        get => selectedDate;
        set
        {
            if (SetProperty(ref selectedDate, value) && value.HasValue)
            {
                OnPropertyChanged(nameof(SelectedDateText));
            }
        }
    }

    public DateTime? PickerDate
    {
        get => pickerDate;
        set
        {
            if (SetProperty(ref pickerDate, value) &&
                !isApplyingAIResult &&
                value.HasValue)
            {
                SelectedDate = value;
            }
        }
    }

    public string SelectedDateText =>
        SelectedDate?.ToString("dd MMMM yyyy", CultureInfo.CurrentCulture) ?? string.Empty;

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                OnPropertyChanged(nameof(IsSearchIconVisible));
                OnPropertyChanged(nameof(IsLoadingVisible));
                OnPropertyChanged(nameof(IsEditorEnabled));
                OnPropertyChanged(nameof(SearchButtonOpacity));
            }
        }
    }

    public bool IsPickerOpen
    {
        get => isPickerOpen;
        set => SetProperty(ref isPickerOpen, value);
    }

    public string Placeholder
    {
        get => placeholder;
        private set => SetProperty(ref placeholder, value);
    }

    public bool IsSearchIconVisible => !IsBusy;

    public bool IsLoadingVisible => IsBusy;

    public bool IsEditorEnabled => !IsBusy;

    public double SearchButtonOpacity =>
        IsBusy || string.IsNullOrWhiteSpace(DateRequest) ? 0.6 : 1;

    public Color SearchButtonBackgroundColor =>
        string.IsNullOrWhiteSpace(DateRequest)
            ? Color.FromArgb("#DFD8F7")
            : Color.FromArgb("#6B4FD3");

    private async Task ResolveDateRequestAsync()
    {
        var request = DateRequest.Trim();

        if (IsBusy || string.IsNullOrWhiteSpace(request))
        {
            return;
        }

        IsBusy = true;
        DateRequest = string.Empty;
        Placeholder = "Finding date...";

        var result = await GetDateFromAIAsync(request);

        if (result.IsSuccess && result.Date.HasValue)
        {
            isApplyingAIResult = true;
            PickerDate = result.Date.Value;
            IsPickerOpen = true;

            await Task.Delay(1200);

            IsPickerOpen = false;
            isApplyingAIResult = false;
            SelectedDate = result.Date.Value;
        }

        isApplyingAIResult = false;
        Placeholder = "Try: next leap day, Next christmas...";
        IsBusy = false;
    }

    private async Task<DateResolutionResult> GetDateFromAIAsync(string request)
    {
        var prompt =
            $"Reference date (today): {DateTime.Today:yyyy-MM-dd} ({DateTime.Today:dddd}). " +
            "Resolve the following natural-language question to one specific Gregorian calendar date. " +
            "It may refer to a historical event, war, holiday, weekday, leap day, anniversary, or relative date. " +
            "For a war, use its start date unless the user asks for its end. Apply the exact meaning of " +
            "next, upcoming, this, last, from now, and after; next/upcoming must be strictly after the reference date. " +
            "Use the next occurrence for recurring holidays without a year. Interpret Independence Day without a country " +
            "as US Independence Day on July 4. Return only yyyy-MM-dd or INVALID_REQUEST. " +
            $"Question: {request}";

        var completion = await azureAIService.GetCompletion(prompt);

        if (!string.IsNullOrWhiteSpace(completion))
        {
                var normalized = completion.Trim();

                if (string.Equals(normalized, "INVALID_REQUEST", StringComparison.OrdinalIgnoreCase))
                {
                    return new DateResolutionResult(null, "The request is too vague to resolve to a single valid date.");
                }

                if (DateTime.TryParseExact(
                    normalized,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var aiDate))
                {
                    if (IsValidAIResult(request, aiDate))
                    {
                        return new DateResolutionResult(aiDate, null);
                    }
                }

                var match = Regex.Match(normalized, @"\d{4}-\d{2}-\d{2}");

                if (match.Success && DateTime.TryParseExact(
                    match.Value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out aiDate))
                {
                    if (IsValidAIResult(request, aiDate))
                    {
                        return new DateResolutionResult(aiDate, null);
                    }
                }
        }

        var fallbackDate = CalculateDate(request);
        return fallbackDate.HasValue
            ? new DateResolutionResult(fallbackDate, null)
            : new DateResolutionResult(null, "Unable to resolve the requested date.");
    }

    private static DateTime? CalculateDate(string request)
    {
        var text = request.ToLowerInvariant();
        var today = DateTime.Today;

        if (text.Contains("today")) return today;
        if (text.Contains("tomorrow")) return today.AddDays(1);
        if (text.Contains("next friday")) return NextWeekday(today, DayOfWeek.Friday);
        if (text.Contains("two weeks")) return today.AddDays(14);
        if (text.Contains("three weeks")) return today.AddDays(21);
        if (text.Contains("30 days")) return today.AddDays(30);
        if (text.Contains("last working day of this month")) return LastWorkingDay(today.Year, today.Month);

        if (text.Contains("first business day of next month"))
        {
            var nextMonth = today.AddMonths(1);
            return FirstBusinessDay(nextMonth.Year, nextMonth.Month);
        }

        if (text.Contains("independence day")) return Holiday(today.Year, 7, 4);
        if (text.Contains("christmas")) return Holiday(today.Year, 12, 25);
        if (text.Contains("new year's") || text.Contains("new years")) return Holiday(today.Year, 1, 1);
        if (text.Contains("thanksgiving")) return NthWeekday(today.Year, 11, DayOfWeek.Thursday, 4);
        if (text.Contains("diwali")) return Diwali(today.Year);

        return null;
    }

    private static bool IsValidAIResult(string request, DateTime date)
    {
        var text = request.ToLowerInvariant();
        var requiresFutureDate = text.Contains("next") ||
                                 text.Contains("upcoming") ||
                                 text.Contains("coming");

        return !requiresFutureDate || date.Date > DateTime.Today;
    }

    private static DateTime NextWeekday(DateTime date, DayOfWeek weekday)
    {
        var days = ((int)weekday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(days == 0 ? 7 : days);
    }

    private static DateTime FirstBusinessDay(int year, int month)
    {
        var date = new DateTime(year, month, 1);

        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static DateTime LastWorkingDay(int year, int month)
    {
        var date = new DateTime(year, month, DateTime.DaysInMonth(year, month));

        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    private static DateTime FirstWeekday(DateTime month, DayOfWeek weekday)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        return first.AddDays(((int)weekday - (int)first.DayOfWeek + 7) % 7);
    }

    private static DateTime NthWeekday(int year, int month, DayOfWeek weekday, int occurrence) =>
        FirstWeekday(new DateTime(year, month, 1), weekday).AddDays((occurrence - 1) * 7);

    private static DateTime Holiday(int year, int month, int day)
    {
        var date = new DateTime(year, month, day);
        return date < DateTime.Today ? date.AddYears(1) : date;
    }

    private static DateTime Diwali(int year)
    {
        var dates = new Dictionary<int, DateTime>
        {
            [2024] = new(2024, 11, 1),
            [2025] = new(2025, 10, 20),
            [2026] = new(2026, 11, 8),
            [2027] = new(2027, 10, 29),
            [2028] = new(2028, 10, 17),
            [2029] = new(2029, 11, 5),
            [2030] = new(2030, 10, 26)
        };

        return dates.TryGetValue(year, out var date) && date >= DateTime.Today
            ? date
            : dates.TryGetValue(year + 1, out date)
                ? date
                : new DateTime(year, 10, 31);
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);

        if (propertyName is nameof(DateRequest))
        {
            OnPropertyChanged(nameof(SearchButtonBackgroundColor));
            OnPropertyChanged(nameof(SearchButtonOpacity));
        }

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
