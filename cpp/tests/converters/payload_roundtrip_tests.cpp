#include <gtest/gtest.h>

#include <any>
#include <cstdint>
#include <string>
#include <typeindex>
#include <vector>

#include "temporalio/activities/activity.h"
#include "temporalio/converters/data_converter.h"
#include "temporalio/workflows/workflow.h"
#include "temporalio/workflows/workflow_definition.h"

using namespace temporalio::converters;

// ===========================================================================
// 1. payload_to_any -> decode_payload_value round-trip tests
//
// These test the core path that was broken by the double-encoding bug:
//   converter.to_payload_typed<T>(value)
//     -> wrap in std::any containing converters::Payload
//       -> decode_payload_value<T>(any_containing_payload)
//         -> value of type T
// ===========================================================================

class PayloadRoundTripTest : public ::testing::Test {
protected:
    DefaultPayloadConverter converter;
};

// -- json/plain round trips --

TEST_F(PayloadRoundTripTest, StringViaPayloadAny) {
    std::string original = "hello world";
    Payload payload = converter.to_payload_typed(original);

    // Wrap as std::any(Payload) -- simulates what the worker does
    std::any wrapped = std::any(payload);

    // decode_payload_value should detect Payload inside std::any and decode
    auto result = decode_payload_value<std::string>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, IntViaPayloadAny) {
    int original = 42;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<int>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, NegativeIntViaPayloadAny) {
    int original = -999;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<int>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, Int64ViaPayloadAny) {
    int64_t original = 9876543210LL;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<int64_t>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, Uint64ViaPayloadAny) {
    uint64_t original = 18446744073709551000ULL;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<uint64_t>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, DoubleViaPayloadAny) {
    double original = 3.14159265;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<double>(wrapped, &converter);
    EXPECT_DOUBLE_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, FloatViaPayloadAny) {
    float original = 2.718f;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<float>(wrapped, &converter);
    EXPECT_FLOAT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, BoolTrueViaPayloadAny) {
    Payload payload = converter.to_payload_typed(true);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<bool>(wrapped, &converter);
    EXPECT_TRUE(result);
}

TEST_F(PayloadRoundTripTest, BoolFalseViaPayloadAny) {
    Payload payload = converter.to_payload_typed(false);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<bool>(wrapped, &converter);
    EXPECT_FALSE(result);
}

// -- binary/plain round trips --

TEST_F(PayloadRoundTripTest, BytesViaPayloadAny) {
    std::vector<uint8_t> original = {0xDE, 0xAD, 0xBE, 0xEF};
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result =
        decode_payload_value<std::vector<uint8_t>>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, EmptyBytesViaPayloadAny) {
    std::vector<uint8_t> original;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result =
        decode_payload_value<std::vector<uint8_t>>(wrapped, &converter);
    EXPECT_TRUE(result.empty());
}

// -- binary/null round trip --

TEST_F(PayloadRoundTripTest, NullViaPayloadAny) {
    std::any null_val;
    Payload payload = converter.to_payload(null_val);
    EXPECT_EQ(payload.metadata.at("encoding"), "binary/null");

    // Decoding a binary/null payload should give an empty std::any
    std::any wrapped = std::any(payload);
    auto result = converter.to_value(payload, std::type_index(typeid(void)));
    EXPECT_FALSE(result.has_value());
}

// -- decode_payload_value with direct typed values (no Payload wrapper) --

TEST_F(PayloadRoundTripTest, DirectStringValue) {
    std::any val = std::any(std::string("direct"));
    auto result = decode_payload_value<std::string>(val, &converter);
    EXPECT_EQ(result, "direct");
}

TEST_F(PayloadRoundTripTest, DirectIntValue) {
    std::any val = std::any(100);
    auto result = decode_payload_value<int>(val, &converter);
    EXPECT_EQ(result, 100);
}

TEST_F(PayloadRoundTripTest, DirectBoolValue) {
    std::any val = std::any(true);
    auto result = decode_payload_value<bool>(val, &converter);
    EXPECT_TRUE(result);
}

// -- decode_payload_value with default converter (nullptr) --

TEST_F(PayloadRoundTripTest, PayloadAnyWithDefaultConverter) {
    std::string original = "default converter test";
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    // Pass nullptr for converter -- should use internal DefaultPayloadConverter
    auto result = decode_payload_value<std::string>(wrapped, nullptr);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, PayloadAnyNoConverterArg) {
    int original = 77;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    // Use the default argument (no converter parameter at all)
    auto result = decode_payload_value<int>(wrapped);
    EXPECT_EQ(result, original);
}

