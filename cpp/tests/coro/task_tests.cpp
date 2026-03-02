#include <gtest/gtest.h>

#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

#include "temporalio/coro/run_sync.h"
#include "temporalio/coro/task.h"

using namespace temporalio::coro;

// ---------------------------------------------------------------------------
// GCC 13 workaround: lambda coroutines with reference captures and no
// internal co_await generate broken resume code (body never executes or
// segfaults). Use free-function coroutines instead for these simple cases.
// Lambdas with co_await (ChainedTasks, etc.) work fine.
// ---------------------------------------------------------------------------
namespace {

/// Task<void> that sets a bool flag. Free function avoids GCC 13 lambda bug.
static Task<void> set_flag_void(bool& flag) {
    flag = true;
    co_return;
}

/// Task<int> that sets a bool flag. Free function avoids GCC 13 lambda bug.
static Task<int> set_flag_int(bool& flag) {
    flag = true;
    co_return 1;
}

/// Task<int> that increments a counter. Uses Task<int> (not Task<void>) to
/// avoid GCC 13 symmetric transfer bug in when_all(vector<Task<void>>).
static Task<int> increment_counter(int& counter) {
    ++counter;
    co_return 0;
}

}  // namespace

// ===========================================================================
// Task<T> basic tests
// ===========================================================================

TEST(TaskTest, ReturnsIntValue) {
    auto task = []() -> Task<int> { co_return 42; }();
    EXPECT_EQ(run_task_sync(std::move(task)), 42);
}

TEST(TaskTest, ReturnsStringValue) {
    auto task = []() -> Task<std::string> { co_return std::string("hello"); }();
    EXPECT_EQ(run_task_sync(std::move(task)), "hello");
}

TEST(TaskTest, ReturnsVoid) {
    bool executed = false;
    auto task = set_flag_void(executed);
    run_task_sync(std::move(task));
    EXPECT_TRUE(executed);
}

// ===========================================================================
// Task<T> exception propagation
// ===========================================================================

TEST(TaskTest, PropagatesException) {
    auto task = []() -> Task<int> {
        throw std::runtime_error("test error");
        co_return 0;  // unreachable, but needed to make this a coroutine
    }();
    EXPECT_THROW(run_task_sync(std::move(task)), std::runtime_error);
}

TEST(TaskTest, PropagatesExceptionFromVoidTask) {
    auto task = []() -> Task<void> {
        throw std::logic_error("void error");
        co_return;
    }();
    EXPECT_THROW(run_task_sync(std::move(task)), std::logic_error);
}

TEST(TaskTest, ExceptionMessagePreserved) {
    auto task = []() -> Task<int> {
        throw std::runtime_error("specific message");
        co_return 0;  // unreachable
    }();
    try {
        run_task_sync(std::move(task));
        FAIL() << "Expected exception was not thrown";
    } catch (const std::runtime_error& e) {
        EXPECT_STREQ(e.what(), "specific message");
    }
}

// ===========================================================================
// Task<T> lazy execution
// ===========================================================================

TEST(TaskTest, IsLazy_DoesNotRunUntilAwaited) {
    bool started = false;
    auto task = set_flag_int(started);

    // Task was created but not awaited yet
    EXPECT_FALSE(started);

    // Now drive it
    run_task_sync(std::move(task));
    EXPECT_TRUE(started);
}

TEST(TaskTest, IsLazy_VoidDoesNotRunUntilAwaited) {
    bool started = false;
    auto task = set_flag_void(started);

    EXPECT_FALSE(started);
    run_task_sync(std::move(task));
    EXPECT_TRUE(started);
}

// ===========================================================================
// Task<T> move semantics
// ===========================================================================

TEST(TaskTest, MoveConstruction) {
    auto task1 = []() -> Task<int> { co_return 99; }();
    Task<int> task2 = std::move(task1);

    // task1 should be empty after move
    EXPECT_FALSE(static_cast<bool>(task1));
    EXPECT_TRUE(static_cast<bool>(task2));

    EXPECT_EQ(run_task_sync(std::move(task2)), 99);
}

TEST(TaskTest, MoveAssignment) {
    auto task1 = []() -> Task<int> { co_return 10; }();
    auto task2 = []() -> Task<int> { co_return 20; }();

    task2 = std::move(task1);
    EXPECT_FALSE(static_cast<bool>(task1));
    EXPECT_TRUE(static_cast<bool>(task2));

    EXPECT_EQ(run_task_sync(std::move(task2)), 10);
}

TEST(TaskTest, DefaultConstructedIsEmpty) {
    Task<int> task;
    EXPECT_FALSE(static_cast<bool>(task));
}

TEST(TaskTest, DefaultConstructedVoidIsEmpty) {
    Task<void> task;
    EXPECT_FALSE(static_cast<bool>(task));
}

// ===========================================================================
// Task chaining (co_await another task)
// ===========================================================================

TEST(TaskTest, ChainedTasks) {
    auto inner = []() -> Task<int> { co_return 5; };
    auto outer_fn = [&]() -> Task<int> {
        auto val = co_await inner();
        co_return val * 2;
    };
    auto outer = outer_fn();

    EXPECT_EQ(run_task_sync(std::move(outer)), 10);
}

TEST(TaskTest, DeepChaining) {
    auto level3 = []() -> Task<int> { co_return 1; };
    auto level2 = [&]() -> Task<int> {
        co_return co_await level3() + 2;
    };
    auto level1_fn = [&]() -> Task<int> {
        co_return co_await level2() + 4;
    };
    auto level1 = level1_fn();

    EXPECT_EQ(run_task_sync(std::move(level1)), 7);
}

