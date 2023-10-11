using System.Text;
using System.Text.Json;
using Xunit;

namespace Temporalio.Tests
{
    public static class AssertMore
    {
        public static Task EventuallyAsync(
            Func<Task> func, TimeSpan? interval = null, int iterations = 15) =>
            EventuallyAsync(
                async () =>
                {
                    await func();
                    return ValueTuple.Create();
                },
                interval,
                iterations);

        public static async Task<T> EventuallyAsync<T>(
            Func<Task<T>> func, TimeSpan? interval = null, int iterations = 15)
        {
            var tick = interval ?? TimeSpan.FromMilliseconds(300);
            for (var i = 0; ; i++)
            {
                try
                {
                    return await func();
                }
                catch (Xunit.Sdk.XunitException)
                {
                    if (i >= iterations - 1)
                    {
                        throw;
                    }
                }
                await Task.Delay(tick);
            }
        }

        public static Task EqualEventuallyAsync<T>(
            T expected, Func<Task<T>> func, TimeSpan? interval = null, int iterations = 15)
        {
            return EventuallyAsync(
                async () => Assert.Equal(expected, await func()), interval, iterations);
        }

        public static void DateTimeFromUtcNow(DateTime actual, TimeSpan fromNow, double maxDeltaSeconds = 30.0)
        {
            var expected = DateTime.UtcNow + fromNow;
            var delta = TimeSpan.FromSeconds(maxDeltaSeconds);
            Assert.InRange(actual, expected - delta, expected + delta);
        }

        /// <summary>
        /// Assert every item passes at least one action.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="items">Items.</param>
        /// <param name="actions">Actions.</param>
        public static void Every<T>(IEnumerable<T> items, params Action<T>[] actions)
        {
            foreach (var item in items)
            {
                var found = false;
                foreach (var action in actions)
                {
                    try
                    {
                        action(item);
                        found = true;
                        break;
                    }
                    catch (Xunit.Sdk.XunitException)
                    {
                    }
                }
                Assert.True(found, $"Item {item} had no match");
            }
        }

        public static void EqualAsJson(object? expected, object? actual) =>
            JsonEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

        // TODO(cretz): From https://github.com/dotnet/runtime/blob/fd9f52098bba9e88269b2b147a45b8f60e4b8d0d/src/libraries/System.Text.Json/tests/Common/JsonTestHelper.cs
        //  pending https://github.com/dotnet/runtime/issues/33388
        public static void JsonEqual(string expected, string actual)
        {
            using JsonDocument expectedDom = JsonDocument.Parse(expected);
            using JsonDocument actualDom = JsonDocument.Parse(actual);
            JsonEqual(expectedDom.RootElement, actualDom.RootElement);
        }

        public static void JsonEqual(JsonElement expected, JsonElement actual)
        {
            JsonEqualCore(expected, actual, new());
        }

        private static void JsonEqualCore(
            JsonElement expected,
            JsonElement actual,
            Stack<object> path)
        {
            JsonValueKind valueKind = expected.ValueKind;
            AssertTrue(passCondition: valueKind == actual.ValueKind);

            switch (valueKind)
            {
                case JsonValueKind.Object:
                    var expectedProperties = new List<string>();
                    foreach (JsonProperty property in expected.EnumerateObject())
                    {
                        expectedProperties.Add(property.Name);
                    }

                    var actualProperties = new List<string>();
                    foreach (JsonProperty property in actual.EnumerateObject())
                    {
                        actualProperties.Add(property.Name);
                    }

                    foreach (var property in expectedProperties.Except(actualProperties))
                    {
                        AssertTrue(
                            passCondition: false,
                            $"Property \"{property}\" missing from actual object.");
                    }

                    foreach (var property in actualProperties.Except(expectedProperties))
                    {
                        AssertTrue(
                            passCondition: false,
                            $"Actual object defines additional property \"{property}\".");
                    }

                    foreach (string name in expectedProperties)
                    {
                        path.Push(name);
                        JsonEqualCore(expected.GetProperty(name), actual.GetProperty(name), path);
                        path.Pop();
                    }
                    break;
                case JsonValueKind.Array:
                    JsonElement.ArrayEnumerator expectedEnumerator = expected.EnumerateArray();
                    JsonElement.ArrayEnumerator actualEnumerator = actual.EnumerateArray();

                    int i = 0;
                    while (expectedEnumerator.MoveNext())
                    {
                        AssertTrue(
                            passCondition: actualEnumerator.MoveNext(),
                            "Actual array contains fewer elements.");
                        path.Push(i++);
                        JsonEqualCore(expectedEnumerator.Current, actualEnumerator.Current, path);
                        path.Pop();
                    }

                    AssertTrue(
                        passCondition: !actualEnumerator.MoveNext(),
                        "Actual array contains additional elements.");
                    break;
                case JsonValueKind.String:
                    AssertTrue(passCondition: expected.GetString() == actual.GetString());
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    AssertTrue(passCondition: expected.GetRawText() == actual.GetRawText());
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected JsonValueKind: JsonValueKind.{valueKind}.");
            }

            void AssertTrue(bool passCondition, string? message = null)
            {
                if (!passCondition)
                {
                    message ??= "Expected JSON does not match actual value";
                    Assert.Fail(
                        $"{message}\nExpected JSON: {expected}\n  Actual JSON: {actual}\n  in JsonPath: {BuildJsonPath(path)}");
                }

                // TODO replace with JsonPath implementation for JsonElement
                // cf. https://github.com/dotnet/runtime/issues/31068
                static string BuildJsonPath(Stack<object> path)
                {
                    var sb = new StringBuilder("$");
                    foreach (object node in path.Reverse())
                    {
                        string pathNode = node is string propertyName
                            ? "." + propertyName
                            : $"[{(int)node}]";

                        sb.Append(pathNode);
                    }
                    return sb.ToString();
                }
            }
        }
    }
}
