namespace Temporalio.Tests.Common;

using Temporalio.Common;
using Xunit;

public class DeterministicRandomTests
{
    [Fact]
    public void DeterministicRandom_RandomCalls_AreDeterministic()
    {
        // Create some ints, doubles, and bytes. Confirm that they are not the same, and that they
        // are not the same for different seed but are the same for the same seed.
        var rand = new DeterministicRandom(1234);
        var randDiff = new DeterministicRandom(2345);
        var randSame = new DeterministicRandom(1234);

        // Ints
        var int1 = rand.Next();
        Assert.InRange(int1, 0, int.MaxValue - 1);
        var int2 = rand.Next();
        Assert.InRange(int2, 0, int.MaxValue - 1);
        Assert.NotEqual(int1, int2);
        Assert.NotEqual(int1, randDiff.Next());
        Assert.NotEqual(int2, randDiff.Next());
        Assert.Equal(int1, randSame.Next());
        Assert.Equal(int2, randSame.Next());

        // Doubles
        var double1 = rand.NextDouble();
        Assert.InRange(double1, 0, 1.1);
        var double2 = rand.NextDouble();
        Assert.InRange(double2, 0, 1.1);
        Assert.NotEqual(double1, double2);
        Assert.NotEqual(double1, randDiff.NextDouble());
        Assert.NotEqual(double2, randDiff.NextDouble());
        Assert.Equal(double1, randSame.NextDouble());
        Assert.Equal(double2, randSame.NextDouble());

        // Bytes
        var bytes1 = new byte[30];
        var bytes2 = new byte[30];
        var bytes3 = new byte[30];
        var bytes4 = new byte[30];
        var bytes5 = new byte[30];
        var bytes6 = new byte[30];
        rand.NextBytes(bytes1);
        rand.NextBytes(bytes2);
        Assert.NotEqual(bytes1, bytes2);
        randDiff.NextBytes(bytes3);
        Assert.NotEqual(bytes1, bytes3);
        randDiff.NextBytes(bytes4);
        Assert.NotEqual(bytes2, bytes4);
        randSame.NextBytes(bytes5);
        Assert.Equal(bytes1, bytes5);
        randSame.NextBytes(bytes6);
        Assert.Equal(bytes2, bytes6);
    }
}