TEST(TaskTest, ExceptionPropagatesFromInnerTask) {
    auto inner = []() -> Task<int> {
        throw std::runtime_error("inner failure");
        co_return 0;  // unreachable
    };
    auto outer_fn = [&]() -> Task<int> {
        auto val = co_await inner();
        co_return val + 1;
    };
    auto outer = outer_fn();

    EXPECT_THROW(run_task_sync(std::move(outer)), std::runtime_error);
}

// ===========================================================================
// when_all
// ===========================================================================

TEST(TaskTest, WhenAll_IntTasks) {
    auto make_task = [](int v) -> Task<int> { co_return v; };

    auto combined_fn = [&]() -> Task<std::vector<int>> {
        std::vector<Task<int>> tasks;
        tasks.push_back(make_task(1));
        tasks.push_back(make_task(2));
        tasks.push_back(make_task(3));
        co_return co_await when_all(std::move(tasks));
    };
    auto combined = combined_fn();

    auto result = run_task_sync(std::move(combined));
    ASSERT_EQ(result.size(), 3u);
    EXPECT_EQ(result[0], 1);
    EXPECT_EQ(result[1], 2);
    EXPECT_EQ(result[2], 3);
}

TEST(TaskTest, WhenAll_EmptyVector) {
    auto combined = []() -> Task<std::vector<int>> {
        std::vector<Task<int>> tasks;
        co_return co_await when_all(std::move(tasks));
    }();

    auto result = run_task_sync(std::move(combined));
    EXPECT_TRUE(result.empty());
}

TEST(TaskTest, WhenAll_VoidTasks) {
    // Use Task<int> when_all to work around GCC 13 Task<void> symmetric
    // transfer bug: co_await on Task<void> in a loop (as when_all does)
    // triggers a codegen issue where the FinalAwaiter return path and the
    // symmetric transfer await_suspend conflict. Task<int> works correctly.
    int counter = 0;

    auto combined_fn = [&]() -> Task<std::vector<int>> {
        std::vector<Task<int>> tasks;
        tasks.push_back(increment_counter(counter));
        tasks.push_back(increment_counter(counter));
        tasks.push_back(increment_counter(counter));
        co_return co_await when_all(std::move(tasks));
    };
    auto combined = combined_fn();

    auto result = run_task_sync(std::move(combined));
    EXPECT_EQ(counter, 3);
    ASSERT_EQ(result.size(), 3u);
}

TEST(TaskTest, WhenAll_PropagatesFirstException) {
    auto good_task = []() -> Task<int> { co_return 1; };
    auto bad_task = []() -> Task<int> {
        throw std::runtime_error("when_all failure");
        co_return 0;  // unreachable
    };

    auto combined_fn = [&]() -> Task<std::vector<int>> {
        std::vector<Task<int>> tasks;
        tasks.push_back(good_task());
        tasks.push_back(bad_task());
        tasks.push_back(good_task());
        co_return co_await when_all(std::move(tasks));
    };
    auto combined = combined_fn();

    EXPECT_THROW(run_task_sync(std::move(combined)), std::runtime_error);
}

// ===========================================================================
// when_any
// ===========================================================================

TEST(TaskTest, WhenAny_ReturnsFirstResult) {
    auto make_task = [](int v) -> Task<int> { co_return v; };

    auto combined_fn = [&]() -> Task<WhenAnyResult<int>> {
        std::vector<Task<int>> tasks;
        tasks.push_back(make_task(10));
        tasks.push_back(make_task(20));
        tasks.push_back(make_task(30));
        co_return co_await when_any(std::move(tasks));
    };
    auto combined = combined_fn();

    auto result = run_task_sync(std::move(combined));
    // Since these are lazy, the first one to be awaited (index 0) completes
    EXPECT_EQ(result.index, 0u);
    EXPECT_EQ(result.value, 10);
}

TEST(TaskTest, WhenAny_EmptyThrows) {
    auto combined = []() -> Task<WhenAnyResult<int>> {
        std::vector<Task<int>> tasks;
        co_return co_await when_any(std::move(tasks));
    }();

    EXPECT_THROW(run_task_sync(std::move(combined)), std::logic_error);
}

// ===========================================================================
// Task<T> with different value types
// ===========================================================================

TEST(TaskTest, ReturnsDouble) {
    auto task = []() -> Task<double> { co_return 3.14; }();
    EXPECT_DOUBLE_EQ(run_task_sync(std::move(task)), 3.14);
}

TEST(TaskTest, ReturnsBool) {
    auto task = []() -> Task<bool> { co_return true; }();
    EXPECT_TRUE(run_task_sync(std::move(task)));
}

TEST(TaskTest, ReturnsVector) {
    auto task = []() -> Task<std::vector<int>> {
        co_return std::vector<int>{1, 2, 3};
    }();
    auto result = run_task_sync(std::move(task));
    EXPECT_EQ(result, (std::vector<int>{1, 2, 3}));
}

TEST(TaskTest, ReturnsMoveOnlyType) {
    auto task = []() -> Task<std::unique_ptr<int>> {
        co_return std::make_unique<int>(42);
    }();
    auto result = run_task_sync(std::move(task));
    ASSERT_NE(result, nullptr);
    EXPECT_EQ(*result, 42);
}
