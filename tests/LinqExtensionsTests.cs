using Com.H.Linq;

namespace Com.H.Tests;

public class LinqExtensionsTests
{
    #region AggregateUntil

    [Fact]
    public void AggregateUntil_StopsWhenConditionMet()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Sum until running total >= 6
        var result = numbers.AggregateUntil(
            0,
            (acc, item) => acc + item,
            (acc, _) => acc >= 6);

        Assert.Equal(6, result);
    }

    [Fact]
    public void AggregateUntil_NeverTriggered_ProcessesAll()
    {
        var numbers = new[] { 1, 2, 3 };

        var result = numbers.AggregateUntil(
            0,
            (acc, item) => acc + item,
            (acc, _) => acc >= 100);

        Assert.Equal(6, result);
    }

    [Fact]
    public void AggregateUntil_EmptySource_ReturnsSeed()
    {
        var empty = Enumerable.Empty<int>();

        var result = empty.AggregateUntil(
            42,
            (acc, item) => acc + item,
            (acc, _) => true);

        Assert.Equal(42, result);
    }

    [Fact]
    public void AggregateUntil_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<int>)null!).AggregateUntil(
                0,
                (acc, item) => acc + item,
                (acc, _) => false));
    }

    [Fact]
    public void AggregateUntil_NullFunc_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new[] { 1 }.AggregateUntil<int, int>(
                0,
                null!,
                (acc, _) => false));
    }

    #endregion

    #region AggregateWhile

    [Fact]
    public void AggregateWhile_StopsWhenConditionFalse()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };

        // Sum while running total < 6
        // seed=0 < 6 true → +1=1, 1 < 6 true → +2=3, 3 < 6 true → +3=6, 6 < 6 false → return 6
        var result = numbers.AggregateWhile(
            0,
            (acc, item) => acc + item,
            (acc, _) => acc < 6);

        Assert.Equal(6, result);
    }

    [Fact]
    public void AggregateWhile_AlwaysTrue_ProcessesAll()
    {
        var numbers = new[] { 1, 2, 3 };

        var result = numbers.AggregateWhile(
            0,
            (acc, item) => acc + item,
            (acc, _) => true);

        Assert.Equal(6, result);
    }

    [Fact]
    public void AggregateWhile_NullSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IEnumerable<int>)null!).AggregateWhile(
                0,
                (acc, item) => acc + item,
                (acc, _) => true));
    }

    #endregion

    #region FindDescendants

    [Fact]
    public void FindDescendants_SimpleHierarchy_FindsNodes()
    {
        // Create a simple tree: root -> child1 -> leaf1, root -> child2
        var leaf1 = new TreeNode("leaf1");
        var child1 = new TreeNode("child1", leaf1);
        var child2 = new TreeNode("child2");
        var root = new TreeNode("root", child1, child2);

        Func<TreeNode, string, IEnumerable<TreeNode?>?> findChildren =
            (node, name) => node.Children.Where(c => c.Name == name);

        // With checkRoot, path includes root name
        var result = root.FindDescendants(
            "root/child1/leaf1",
            findChildren,
            new[] { "/" },
            (node, name) => node.Name == name);

        Assert.NotNull(result);
        var list = result!.ToList();
        Assert.Single(list);
        Assert.Equal("leaf1", list[0]!.Name);
    }

    [Fact]
    public void FindDescendants_WithoutCheckRoot_TraversesFromNode()
    {
        var leaf1 = new TreeNode("leaf1");
        var child1 = new TreeNode("child1", leaf1);
        var root = new TreeNode("root", child1);

        Func<TreeNode, string, IEnumerable<TreeNode?>?> findChildren =
            (node, name) => node.Children.Where(c => c.Name == name);

        // Without checkRoot, path starts from children of traversableItem
        var result = root.FindDescendants(
            "child1/leaf1",
            findChildren,
            new[] { "/" });

        Assert.NotNull(result);
        var list = result!.ToList();
        Assert.Single(list);
        Assert.Equal("leaf1", list[0]!.Name);
    }

    [Fact]
    public void FindDescendants_NullPath_ReturnsDefault()
    {
        var node = new TreeNode("root");

        Func<TreeNode, string, IEnumerable<TreeNode?>?> findChildren =
            (_, _) => null;

        var result = node.FindDescendants(
            null!,
            findChildren,
            new[] { "/" });

        Assert.Null(result);
    }

    private record TreeNode(string Name, params TreeNode[] Children);

    #endregion
}
