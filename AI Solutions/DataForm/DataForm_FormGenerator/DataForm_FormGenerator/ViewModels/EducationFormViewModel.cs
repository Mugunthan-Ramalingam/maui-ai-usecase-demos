using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DataForm_FormGenerator;

public class EducationFormViewModel : INotifyPropertyChanged
{
    private readonly EducationFormService formService;
    private readonly StudentInformation studentInformation;
    private bool isBusy;
    private string? errorMessage;
    private string? pendingEducationType;
    private bool hasCredentialError;
    private EducationEntryViewModel? generatingCard;
    private CancellationTokenSource? generationCts;

    public EducationFormViewModel(IAiSchemaService schemaService)
    {
        this.formService = new EducationFormService(schemaService);
        this.studentInformation = new StudentInformation
        {
            FullLegalName = "Shalini Suresh",
            EmailAddress = "shalini.suresh@example.com",
            MobileNumber = "5551234567",
            Nationality = "United States",
            Gender = "Female",
            DateOfBirth = new DateTime(1998, 6, 15),
        };
        this.formService.BusyChanged += OnFormServiceBusyChanged;

        _ = ProbeCredentialsOnStartupAsync();
    }

    /// <summary>
    /// Runs the credential probe asynchronously on startup. On failure
    /// the message is published to <see cref="ErrorMessage"/> so the
    /// existing error banner in the page picks it up. On success it
    /// silently clears a leftover credential banner so a user who fixed a
    /// bad key sees a clean page.
    /// </summary>
    private async Task ProbeCredentialsOnStartupAsync()
    {
        try
        {
            await formService.ValidateCredentialsAsync();
            if (this.hasCredentialError)
            {
                this.hasCredentialError = false;
                ErrorMessage = null;
            }
        }
        catch (AiCredentialException ex)
        {
            this.hasCredentialError = true;
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to verify the Azure OpenAI credentials. {ex.Message}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// POCO bound to the top "Student Information" SfDataForm. Always bound
    /// so the form stays in sync with the user's edits.
    /// </summary>
    public StudentInformation StudentInformation => this.studentInformation;

    /// <summary>
    /// One card per education level added during the session. Each card
    /// owns its own SfDataForm and renders independently.
    /// </summary>
    public ObservableCollection<EducationEntryViewModel> EducationEntries { get; } = new();

    public IReadOnlyList<string> EducationOptions => EducationFormCatalog.Options;

    public bool IsBusy
    {
        get => this.isBusy;
        private set
        {
            if (this.isBusy != value)
            {
                this.isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    public string? ErrorMessage
    {
        get => this.errorMessage;
        private set
        {
            if (this.errorMessage != value)
            {
                this.errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Currently-selected level in the "Add an Education level" combo box.
    /// When this changes to a non-empty value the VM immediately generates a
    /// card for that level — the pick itself is the trigger. After the
    /// generation completes this property resets to null so the same level
    /// can be picked again.
    /// </summary>
    public string? PendingEducationType
    {
        get => this.pendingEducationType;
        set
        {
            if (string.Equals(this.pendingEducationType, value, StringComparison.Ordinal))
            {
                return;
            }
            this.pendingEducationType = value;
            OnPropertyChanged();

            if (!string.IsNullOrWhiteSpace(value) && EducationFormCatalog.IsKnown(value))
            {
                _ = HandlePendingEducationSelectedAsync(value);
            }
        }
    }

    private async Task HandlePendingEducationSelectedAsync(string level)
    {
        await AddEducationEntryAsync(level);
        PendingEducationType = null;
    }

    private EducationEntryViewModel CreateCard(string educationType)
    {
        var idx = EducationEntries.Count + 1;
        var card = new EducationEntryViewModel(idx, educationType, EducationFormCatalog.CreateEntry);
        card.DeleteRequested += (_, _) => RemoveEducationEntry(card);
        return card;
    }

    /// <summary>
    /// Adds a new card pinned to <paramref name="level"/> and kicks off
    /// AI generation. On any failure the card stays in place showing its
    /// error so the user can delete it and retry.
    /// </summary>
    private async Task AddEducationEntryAsync(string level)
    {
        if (IsBusy)
        {
            return;
        }

        var normalized = (level ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(normalized) || !EducationFormCatalog.IsKnown(normalized))
        {
            ErrorMessage = "Pick an education level before adding fields.";
            return;
        }

        ErrorMessage = null;

        var card = CreateCard(normalized);
        EducationEntries.Add(card);

        await GenerateForCardAsync(card);
    }

    private void RemoveEducationEntry(EducationEntryViewModel card)
    {
        if (!EducationEntries.Remove(card))
        {
            return;
        }

        // Deleting a card whose generation is still running abandons that call.
        if (ReferenceEquals(this.generatingCard, card))
        {
            this.generationCts?.Cancel();
        }

        for (var i = 0; i < EducationEntries.Count; i++)
        {
            EducationEntries[i].Renumber(i + 1);
        }
    }

    private async Task GenerateForCardAsync(EducationEntryViewModel card)
    {
        if (card is null || IsBusy)
        {
            return;
        }

        var level = card.EducationType;
        if (string.IsNullOrEmpty(level) || !EducationFormCatalog.IsKnown(level))
        {
            card.SetError("Invalid education level.");
            ErrorMessage = "Pick an education level before generating.";
            return;
        }

        using var cts = new CancellationTokenSource();
        this.generationCts = cts;
        this.generatingCard = card;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var (title, entry) = await formService.GenerateAsync(level, cts.Token);
            if (title is null || entry is null)
            {
                card.SetError("Generation did not return a valid schema.");
                return;
            }

            card.ApplyGenerated(title);
        }
        catch (OperationCanceledException)
        {
            // Only reachable when the card was deleted mid-generation.
        }
        catch (Exception ex)
        {
            var message = $"Unable to generate form: {ex.Message}";
            card.SetError(message);
            ErrorMessage = message;
        }
        finally
        {
            this.generatingCard = null;
            this.generationCts = null;
            IsBusy = false;
        }
    }

    private void OnFormServiceBusyChanged(object? sender, EventArgs e)
        => IsBusy = formService.IsBusy;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