// -- Edge case: empty string --

TEST_F(PayloadRoundTripTest, EmptyStringViaPayloadAny) {
    std::string original;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<std::string>(wrapped, &converter);
    EXPECT_EQ(result, original);
    EXPECT_TRUE(result.empty());
}

// -- Edge case: string with special characters --

TEST_F(PayloadRoundTripTest, SpecialCharsStringViaPayloadAny) {
    std::string original = "line1\nline2\ttab \"quotes\" \\backslash";
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<std::string>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

// -- Edge case: zero int --

TEST_F(PayloadRoundTripTest, ZeroIntViaPayloadAny) {
    int original = 0;
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<int>(wrapped, &converter);
    EXPECT_EQ(result, 0);
}

// -- Edge case: max/min numeric values --

TEST_F(PayloadRoundTripTest, MaxIntViaPayloadAny) {
    int original = std::numeric_limits<int>::max();
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<int>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

TEST_F(PayloadRoundTripTest, MinIntViaPayloadAny) {
    int original = std::numeric_limits<int>::min();
    Payload payload = converter.to_payload_typed(original);
    std::any wrapped = std::any(payload);

    auto result = decode_payload_value<int>(wrapped, &converter);
    EXPECT_EQ(result, original);
}

// ===========================================================================
// 2. Payload encoding correctness tests
//
// Verify that the encoding metadata and data bytes are correct
// for each converter type.
// ===========================================================================

TEST_F(PayloadRoundTripTest, StringPayloadEncoding) {
    Payload payload = converter.to_payload_typed(std::string("test"));
    EXPECT_EQ(payload.metadata.at("encoding"), "json/plain");
    std::string data(payload.data.begin(), payload.data.end());
    EXPECT_EQ(data, "\"test\"");
}

TEST_F(PayloadRoundTripTest, IntPayloadEncoding) {
    Payload payload = converter.to_payload_typed(42);
    EXPECT_EQ(payload.metadata.at("encoding"), "json/plain");
    std::string data(payload.data.begin(), payload.data.end());
    EXPECT_EQ(data, "42");
}

TEST_F(PayloadRoundTripTest, BoolPayloadEncoding) {
    Payload payload = converter.to_payload_typed(true);
    EXPECT_EQ(payload.metadata.at("encoding"), "json/plain");
    std::string data(payload.data.begin(), payload.data.end());
    EXPECT_EQ(data, "true");
}

TEST_F(PayloadRoundTripTest, BytesPayloadEncoding) {
    std::vector<uint8_t> bytes = {0x01, 0x02, 0x03};
    Payload payload = converter.to_payload_typed(bytes);
    EXPECT_EQ(payload.metadata.at("encoding"), "binary/plain");
    EXPECT_EQ(payload.data, bytes);
}

// ===========================================================================
// 3. Activity arg decoding tests
//
// Verify that ActivityDefinition correctly decodes converters::Payload
// arguments (simulating how the worker delivers args from the server).
// ===========================================================================

namespace {

// Helper to synchronously run a lazy coroutine task.
// Since Task<T> has initial_suspend=suspend_always, we must resume it.
template <typename T>
T run_task(temporalio::coro::Task<T> task) {
    auto handle = task.handle();
    // Resume the coroutine -- for simple non-suspending coroutines,
    // this will run to completion.
    handle.resume();
    // Now await_resume will give us the result
    return task.await_resume();
}

template <>
void run_task<void>(temporalio::coro::Task<void> task) {
    auto handle = task.handle();
    handle.resume();
    task.await_resume();
}

// Test activities

temporalio::coro::Task<std::string> echo_activity(std::string input) {
    co_return input;
}

temporalio::coro::Task<int> add_activity(int a, int b) {
    co_return a + b;
}

temporalio::coro::Task<bool> negate_activity(bool val) {
    co_return !val;
}

temporalio::coro::Task<double> double_it_activity(double val) {
    co_return val * 2.0;
}

temporalio::coro::Task<std::string> no_args_activity() {
    co_return std::string("no args");
}

}  // namespace

TEST(ActivityArgDecodingTest, SingleStringArgFromPayload) {
    auto def = temporalio::activities::ActivityDefinition::create(
        "echo", &echo_activity);

    DefaultPayloadConverter converter;
    Payload payload = converter.to_payload_typed(std::string("hello from payload"));

    // Wrap payload in std::any, like the worker does
    std::vector<std::any> args = {std::any(payload)};

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<std::string>(result), "hello from payload");
}

TEST(ActivityArgDecodingTest, TwoIntArgsFromPayload) {
    auto def = temporalio::activities::ActivityDefinition::create(
        "add", &add_activity);

    DefaultPayloadConverter converter;
    Payload p1 = converter.to_payload_typed(10);
    Payload p2 = converter.to_payload_typed(32);

    std::vector<std::any> args = {std::any(p1), std::any(p2)};

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<int>(result), 42);
}

