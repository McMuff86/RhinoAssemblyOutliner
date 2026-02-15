using Xunit;

namespace RhinoAssemblyOutliner.Tests.Services;

/// <summary>
/// Tests for UndoHelper scope pattern.
/// Since RhinoDoc.BeginUndoRecord/EndUndoRecord isn't available in tests,
/// we test the pattern with a test double that tracks undo record lifecycle.
/// </summary>
public class UndoHelperTests
{
    #region Test Double

    private sealed class FakeUndoSystem
    {
        private uint _nextId = 1;
        public List<(uint Id, string Description)> OpenedRecords { get; } = new();
        public List<uint> ClosedRecords { get; } = new();

        public uint BeginUndoRecord(string description)
        {
            var id = _nextId++;
            OpenedRecords.Add((id, description));
            return id;
        }

        public bool EndUndoRecord(uint id)
        {
            ClosedRecords.Add(id);
            return true;
        }
    }

    /// <summary>
    /// Mirrors the UndoHelper pattern: IDisposable scope that wraps
    /// BeginUndoRecord/EndUndoRecord.
    /// </summary>
    private sealed class UndoScope : IDisposable
    {
        private readonly FakeUndoSystem _undo;
        private readonly uint _recordId;
        private bool _disposed;

        public uint RecordId => _recordId;
        public string Description { get; }

        public UndoScope(FakeUndoSystem undo, string description)
        {
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            _recordId = undo.BeginUndoRecord(description);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _undo.EndUndoRecord(_recordId);
        }
    }

    #endregion

    private readonly FakeUndoSystem _undo = new();

    // --- Basic Scope Pattern ---

    [Fact]
    public void UndoScope_OpensRecordOnCreate()
    {
        using var scope = new UndoScope(_undo, "Test");
        Assert.Single(_undo.OpenedRecords);
        Assert.Empty(_undo.ClosedRecords);
    }

    [Fact]
    public void UndoScope_ClosesRecordOnDispose()
    {
        var scope = new UndoScope(_undo, "Test");
        scope.Dispose();

        Assert.Single(_undo.OpenedRecords);
        Assert.Single(_undo.ClosedRecords);
        Assert.Equal(_undo.OpenedRecords[0].Id, _undo.ClosedRecords[0]);
    }

    [Fact]
    public void UndoScope_UsingStatement_AutoCloses()
    {
        using (var scope = new UndoScope(_undo, "Auto"))
        {
            Assert.Empty(_undo.ClosedRecords);
        }
        Assert.Single(_undo.ClosedRecords);
    }

    [Fact]
    public void UndoScope_DoubleDispose_ClosesOnce()
    {
        var scope = new UndoScope(_undo, "Test");
        scope.Dispose();
        scope.Dispose();
        Assert.Single(_undo.ClosedRecords);
    }

    // --- Description ---

    [Fact]
    public void UndoScope_StoresDescription()
    {
        using var scope = new UndoScope(_undo, "Change Assembly Configuration");
        Assert.Equal("Change Assembly Configuration", scope.Description);
        Assert.Equal("Change Assembly Configuration", _undo.OpenedRecords[0].Description);
    }

    [Fact]
    public void UndoScope_NullDescription_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UndoScope(_undo, null!));
    }

    [Fact]
    public void UndoScope_NullUndoSystem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UndoScope(null!, "Test"));
    }

    [Fact]
    public void UndoScope_EmptyDescription_Allowed()
    {
        using var scope = new UndoScope(_undo, "");
        Assert.Equal("", scope.Description);
    }

    // --- Nested Scopes ---

    [Fact]
    public void NestedScopes_EachGetsUniqueId()
    {
        using var outer = new UndoScope(_undo, "Outer");
        using var inner = new UndoScope(_undo, "Inner");

        Assert.Equal(2, _undo.OpenedRecords.Count);
        Assert.NotEqual(outer.RecordId, inner.RecordId);
    }

    [Fact]
    public void NestedScopes_InnerClosedFirst()
    {
        uint innerId;
        uint outerId;

        using (var outer = new UndoScope(_undo, "Outer"))
        {
            outerId = outer.RecordId;
            using (var inner = new UndoScope(_undo, "Inner"))
            {
                innerId = inner.RecordId;
            }
            // Inner should be closed, outer still open
            Assert.Single(_undo.ClosedRecords);
            Assert.Equal(innerId, _undo.ClosedRecords[0]);
        }
        // Now outer is closed too
        Assert.Equal(2, _undo.ClosedRecords.Count);
        Assert.Equal(outerId, _undo.ClosedRecords[1]);
    }

    [Fact]
    public void TripleNested_ClosedInReverseOrder()
    {
        using (var a = new UndoScope(_undo, "A"))
        using (var b = new UndoScope(_undo, "B"))
        using (var c = new UndoScope(_undo, "C"))
        {
            // all open
        }

        Assert.Equal(3, _undo.ClosedRecords.Count);
        // C#'s using disposes in reverse order
        Assert.Equal(3u, _undo.ClosedRecords[0]); // C
        Assert.Equal(2u, _undo.ClosedRecords[1]); // B
        Assert.Equal(1u, _undo.ClosedRecords[2]); // A
    }

    // --- Exception Safety ---

    [Fact]
    public void UndoScope_ExceptionInBody_StillCloses()
    {
        try
        {
            using var scope = new UndoScope(_undo, "Crash");
            throw new InvalidOperationException("simulated error");
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        Assert.Single(_undo.ClosedRecords);
    }

    // --- RecordId ---

    [Fact]
    public void UndoScope_RecordId_MatchesOpened()
    {
        using var scope = new UndoScope(_undo, "Test");
        Assert.Equal(_undo.OpenedRecords[0].Id, scope.RecordId);
    }

    [Fact]
    public void SequentialScopes_IncrementingIds()
    {
        using (new UndoScope(_undo, "A")) { }
        using (new UndoScope(_undo, "B")) { }
        using (new UndoScope(_undo, "C")) { }

        Assert.Equal(new uint[] { 1, 2, 3 }, _undo.OpenedRecords.Select(r => r.Id).ToArray());
    }
}
