using Com.H.Collections.Generic;
using Com.H.Linq.Async;

namespace Com.H.Tests;

public class CollectionsAndAsyncTests
{
    #region ToChamberedEnumerable (sync - works on all TFMs)

    [Fact]
    public void ToChamberedEnumerable_BasicList_ReturnsChamberCountAndAllItems()
    {
        var items = new List<dynamic> { "a", "b", "c", "d", "e" };
        var result = items.ToChamberedEnumerable(chamberSize: 2);

        Assert.Equal(2, result.ChamberedCount);

        var all = result.ToList();
        Assert.Equal(5, all.Count);
        Assert.Equal("a", (string)all[0]);
        Assert.Equal("e", (string)all[4]);
    }

    [Fact]
    public void ToChamberedEnumerable_EmptyEnumerable_ReturnsZeroChamber()
    {
        IEnumerable<dynamic>? items = Enumerable.Empty<dynamic>();
        var result = items.ToChamberedEnumerable(chamberSize: 2);

        Assert.Equal(0, result.ChamberedCount);
        Assert.Empty(result.ToList());
    }

    [Fact]
    public void ToChamberedEnumerable_NullEnumerable_ReturnsZeroChamber()
    {
        IEnumerable<dynamic>? items = null;
        var result = items.ToChamberedEnumerable();

        Assert.Equal(0, result.ChamberedCount);
        Assert.Empty(result.ToList());
    }

    [Fact]
    public void ToChamberedEnumerable_FewerItemsThanChamber_ReturnsActualCount()
    {
        var items = new List<dynamic> { "a" };
        var result = items.ToChamberedEnumerable(chamberSize: 5);

        Assert.Equal(1, result.ChamberedCount);
        Assert.Single(result.ToList());
    }

    [Fact]
    public void ToChamberedEnumerable_DefaultChamberSize_IsOne()
    {
        var items = new List<dynamic> { "a", "b", "c" };
        var result = items.ToChamberedEnumerable();

        Assert.Equal(1, result.ChamberedCount);
        Assert.Equal(3, result.ToList().Count);
    }

    #endregion

    #region Disposal cascade (sync)

    // A stand-in for a disposable source like Com.H.Data.Common's DbQueryResult:
    // holds an underlying list, records whether Dispose() was called. No DB dependency.
    // Implemented over IEnumerable<object> — C# forbids implementing IEnumerable<dynamic>
    // directly (dynamic isn't a real type), but dynamic erases to object at the CLR level,
    // so ToChamberedEnumerable (IEnumerable<dynamic>?) binds happily.
    private sealed class DisposableEnumerable : IEnumerable<object>, IDisposable
    {
        private readonly IEnumerable<object> _inner;
        public bool Disposed { get; private set; }
        public int DisposeCallCount { get; private set; }

        public DisposableEnumerable(IEnumerable<object> inner) => _inner = inner;