TEST(ActivityArgDecodingTest, BoolArgFromPayload) {
    auto def = temporalio::activities::ActivityDefinition::create(
        "negate", &negate_activity);

    DefaultPayloadConverter converter;
    Payload payload = converter.to_payload_typed(true);

    std::vector<std::any> args = {std::any(payload)};

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<bool>(result), false);
}

TEST(ActivityArgDecodingTest, DoubleArgFromPayload) {
    auto def = temporalio::activities::ActivityDefinition::create(
        "double_it", &double_it_activity);

    DefaultPayloadConverter converter;
    Payload payload = converter.to_payload_typed(1.5);

    std::vector<std::any> args = {std::any(payload)};

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_DOUBLE_EQ(std::any_cast<double>(result), 3.0);
}

TEST(ActivityArgDecodingTest, NoArgsActivity) {
    auto def = temporalio::activities::ActivityDefinition::create(
        "no_args", &no_args_activity);

    std::vector<std::any> args;

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<std::string>(result), "no args");
}

TEST(ActivityArgDecodingTest, DirectTypedArgStillWorks) {
    // Verify that passing a direct typed value (not Payload) also works
    auto def = temporalio::activities::ActivityDefinition::create(
        "echo", &echo_activity);

    std::vector<std::any> args = {std::any(std::string("direct value"))};

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<std::string>(result), "direct value");
}

// ===========================================================================
// 4. Activity with lambda arg decoding
// ===========================================================================

TEST(ActivityArgDecodingTest, LambdaSingleArgFromPayload) {
    auto def = temporalio::activities::ActivityDefinition::create(
        "lambda_echo",
        [](std::string s) -> temporalio::coro::Task<std::string> {
            co_return "echo: " + s;
        });

    DefaultPayloadConverter converter;
    Payload payload = converter.to_payload_typed(std::string("lambda test"));

    std::vector<std::any> args = {std::any(payload)};

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<std::string>(result), "echo: lambda test");
}

TEST(ActivityArgDecodingTest, LambdaMultiArgFromPayload) {
    auto def = temporalio::activities::ActivityDefinition::create(
        "lambda_add",
        [](int a, int b) -> temporalio::coro::Task<int> {
            co_return a + b;
        });

    DefaultPayloadConverter converter;
    Payload p1 = converter.to_payload_typed(100);
    Payload p2 = converter.to_payload_typed(200);

    std::vector<std::any> args = {std::any(p1), std::any(p2)};

    auto task = def->execute(std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<int>(result), 300);
}

// ===========================================================================
// 5. Workflow definition arg decoding tests
//
// Test that WorkflowDefinition correctly decodes Payload args for
// the run function, signal handlers, and query handlers.
// These use Workflow::decode_value internally which delegates to
// decode_payload_value.
// ===========================================================================

namespace {

// A simple workflow class for testing
class TestWorkflow {
public:
    temporalio::coro::Task<std::string> run(std::string input) {
        last_input_ = input;
        co_return "result: " + input;
    }

    temporalio::coro::Task<void> on_signal(std::string msg) {
        signal_value_ = msg;
        co_return;
    }

    std::string get_status() const {
        return "status: " + signal_value_;
    }

    std::string get_query_with_arg(std::string prefix) const {
        return prefix + signal_value_;
    }

    std::string last_input_;
    std::string signal_value_;
};

class MultiArgWorkflow {
public:
    temporalio::coro::Task<int> run(int a, int b) {
        co_return a + b;
    }
};

// Minimal WorkflowContext that provides a DataConverter
// Used so Workflow::decode_value can find a converter.
class TestWorkflowContext : public temporalio::workflows::WorkflowContext {
public:
    TestWorkflowContext() : dc_(DataConverter::default_instance()) {}

