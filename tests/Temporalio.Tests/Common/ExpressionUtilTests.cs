namespace Temporalio.Tests.Common;

using System.Reflection;
using Temporalio.Common;
using Xunit;

public class ExpressionUtilTests
{
    [Fact]
    public void ExtractTree_DifferentCallExpressions_ExtractProperly()
    {
        AssertCall(
            ExpressionUtil.ExtractCall(() => Temp1.DoSomethingStatic(null)),
            "DoSomethingStatic",
            (object?)null);
        AssertCall(
            ExpressionUtil.ExtractCall(() => Temp1.DoSomethingStatic("foo")),
            "DoSomethingStatic",
            "foo");
        AssertCall(
            ExpressionUtil.ExtractCall(() => Temp1.DoSomethingStatic(Temp1.DoSomethingStatic(null))),
            "DoSomethingStatic",
            (object?)null);
        var strOutsideClosure = "foo";
        AssertCall(
            ExpressionUtil.ExtractCall(() => Temp1.DoSomethingStatic(strOutsideClosure)),
            "DoSomethingStatic",
            "foo");
        AssertCall(
            ExpressionUtil.ExtractCall(() => Temp1.DoSomethingStatic(strOutsideClosure + "bar")),
            "DoSomethingStatic",
            "foobar");
        AssertCall(
            ExpressionUtil.ExtractCall((Temp1 temp1) => temp1.DoSomethingInstance("foo")),
            "DoSomethingInstance",
            "foo");

        AssertBadCall(
            () => ExpressionUtil.ExtractCall(() => "whatever"),
            "must be a single method call");
        AssertBadCall(
            () => ExpressionUtil.ExtractCall((Temp1 temp1) => Temp1.DoSomethingStatic("foo")),
            "must not have a lambda parameter");
        AssertBadCall(
            () => ExpressionUtil.ExtractCall(() => new Temp1().DoSomethingInstance("foo")),
            "must have a single lambda parameter");
        var outside = new Temp1();
        AssertBadCall(
            () => ExpressionUtil.ExtractCall((Temp1 notThis) => outside.DoSomethingInstance("foo")),
            "must have a single lambda parameter");
    }

    private static void AssertCall(
        (MethodInfo Method, IReadOnlyCollection<object?> Args) call,
        string name,
        params object?[] args)
    {
        Assert.Equal(name, call.Method.Name);
        Assert.Equal(args, call.Args);
    }

    private static void AssertBadCall(
        Action action,
        string contains)
    {
        var exc = Assert.Throws<ArgumentException>(action);
        Assert.Contains(contains, exc.Message);
    }

    public class Temp1
    {
        public static string? DoSomethingStatic(string? arg) => arg;

        public string? DoSomethingInstance(string? arg) => arg;
    }
}