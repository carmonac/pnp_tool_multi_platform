using PnpTool.Core.Infrastructure;

namespace PnpTool.Core.Tests.Infrastructure;

public sealed class OperationStackTests
{
    /// <summary>A number changed by operations that add to it.</summary>
    private sealed class Counter
    {
        public int Value { get; private set; }

        public Operation Add(int amount)
        {
            Value += amount;
            return new Operation("add " + amount, () => Value -= amount, () => Value += amount);
        }
    }

    [Fact]
    public void Operations_are_undone_last_first_and_redone_in_order()
    {
        var counter = new Counter();
        var stack = new OperationStack();
        stack.Push(counter.Add(1));
        stack.Push(counter.Add(10));
        stack.Push(counter.Add(100));

        Assert.Equal(["add 100", "add 10", "add 1"], stack.UndoNames);
        Assert.True(stack.Undo());
        Assert.Equal(11, counter.Value);
        Assert.True(stack.Undo());
        Assert.Equal(1, counter.Value);
        Assert.True(stack.CanRedo);

        Assert.True(stack.Redo());
        Assert.Equal(11, counter.Value);
        Assert.True(stack.Redo());
        Assert.Equal(111, counter.Value);
        Assert.False(stack.Redo());
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Nothing_to_undo_or_redo_does_nothing()
    {
        var stack = new OperationStack();

        Assert.False(stack.CanUndo);
        Assert.False(stack.Undo());
        Assert.False(stack.Redo());
    }

    [Fact]
    public void A_new_operation_discards_what_could_be_redone()
    {
        var counter = new Counter();
        var stack = new OperationStack();
        stack.Push(counter.Add(1));
        stack.Push(counter.Add(10));
        stack.Undo();

        stack.Push(counter.Add(5));

        Assert.False(stack.CanRedo);
        Assert.Equal(6, counter.Value);
        stack.Undo();
        stack.Undo();
        Assert.Equal(0, counter.Value);
    }

    [Fact]
    public void Only_the_last_ten_operations_are_remembered()
    {
        var counter = new Counter();
        var stack = new OperationStack();
        for (int i = 0; i < 13; i++)
        {
            stack.Push(counter.Add(1));
        }

        int undone = 0;
        while (stack.Undo())
        {
            undone++;
        }

        Assert.Equal(OperationStack.Capacity, undone);
        Assert.Equal(3, counter.Value);
    }

    [Fact]
    public void A_blocked_stack_remembers_nothing_and_clear_forgets_everything()
    {
        var counter = new Counter();
        var stack = new OperationStack { Blocked = true };
        int changes = 0;
        stack.Changed += (_, _) => changes++;

        stack.Push(counter.Add(1));
        Assert.False(stack.CanUndo);
        Assert.Equal(0, changes);

        stack.Blocked = false;
        stack.Push(counter.Add(1));
        stack.Push(counter.Add(1));
        stack.Undo();
        Assert.Equal(3, changes);

        stack.Clear();
        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Equal(4, changes);
    }
}
