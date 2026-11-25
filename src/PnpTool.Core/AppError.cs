using PnpTool.Core.Infrastructure;

namespace PnpTool.Core;

/// <summary>Original: <c>Error.TiposError</c>.</summary>
public enum ErrorKind
{
    None,
    Warning,
    MissingData,
    Error,
}

/// <summary>
/// Result of an operation: a severity plus accumulated messages. Original: <c>Error</c>.
/// Most original methods return one of these instead of throwing.
/// </summary>
public class AppError
{
    private string _message = string.Empty;

    /// <summary>Original: <c>TipoError</c>.</summary>
    public ErrorKind Kind { get; set; } = ErrorKind.None;

    /// <summary>True for <see cref="ErrorKind.Error"/> and <see cref="ErrorKind.MissingData"/>. Original: <c>HayError</c>.</summary>
    public bool HasError => Kind is ErrorKind.Error or ErrorKind.MissingData;

    /// <summary>Original: <c>HayWarning</c>.</summary>
    public bool HasWarning => Kind == ErrorKind.Warning;

    /// <summary>Original: <c>Titulo</c>.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Setting this appends a line to the existing text; it never replaces it.</summary>
    public string Message
    {
        get => _message;
        set => Append(value);
    }

    /// <summary>Appends the other message and keeps the more severe kind.</summary>
    public void Merge(AppError other)
    {
        Append(other.Message);
        if (Kind == ErrorKind.Error || other.Kind == ErrorKind.Error)
        {
            Kind = ErrorKind.Error;
        }
        else if (Kind == ErrorKind.MissingData || other.Kind == ErrorKind.MissingData)
        {
            Kind = ErrorKind.MissingData;
        }
        else if (Kind == ErrorKind.Warning || other.Kind == ErrorKind.Warning)
        {
            Kind = ErrorKind.Warning;
        }
        else
        {
            Kind = ErrorKind.None;
        }
    }

    /// <summary>Appends the translated text for a resource key. Original: <c>SetMessageResource</c>.</summary>
    public void AppendResource(string key) => Append(StringResources.Get(key));

    private void Append(string value)
    {
        if (value == string.Empty)
        {
            return;
        }

        if (_message != string.Empty)
        {
            _message += Environment.NewLine;
        }

        _message += value;
    }
}
