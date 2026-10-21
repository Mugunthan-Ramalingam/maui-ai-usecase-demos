using Syncfusion.Maui.DataForm;

namespace DataForm_FormGenerator;

/// <summary>
/// Attach to any SfDataForm (including ones inside a DataTemplate) to give
/// it the shared item-source provider and ComboBox editors for the
/// dropdown fields, so each form needs no code-behind wiring.
/// </summary>
public sealed class DataFormProviderBehavior : Behavior<SfDataForm>
{
    private static readonly FormItemSourceProvider SharedProvider = new();

    protected override void OnAttachedTo(SfDataForm bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.ItemsSourceProvider ??= SharedProvider;
        foreach (var name in FormItemSourceProvider.SourceNames)
        {
            bindable.RegisterEditor(name, DataFormEditorType.ComboBox);
        }
    }
}

/// <summary>
/// Provides static option lists to SfDataForm for fields that render as a
/// ComboBox. The provider is queried per property name, so the same
/// instance serves the Student form and every Education-card form.
/// </summary>
internal sealed class FormItemSourceProvider : IDataFormSourceProvider
{
    private static readonly string[] UsStates =
    {
        "California (CA)", "Texas (TX)", "New York (NY)", "Washington (WA)"
    };

    private static readonly string[] AccreditationOptions =
    {
        "Accredited", "Pending", "Not Accredited"
    };

    private static readonly string[] CountryOptions =
    {
        "United States", "Canada", "United Kingdom", "Australia", "India"
    };

    internal static readonly string[] SourceNames =
    {
        nameof(HighSchoolEntry.State),
        nameof(DiplomaEntry.AccreditationStatus),
        nameof(BachelorsEntry.Country),
        nameof(StudentInformation.Nationality),
        nameof(StudentInformation.Gender),
    };

    public object? GetSource(string sourceName)
    {
        if (sourceName == nameof(HighSchoolEntry.State))
        {
            return UsStates;
        }
        if (sourceName == nameof(DiplomaEntry.AccreditationStatus))
        {
            return AccreditationOptions;
        }
        if (sourceName == nameof(BachelorsEntry.Country))
        {
            return CountryOptions;
        }
        if (sourceName == nameof(StudentInformation.Nationality))
        {
            return StudentInformation.DefaultNationalities;
        }
        if (sourceName == nameof(StudentInformation.Gender))
        {
            return StudentInformation.DefaultGenders;
        }

        return null;
    }
}