    const temporalio::workflows::WorkflowInfo& info() const override {
        return info_;
    }
    std::stop_token cancellation_token() const override { return {}; }
    bool continue_as_new_suggested() const override { return false; }
    bool all_handlers_finished() const override { return true; }
    std::chrono::system_clock::time_point utc_now() const override {
        return std::chrono::system_clock::now();
    }
    std::mt19937& random() override { return rng_; }
    int current_history_length() const override { return 0; }
    int current_history_size() const override { return 0; }
    bool is_replaying() const override { return false; }
    const temporalio::workflows::WorkflowUpdateInfo* current_update_info()
        const override {
        return nullptr;
    }
    bool patched(const std::string&) override { return false; }
    void deprecate_patch(const std::string&) override {}

    temporalio::coro::Task<void> start_timer(
        std::chrono::milliseconds,
        std::stop_token) override {
        co_return;
    }

    temporalio::coro::Task<bool> register_condition(
        std::function<bool()>,
        std::optional<std::chrono::milliseconds>,
        std::stop_token) override {
        co_return true;
    }

    temporalio::coro::Task<std::any> schedule_activity(
        const std::string&,
        std::vector<std::any>,
        const temporalio::workflows::ActivityOptions&) override {
        co_return std::any{};
    }

    const DataConverter* data_converter() const override { return &dc_; }

private:
    temporalio::workflows::WorkflowInfo info_ = [] {
        temporalio::workflows::WorkflowInfo wi;
        wi.workflow_type = "TestWorkflow";
        wi.workflow_id = "test-id";
        wi.run_id = "test-run-id";
        wi.task_queue = "default";
        wi.namespace_ = "default";
        return wi;
    }();
    std::mt19937 rng_;
    DataConverter dc_;
};

}  // namespace

TEST(WorkflowArgDecodingTest, RunFuncSingleStringFromPayload) {
    auto def = temporalio::workflows::WorkflowDefinition::create<TestWorkflow>(
                   "TestWorkflow")
                   .run(&TestWorkflow::run)
                   .build();

    // Set up a workflow context so decode_value can find a converter
    TestWorkflowContext ctx;
    temporalio::workflows::WorkflowContextScope scope(&ctx);

    auto instance = def->create_instance();
    auto* wf = static_cast<TestWorkflow*>(instance.get());

    DefaultPayloadConverter converter;
    Payload payload =
        converter.to_payload_typed(std::string("workflow input"));

    std::vector<std::any> args = {std::any(payload)};

    auto task = def->run_func()(wf, std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<std::string>(result), "result: workflow input");
}

TEST(WorkflowArgDecodingTest, RunFuncMultipleIntArgsFromPayload) {
    auto def =
        temporalio::workflows::WorkflowDefinition::create<MultiArgWorkflow>(
            "MultiArgWorkflow")
            .run(&MultiArgWorkflow::run)
            .build();

    TestWorkflowContext ctx;
    temporalio::workflows::WorkflowContextScope scope(&ctx);

    auto instance = def->create_instance();
    auto* wf = static_cast<MultiArgWorkflow*>(instance.get());

    DefaultPayloadConverter converter;
    Payload p1 = converter.to_payload_typed(10);
    Payload p2 = converter.to_payload_typed(20);

    std::vector<std::any> args = {std::any(p1), std::any(p2)};

    auto task = def->run_func()(wf, std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<int>(result), 30);
}

TEST(WorkflowArgDecodingTest, RunFuncDirectTypedArgs) {
    auto def = temporalio::workflows::WorkflowDefinition::create<TestWorkflow>(
                   "TestWorkflow")
                   .run(&TestWorkflow::run)
                   .build();

    TestWorkflowContext ctx;
    temporalio::workflows::WorkflowContextScope scope(&ctx);

    auto instance = def->create_instance();
    auto* wf = static_cast<TestWorkflow*>(instance.get());

    // Pass directly typed value (no Payload wrapping)
    std::vector<std::any> args = {std::any(std::string("direct"))};

    auto task = def->run_func()(wf, std::move(args));
    auto result = run_task(std::move(task));
    EXPECT_EQ(std::any_cast<std::string>(result), "result: direct");
}

// ===========================================================================
// 6. Signal handler arg decoding tests
// ===========================================================================

