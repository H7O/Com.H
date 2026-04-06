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
