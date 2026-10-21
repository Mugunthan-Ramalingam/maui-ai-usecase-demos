namespace DataForm_FormGenerator;

/// <summary>
/// Thrown when the AI service cannot be used because its configuration or
/// credentials are missing, malformed or rejected by the provider. Lets
/// callers react to the failure kind without matching on message text.
/// </summary>
public sealed class AiCredentialException : InvalidOperationException
{
    public AiCredentialException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