TEST(SignalArgDecodingTest, SignalStringFromPayload) {
    auto def = temporalio::workflows::WorkflowDefinition::create<TestWorkflow>(
                   "TestWorkflow")
                   .run(&TestWorkflow::run)
                   .signal("my_signal", &TestWorkflow::on_signal)
                   .build();

    TestWorkflowContext ctx;
    temporalio::workflows::WorkflowContextScope scope(&ctx);

    auto instance = def->create_instance();
    auto* wf = static_cast<TestWorkflow*>(instance.get());

    DefaultPayloadConverter converter;
    Payload payload =
        converter.to_payload_typed(std::string("signal value"));

    auto& signal_def = def->signals().at("my_signal");
    std::vector<std::any> args = {std::any(payload)};

    auto task = signal_def.handler(wf, std::move(args));
    run_task(std::move(task));

    EXPECT_EQ(wf->signal_value_, "signal value");
}

TEST(SignalArgDecodingTest, SignalDirectTypedArg) {
    auto def = temporalio::workflows::WorkflowDefinition::create<TestWorkflow>(
                   "TestWorkflow")
                   .run(&TestWorkflow::run)
                   .signal("my_signal", &TestWorkflow::on_signal)
                   .build();

    TestWorkflowContext ctx;
    temporalio::workflows::WorkflowContextScope scope(&ctx);

    auto instance = def->create_instance();
    auto* wf = static_cast<TestWorkflow*>(instance.get());

    std::vector<std::any> args = {std::any(std::string("direct signal"))};

    auto task = def->signals().at("my_signal").handler(wf, std::move(args));
    run_task(std::move(task));

    EXPECT_EQ(wf->signal_value_, "direct signal");
}

// ===========================================================================
// 7. Query handler arg decoding tests
// ===========================================================================

TEST(QueryArgDecodingTest, QueryNoArgs) {
    auto def = temporalio::workflows::WorkflowDefinition::create<TestWorkflow>(
                   "TestWorkflow")
                   .run(&TestWorkflow::run)
                   .query("get_status", &TestWorkflow::get_status)
                   .build();

    TestWorkflowContext ctx;
    temporalio::workflows::WorkflowContextScope scope(&ctx);

    auto instance = def->create_instance();
    auto* wf = static_cast<TestWorkflow*>(instance.get());
    wf->signal_value_ = "active";

    auto& query_def = def->queries().at("get_status");
    std::vector<std::any> args;

    auto result = query_def.handler(wf, std::move(args));
    EXPECT_EQ(std::any_cast<std::string>(result), "status: active");
}

TEST(QueryArgDecodingTest, QueryWithArgFromPayload) {
    auto def = temporalio::workflows::WorkflowDefinition::create<TestWorkflow>(
                   "TestWorkflow")
                   .run(&TestWorkflow::run)
                   .query("get_query_with_arg",
                          &TestWorkflow::get_query_with_arg)
                   .build();

    TestWorkflowContext ctx;
    temporalio::workflows::WorkflowContextScope scope(&ctx);

    auto instance = def->create_instance();
    auto* wf = static_cast<TestWorkflow*>(instance.get());
    wf->signal_value_ = "world";

    DefaultPayloadConverter converter;
    Payload payload = converter.to_payload_typed(std::string("hello "));

    auto& query_def = def->queries().at("get_query_with_arg");
    std::vector<std::any> args = {std::any(payload)};

    auto result = query_def.handler(wf, std::move(args));
    EXPECT_EQ(std::any_cast<std::string>(result), "hello world");
}

// ===========================================================================
// 8. Activity result decoding tests
//
// Verify that when an activity returns a value, the result can be
// decoded from std::any (simulating execute_activity<T> pattern).
// ===========================================================================

TEST(ActivityResultDecodingTest, ResultDecodedViaPayload) {
    // Simulate: activity returns a value, worker serializes to Payload,
    // client receives Payload in std::any, uses decode_payload_value<T>.
    DefaultPayloadConverter converter;
    int activity_result = 42;
    Payload result_payload = converter.to_payload_typed(activity_result);

    // Worker puts Payload in std::any for the caller
    std::any result_any = std::any(result_payload);

    // Client decodes the result
    auto decoded = decode_payload_value<int>(result_any, &converter);
    EXPECT_EQ(decoded, 42);
}

TEST(ActivityResultDecodingTest, StringResultDecodedViaPayload) {
    DefaultPayloadConverter converter;
    Payload result_payload =
        converter.to_payload_typed(std::string("activity done"));
    std::any result_any = std::any(result_payload);

    auto decoded = decode_payload_value<std::string>(result_any, &converter);
    EXPECT_EQ(decoded, "activity done");
}

