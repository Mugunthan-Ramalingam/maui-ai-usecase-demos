using SmartRTEFormatter.Services;

namespace SmartRTEFormatter;

public partial class MainPage : ContentPage
{
    private readonly IAIFormattingService formattingService;

    public MainPage(IAIFormattingService formattingService)
    {
        InitializeComponent();

        this.formattingService = formattingService;
    }

    private async void OnFormatTextClicked(object? sender, EventArgs e)
    {
        var input = InputEditor.Text?.Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            await DisplayAlert(
                "Input Required",
                "Please enter some content to format.",
                "OK");

            return;
        }

        FormatButton.IsEnabled = false;
        FormatButton.Text = "Analyzing content...";

        ButtonLoadingIndicator.IsVisible = true;
        ButtonLoadingIndicator.IsRunning = true;

        try
        {
            var result = await formattingService.FormatAsync(input);

            RichTextEditor.Opacity = 0;

            RichTextEditor.HtmlText = result.HtmlContent;

            await RichTextEditor.FadeTo(
                1,
                250,
                Easing.CubicIn);
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Error",
                ex.Message,
                "OK");
        }
        finally
        {
            ButtonLoadingIndicator.IsRunning = false;
            ButtonLoadingIndicator.IsVisible = false;

            FormatButton.Text = "✨ Format with AI";
            FormatButton.IsEnabled = true;
        }
    }
}