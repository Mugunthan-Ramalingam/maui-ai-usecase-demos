namespace SmartRTEFormatter;

/// <summary>
/// Defines the operation for converting unstructured text into formatted HTML.
/// </summary>
public interface IAIFormattingService
{
    /// <summary>
    /// Formats the supplied text into structured HTML.
    /// </summary>
    /// <param name="content">The unstructured text to format.</param>
    /// <returns>A result containing the formatted HTML.</returns>
    Task<FormattingResult> FormatAsync(string content);
}