TEST(ActivityResultDecodingTest, BoolResultDecodedViaPayload) {
    DefaultPayloadConverter converter;
    Payload result_payload = converter.to_payload_typed(true);
    std::any result_any = std::any(result_payload);

    auto decoded = decode_payload_value<bool>(result_any, &converter);
    EXPECT_TRUE(decoded);
}

TEST(ActivityResultDecodingTest, DoubleResultDecodedViaPayload) {
    DefaultPayloadConverter converter;
    double original = 99.99;
    Payload result_payload = converter.to_payload_typed(original);
    std::any result_any = std::any(result_payload);

    auto decoded = decode_payload_value<double>(result_any, &converter);
    EXPECT_DOUBLE_EQ(decoded, 99.99);
}

TEST(ActivityResultDecodingTest, BytesResultDecodedViaPayload) {
    DefaultPayloadConverter converter;
    std::vector<uint8_t> original = {0xCA, 0xFE};
    Payload result_payload = converter.to_payload_typed(original);
    std::any result_any = std::any(result_payload);

    auto decoded =
        decode_payload_value<std::vector<uint8_t>>(result_any, &converter);
    EXPECT_EQ(decoded, original);
}

// ===========================================================================
// 9. DataConverter integration round-trip
//
// Test using the DataConverter::default_instance() to encode and decode.
// ===========================================================================

TEST(DataConverterRoundTripTest, FullRoundTrip) {
    auto dc = DataConverter::default_instance();
    auto& pc = *dc.payload_converter;

    std::string original = "full round trip";
    Payload payload = pc.to_payload_typed(original);

    // Simulate what the system does: wrap in std::any
    std::any wrapped = std::any(payload);

    // Decode
    auto decoded = decode_payload_value<std::string>(wrapped, &pc);
    EXPECT_EQ(decoded, original);
}

TEST(DataConverterRoundTripTest, MultipleTypesSequential) {
    auto dc = DataConverter::default_instance();
    auto& pc = *dc.payload_converter;

    // Encode multiple values of different types
    auto p_str = pc.to_payload_typed(std::string("text"));
    auto p_int = pc.to_payload_typed(42);
    auto p_bool = pc.to_payload_typed(true);
    auto p_dbl = pc.to_payload_typed(3.14);

    // Decode each one
    EXPECT_EQ(decode_payload_value<std::string>(std::any(p_str), &pc), "text");
    EXPECT_EQ(decode_payload_value<int>(std::any(p_int), &pc), 42);
    EXPECT_EQ(decode_payload_value<bool>(std::any(p_bool), &pc), true);
    EXPECT_DOUBLE_EQ(decode_payload_value<double>(std::any(p_dbl), &pc), 3.14);
}

// ===========================================================================
// 10. Error cases
// ===========================================================================

TEST(PayloadDecodingErrorTest, WrongTypeThrows) {
    DefaultPayloadConverter converter;
    Payload payload = converter.to_payload_typed(std::string("not a bool"));
    std::any wrapped = std::any(payload);

    // Attempting to decode a string payload as bool should throw
    EXPECT_THROW(decode_payload_value<bool>(wrapped, &converter),
                 std::invalid_argument);
}

TEST(PayloadDecodingErrorTest, UnknownEncodingThrows) {
    Payload payload;
    payload.metadata["encoding"] = "custom/unknown";
    payload.data = {1, 2, 3};

    DefaultPayloadConverter converter;
    std::any wrapped = std::any(payload);

    EXPECT_THROW(decode_payload_value<std::string>(wrapped, &converter),
                 std::invalid_argument);
}

TEST(PayloadDecodingErrorTest, MissingEncodingThrows) {
    Payload payload;  // no encoding metadata
    payload.data = {1, 2, 3};

    DefaultPayloadConverter converter;
    std::any wrapped = std::any(payload);

    EXPECT_THROW(decode_payload_value<std::string>(wrapped, &converter),
                 std::invalid_argument);
}

TEST(PayloadDecodingErrorTest, WrongAnyTypeBadCast) {
    // If std::any contains an int but we try decode_payload_value<std::string>
    // and it's not a Payload, std::any_cast should throw std::bad_any_cast
    std::any val = std::any(42);

    EXPECT_THROW(decode_payload_value<std::string>(val),
                 std::bad_any_cast);
}
