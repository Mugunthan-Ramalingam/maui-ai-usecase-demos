using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace SmartRTEFormatter;

/// <summary>
/// Provides editor content, formatting state, and commands for the main page.
/// </summary>
public sealed class MainPageViewModel : INotifyPropertyChanged
{
    private readonly IAIFormattingService formattingService;
    private readonly Command<string> formatCommand;
    private string formattedHtml = string.Empty;
    private bool isFormatting;
    private string errorMessage = string.Empty;

    /// <summary>
    /// Initializes the view model with the AI formatting service.
    /// </summary>
    /// <param name="formattingService">The service used to format editor content.</param>
    public MainPageViewModel(IAIFormattingService formattingService)
    {
        this.formattingService = formattingService;
        formatCommand = new Command<string>(
            async input => await FormatAsync(input),
            input => !IsFormatting && !string.IsNullOrWhiteSpace(input));
    }

    /// <summary>
    /// Occurs when a bindable view-model property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the latest formatted HTML displayed in the rich text editor.
    /// </summary>
    public string FormattedHtml
    {
        get => formattedHtml;
        private set => SetProperty(ref formattedHtml, value);
    }

    /// <summary>
    /// Gets a value indicating whether an AI formatting request is in progress.
    /// </summary>
    public bool IsFormatting
    {
        get => isFormatting;
        private set
        {
            if (SetProperty(ref isFormatting, value))
            {
                OnPropertyChanged(nameof(IsFormatButtonVisible));
                formatCommand.ChangeCanExecute();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the format button should be visible.
    /// </summary>
    public bool IsFormatButtonVisible => !IsFormatting;

    /// <summary>
    /// Gets the most recent formatting error message, if any.
    /// </summary>
    public string ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether an error message is available to display.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Gets the command that formats the editor's current text.
    /// </summary>
    public ICommand FormatCommand => formatCommand;

    private async Task FormatAsync(string? inputText)
    {
        var input = inputText?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        IsFormatting = true;
        ErrorMessage = string.Empty;

        try
        {
            var result = await formattingService.FormatAsync(input);
            FormattedHtml = result.HtmlContent;
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsFormatting = false;
        }
    }

    private bool SetProperty<T>(
        ref T storage,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}