namespace PnpTool.Core.Infrastructure;

/// <summary>Something the user did that can be taken back and done again.</summary>
/// <param name="Name">What it was, for logs and tests.</param>
/// <param name="Undo">Puts things as they were before.</param>
/// <param name="Redo">Puts things as they were after.</param>
public sealed record Operation(string Name, Action Undo, Action Redo);

/// <summary>
/// The last things the user did, to undo them one by one, and the ones undone, to redo
/// them. Original: <c>PilaOperaciones</c>.
/// </summary>
/// <remarks>
/// The original kept ten commands, each with the data to reverse it, and could only undo.
/// Here each entry carries what undoes it and what redoes it, so redoing comes with it; the
/// limit of ten is kept. Doing something new discards what could be redone.
/// </remarks>
public sealed class OperationStack
{
    /// <summary>How many operations are remembered. Original: the size of <c>ListaComandos</c>.</summary>
    public const int Capacity = 10;

    private readonly List<Operation> _done = [];
    private readonly List<Operation> _undone = [];

    /// <summary>Raised when what can be undone or redone changes.</summary>
    public event EventHandler? Changed;

    /// <summary>While true, <see cref="Push"/> does nothing. Original: <c>BloquearActualizar</c>.</summary>
    public bool Blocked { get; set; }

    public bool CanUndo => _done.Count != 0;

    public bool CanRedo => _undone.Count != 0;

    /// <summary>The names of what can be undone, the most recent first.</summary>
    public IReadOnlyList<string> UndoNames => [.. _done.Select(operation => operation.Name).Reverse()];

    /// <summary>Remembers an operation just done; the oldest is forgotten when there are too many. Original: <c>Push</c>.</summary>
    public void Push(Operation operation)
    {
        if (Blocked)
        {
            return;
        }

        _done.Add(operation);
        if (_done.Count > Capacity)
        {
            _done.RemoveAt(0);
        }

        _undone.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the last operation, if there is one. Original: <c>Deshacer</c>.</summary>
    public bool Undo()
    {
        if (_done.Count == 0)
        {
            return false;
        }

        Operation operation = _done[^1];
        _done.RemoveAt(_done.Count - 1);
        operation.Undo();
        _undone.Add(operation);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Does again the last operation undone, if there is one.</summary>
    public bool Redo()
    {
        if (_undone.Count == 0)
        {
            return false;
        }

        Operation operation = _undone[^1];
        _undone.RemoveAt(_undone.Count - 1);
        operation.Redo();
        _done.Add(operation);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Forgets everything, as when another project is opened.</summary>
    public void Clear()
    {
        _done.Clear();
        _undone.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
