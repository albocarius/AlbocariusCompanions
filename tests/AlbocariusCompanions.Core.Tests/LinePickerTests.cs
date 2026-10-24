using AlbocariusCompanions.Core.Dialogs;

namespace AlbocariusCompanions.Core.Tests;

public class LinePickerTests
{
    private sealed class FixedRandom : IRandomSource
    {
        private readonly Queue<int> _values;
        public FixedRandom(params int[] values) => _values = new Queue<int>(values);
        public int Calls { get; private set; }
        public int LastMax { get; private set; }
        public int Next(int maxExclusive)
        {
            Calls++;
            LastMax = maxExclusive;
            return _values.Dequeue();
        }
    }

    [Fact]
    public void Empty_slot_gives_null() => Assert.Null(LinePicker.Pick(Array.Empty<string>(), new FixedRandom()));

    [Fact]
    public void Single_line_is_returned_without_a_random_call()
    {
        var random = new FixedRandom();
        Assert.Equal("only", LinePicker.Pick(new[] { "only" }, random));
        Assert.Equal(0, random.Calls);
    }

    [Fact]
    public void Several_lines_use_the_random_source_over_the_whole_range()
    {
        var lines = new[] { "a", "b", "c" };
        var random = new FixedRandom(2, 0, 1);

        Assert.Equal(new[] { "c", "a", "b" }, new[] { LinePicker.Pick(lines, random), LinePicker.Pick(lines, random), LinePicker.Pick(lines, random) });
        Assert.Equal(3, random.LastMax);
    }

    [Theory]
    [InlineData(-5, "a")]
    [InlineData(99, "b")]
    public void Out_of_range_values_are_clamped(int value, string expected) =>
        Assert.Equal(expected, LinePicker.Pick(new[] { "a", "b" }, new FixedRandom(value)));

    [Fact]
    public void System_source_covers_every_line_eventually()
    {
        var random = new SystemRandomSource(seed: 42);
        var seen = Enumerable.Range(0, 200).Select(_ => LinePicker.Pick(new[] { "a", "b", "c" }, random)).ToHashSet();
        Assert.Equal(3, seen.Count);
    }
}