        public IEnumerator<object> GetEnumerator() => _inner.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            Disposed = true;
            DisposeCallCount++;
        }
    }

    [Fact]
    public void ToChamberedEnumerable_MoreRowsThanChambered_DisposingChamberedDisposesSource()
    {
        var source = new DisposableEnumerable(new object[] { "a", "b", "c", "d" });

        var chambered = source.ToChamberedEnumerable(chamberSize: 2);
        Assert.Equal(2, chambered.ChamberedCount);
        Assert.False(chambered.WasExhausted(2));
        Assert.False(source.Disposed);

        chambered.Dispose();

        Assert.True(source.Disposed,
            "Disposing ChamberedEnumerable should dispose the underlying source " +
            "even when more items exist past the chamber.");
    }

    [Fact]
    public void ToChamberedEnumerable_Exhausted_DisposingChamberedDisposesSource()
    {
        var source = new DisposableEnumerable(new object[] { "a" });

        var chambered = source.ToChamberedEnumerable(chamberSize: 5);
        Assert.Equal(1, chambered.ChamberedCount);
        Assert.True(chambered.WasExhausted(5));

        chambered.Dispose();

        Assert.True(source.Disposed);
    }

    [Fact]
    public void ToChamberedEnumerable_DisposeIsIdempotent()
    {
        var source = new DisposableEnumerable(new object[] { "a", "b", "c" });

        var chambered = source.ToChamberedEnumerable(chamberSize: 2);
        chambered.Dispose();
        chambered.Dispose();

        Assert.Equal(1, source.DisposeCallCount);
    }

    #endregion

    #region ToChamberedEnumerableAsync (async - net8+ only)

    [Fact]
    public async Task ToChamberedEnumerableAsync_BasicAsyncEnumerable_ReturnsChamberCount()
    {
        async IAsyncEnumerable<dynamic> GetItems()
        {
            yield return "a";
            yield return "b";
            yield return "c";
            yield return "d";
        }

        var result = await GetItems().ToChamberedEnumerableAsync(chamberSize: 2);

        Assert.Equal(2, result.ChamberedCount);

        var all = new List<dynamic>();
        await foreach (var item in result)
        {
            all.Add(item);
        }
        Assert.Equal(4, all.Count);
    }

    [Fact]
    public async Task ToChamberedEnumerableAsync_NullAsync_ReturnsZeroChamber()
    {
        IAsyncEnumerable<dynamic>? items = null;
        var result = await items.ToChamberedEnumerableAsync();

        Assert.Equal(0, result.ChamberedCount);
    }

    // Async stand-in for an IAsyncEnumerable source that owns external resources
    // (e.g. Com.H.Data.Common's DbAsyncQueryResult<T>). Records DisposeAsync() calls.
    private sealed class DisposableAsyncEnumerable : IAsyncEnumerable<object>, IAsyncDisposable
    {
        private readonly IEnumerable<object> _inner;
        public bool Disposed { get; private set; }
        public int DisposeCallCount { get; private set; }

        public DisposableAsyncEnumerable(IEnumerable<object> inner) => _inner = inner;

        public async IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _inner)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return item;
                await Task.Yield();
            }
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            DisposeCallCount++;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task ToChamberedEnumerableAsync_MoreRowsThanChambered_DisposingChamberedDisposesSource()
    {
        var source = new DisposableAsyncEnumerable(new object[] { "a", "b", "c", "d" });

        var chambered = await source.ToChamberedEnumerableAsync(chamberSize: 2);
        Assert.Equal(2, chambered.ChamberedCount);
        Assert.False(chambered.WasExhausted(2));
        Assert.False(source.Disposed);

        await chambered.DisposeAsync();

        Assert.True(source.Disposed,
            "DisposeAsync on ChamberedAsyncEnumerable should dispose the underlying source " +
            "even when more items exist past the chamber.");
    }

    [Fact]
    public async Task ToChamberedEnumerableAsync_Exhausted_DisposingChamberedDisposesSource()
    {
        var source = new DisposableAsyncEnumerable(new object[] { "a" });

        var chambered = await source.ToChamberedEnumerableAsync(chamberSize: 5);
        Assert.Equal(1, chambered.ChamberedCount);
        Assert.True(chambered.WasExhausted(5));

        await chambered.DisposeAsync();

        Assert.True(source.Disposed);
    }

    [Fact]
    public async Task ToChamberedEnumerableAsync_DisposeIsIdempotent()
    {
        var source = new DisposableAsyncEnumerable(new object[] { "a", "b", "c" });

        var chambered = await source.ToChamberedEnumerableAsync(chamberSize: 2);
        await chambered.DisposeAsync();
        await chambered.DisposeAsync();

        Assert.Equal(1, source.DisposeCallCount);
    }

    #endregion

    #region ToListAsync (IAsyncEnumerable extension)

    [Fact]
    public async Task ToListAsync_ConvertsAsyncEnumerableToList()
    {
        async IAsyncEnumerable<int> GetNumbers()
        {
            yield return 1;
            yield return 2;
            yield return 3;
        }

        var list = await LinqAsyncExtensions.ToListAsync(GetNumbers());

        Assert.Equal(3, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(2, list[1]);
        Assert.Equal(3, list[2]);
    }

    [Fact]
    public async Task ToListAsync_EmptyAsyncEnumerable_ReturnsEmptyList()
    {
        async IAsyncEnumerable<string> GetItems()
        {
            yield break;
        }

        var list = await LinqAsyncExtensions.ToListAsync(GetItems());
        Assert.Empty(list);
    }

    [Fact]
    public async Task ToListAsync_WithCancellation_Cancels()
    {
        async IAsyncEnumerable<int> InfiniteNumbers(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            int i = 0;
            while (!ct.IsCancellationRequested)
            {
                yield return i++;
                await Task.Delay(1, ct);
            }
        }

        using var cts = new CancellationTokenSource(50);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await LinqAsyncExtensions.ToListAsync(InfiniteNumbers(), cts.Token));
    }

    #endregion
}
