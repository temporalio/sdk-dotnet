#include <gtest/gtest.h>

#include <atomic>
#include <coroutine>
#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "temporalio/coro/coroutine_scheduler.h"

using namespace temporalio::coro;

// ---------------------------------------------------------------------------
// Minimal coroutine type for scheduler tests.
// Uses suspend_never for final_suspend so the coroutine frame self-destructs
// after completion, avoiding Task's symmetric transfer (FinalAwaiter returns
// noop_coroutine()) which triggers a GCC 13 codegen bug.
// ---------------------------------------------------------------------------
namespace {

struct SimpleCoroutine {
    struct promise_type {
        SimpleCoroutine get_return_object() {
            return SimpleCoroutine{
                std::coroutine_handle<promise_type>::from_promise(*this)};
        }
        std::suspend_always initial_suspend() noexcept { return {}; }
        std::suspend_never final_suspend() noexcept { return {}; }
        void return_void() noexcept {}
        void unhandled_exception() noexcept {}
    };
    std::coroutine_handle<> handle_;
};

// -----------------------------------------------------------------------
// GCC 13 workaround: lambda coroutines with reference captures and no
// internal co_await generate broken resume code (body never executes).
// Free-function coroutines work correctly. See FIFOOrdering test which
// uses record_and_return() and passes on GCC 13.
// -----------------------------------------------------------------------

/// SimpleCoroutine that sets a bool flag.
static SimpleCoroutine set_flag(bool& flag) {
    flag = true;
    co_return;
}

/// SimpleCoroutine that pushes a value to a vector.
static SimpleCoroutine push_value(std::vector<int>& order, int value) {
    order.push_back(value);
    co_return;
}

/// SimpleCoroutine that pushes a string to a vector.
static SimpleCoroutine push_string(std::vector<std::string>& log,
                                   std::string value) {
    log.push_back(std::move(value));
    co_return;
}

/// SimpleCoroutine that atomically increments a counter.
static SimpleCoroutine atomic_increment(std::atomic<int>& count) {
    count.fetch_add(1, std::memory_order_relaxed);
    co_return;
}

/// Awaiter that re-enqueues the coroutine on the scheduler (yields).
/// Used by free-function coroutines below to avoid lambda capture issues.
struct YieldAwaiter {
    CoroutineScheduler& sched;
    bool await_ready() const noexcept { return false; }
    void await_suspend(std::coroutine_handle<> h) {
        sched.schedule(h);
    }
    void await_resume() noexcept {}
};

/// SimpleCoroutine that yields once via the scheduler, pushing to log
/// before and after. Free function avoids lambda coroutine capture issues
/// that ASan detects as stack-use-after-scope. Uses SimpleCoroutine instead
/// of Task<void> to avoid GCC 12/13 symmetric-transfer codegen bugs.
static SimpleCoroutine yield_and_log(CoroutineScheduler& sched,
                                     std::vector<std::string>& log,
                                     std::string step1, std::string step2) {
    log.push_back(std::move(step1));
    co_await YieldAwaiter{sched};
    log.push_back(std::move(step2));
}

/// SimpleCoroutine that yields N times, pushing a label each time.
/// Uses SimpleCoroutine instead of Task<void> to avoid GCC 12/13
/// symmetric-transfer codegen bugs.
static SimpleCoroutine yield_n_times(CoroutineScheduler& sched,
                                     std::vector<std::string>& log,
                                     std::vector<std::string> labels) {
    for (size_t i = 0; i < labels.size(); ++i) {
        log.push_back(std::move(labels[i]));
        if (i + 1 < labels.size()) {
            co_await YieldAwaiter{sched};
        }
    }
}

}  // namespace

// ===========================================================================
// Basic scheduler tests
// ===========================================================================

TEST(CoroutineSchedulerTest, InitiallyEmpty) {
    CoroutineScheduler scheduler;
    EXPECT_TRUE(scheduler.empty());
    EXPECT_EQ(scheduler.size(), 0u);
}

TEST(CoroutineSchedulerTest, DrainEmptyReturnsFalse) {
    CoroutineScheduler scheduler;
    EXPECT_FALSE(scheduler.drain());
}

TEST(CoroutineSchedulerTest, ScheduleIncrementsSize) {
    CoroutineScheduler scheduler;

    auto coro = []() -> SimpleCoroutine { co_return; }();
    scheduler.schedule(coro.handle_);

    EXPECT_FALSE(scheduler.empty());
    EXPECT_EQ(scheduler.size(), 1u);
}

TEST(CoroutineSchedulerTest, DrainRunsScheduledCoroutine) {
    CoroutineScheduler scheduler;

    bool executed = false;
    auto coro = set_flag(executed);

    // Coroutine is lazy, suspended at initial_suspend
    EXPECT_FALSE(executed);

    scheduler.schedule(coro.handle_);
    bool ran = scheduler.drain();

    EXPECT_TRUE(ran);
    EXPECT_TRUE(executed);
    EXPECT_TRUE(scheduler.empty());
}

TEST(CoroutineSchedulerTest, DrainRunsMultipleCoroutines) {
    CoroutineScheduler scheduler;

    std::vector<int> order;

    auto coro1 = push_value(order, 1);
    auto coro2 = push_value(order, 2);
    auto coro3 = push_value(order, 3);

    scheduler.schedule(coro1.handle_);
    scheduler.schedule(coro2.handle_);
    scheduler.schedule(coro3.handle_);

    EXPECT_EQ(scheduler.size(), 3u);

    bool ran = scheduler.drain();
    EXPECT_TRUE(ran);
    EXPECT_TRUE(scheduler.empty());

    // Executes in FIFO order (front/pop_front) for deterministic replay
    ASSERT_EQ(order.size(), 3u);
    EXPECT_EQ(order[0], 1);
    EXPECT_EQ(order[1], 2);
    EXPECT_EQ(order[2], 3);
}

// ===========================================================================
// Coroutines that schedule more work during drain
// ===========================================================================

TEST(CoroutineSchedulerTest, CoroutineCanScheduleMoreWork) {
    CoroutineScheduler scheduler;

    std::vector<std::string> log;

    // Free-function coroutine that yields once via the scheduler.
    // Lambda coroutines with reference captures trigger ASan
    // stack-use-after-scope when the lambda temporary is destroyed.
    auto coro = yield_and_log(scheduler, log, "step1", "step2");

    scheduler.schedule(coro.handle_);

    // First drain: executes step1, which suspends and re-enqueues,
    // then the scheduler picks up the re-enqueued handle and executes step2.
    bool ran = scheduler.drain();
    EXPECT_TRUE(ran);
    EXPECT_TRUE(scheduler.empty());

    ASSERT_EQ(log.size(), 2u);
    EXPECT_EQ(log[0], "step1");
    EXPECT_EQ(log[1], "step2");
}

// ===========================================================================
// Drain after drain
// ===========================================================================

TEST(CoroutineSchedulerTest, SecondDrainReturnsFalseWhenEmpty) {
    CoroutineScheduler scheduler;

    auto coro = []() -> SimpleCoroutine { co_return; }();
    scheduler.schedule(coro.handle_);

    EXPECT_TRUE(scheduler.drain());
    EXPECT_FALSE(scheduler.drain());  // Nothing left
}

// ===========================================================================
// Size tracking
// ===========================================================================

TEST(CoroutineSchedulerTest, SizeDecreasesAfterDrain) {
    CoroutineScheduler scheduler;

    auto c1 = []() -> SimpleCoroutine { co_return; }();
    auto c2 = []() -> SimpleCoroutine { co_return; }();

    scheduler.schedule(c1.handle_);
    scheduler.schedule(c2.handle_);
    EXPECT_EQ(scheduler.size(), 2u);

    scheduler.drain();
    EXPECT_EQ(scheduler.size(), 0u);
}

// ===========================================================================
// Non-copyable, non-movable
// ===========================================================================

TEST(CoroutineSchedulerTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<CoroutineScheduler>);
    EXPECT_FALSE(std::is_copy_assignable_v<CoroutineScheduler>);
}

TEST(CoroutineSchedulerTest, IsNonMovable) {
    EXPECT_FALSE(std::is_move_constructible_v<CoroutineScheduler>);
    EXPECT_FALSE(std::is_move_assignable_v<CoroutineScheduler>);
}

// ===========================================================================
// Ordering (FIFO -- deterministic replay processes in schedule order)
// ===========================================================================

// Helper coroutine that takes parameters by value so they are safely
// captured in the coroutine frame (avoids dangling-lambda-capture pitfall).
static SimpleCoroutine record_and_return(std::vector<int>& order, int value) {
    order.push_back(value);
    co_return;
}

TEST(CoroutineSchedulerTest, FIFOOrdering) {
    CoroutineScheduler scheduler;

    constexpr int N = 10;
    std::vector<int> execution_order;
    std::vector<SimpleCoroutine> coros;

    for (int i = 0; i < N; ++i) {
        coros.push_back(record_and_return(execution_order, i));
    }

    for (auto& c : coros) {
        scheduler.schedule(c.handle_);
    }

    scheduler.drain();

    ASSERT_EQ(execution_order.size(), static_cast<size_t>(N));
    // FIFO: first scheduled runs first
    for (int i = 0; i < N; ++i) {
        EXPECT_EQ(execution_order[static_cast<size_t>(i)], i);
    }
}

// ===========================================================================
// Interleaving: multiple coroutines yielding back to scheduler
// ===========================================================================

TEST(CoroutineSchedulerTest, InterleavedExecution) {
    CoroutineScheduler scheduler;

    std::vector<std::string> log;

    // Free-function coroutines avoid lambda capture ASan issues.
    auto coro_a = yield_n_times(scheduler, log, {"A1", "A2", "A3"});
    auto coro_b = yield_n_times(scheduler, log, {"B1", "B2"});

    scheduler.schedule(coro_a.handle_);
    scheduler.schedule(coro_b.handle_);

    scheduler.drain();

    // Expected FIFO interleaving (front/pop_front):
    // Queue: [A, B] -> pop A: runs A1, A suspends -> re-enqueues A -> [B, A]
    // Queue: [B, A] -> pop B: runs B1, B suspends -> re-enqueues B -> [A, B]
    // Queue: [A, B] -> pop A: runs A2, A suspends -> re-enqueues A -> [B, A]
    // Queue: [B, A] -> pop B: runs B2, B completes -> [A]
    // Queue: [A] -> pop A: runs A3, A completes -> []
    ASSERT_EQ(log.size(), 5u);
    EXPECT_EQ(log[0], "A1");
    EXPECT_EQ(log[1], "B1");
    EXPECT_EQ(log[2], "A2");
    EXPECT_EQ(log[3], "B2");
    EXPECT_EQ(log[4], "A3");
}

// ===========================================================================
// schedule_from_external tests
// ===========================================================================

TEST(CoroutineSchedulerTest, ScheduleFromExternalDrainsInNextDrain) {
    CoroutineScheduler scheduler;

    bool executed = false;
    auto coro = set_flag(executed);

    // Enqueue from "external" (could be any thread)
    scheduler.schedule_from_external(coro.handle_);

    // External queue items are not visible in size()/empty()
    // because those only check the ready queue (not thread-safe)

    // Drain should pick up the external handle
    bool ran = scheduler.drain();
    EXPECT_TRUE(ran);
    EXPECT_TRUE(executed);
    EXPECT_TRUE(scheduler.empty());
}

TEST(CoroutineSchedulerTest, ScheduleFromExternalMultipleHandles) {
    CoroutineScheduler scheduler;

    std::vector<int> order;

    auto c1 = push_value(order, 1);
    auto c2 = push_value(order, 2);
    auto c3 = push_value(order, 3);

    scheduler.schedule_from_external(c1.handle_);
    scheduler.schedule_from_external(c2.handle_);
    scheduler.schedule_from_external(c3.handle_);

    scheduler.drain();

    // External queue appended in order [1, 2, 3], FIFO pops produce 1, 2, 3
    ASSERT_EQ(order.size(), 3u);
    EXPECT_EQ(order[0], 1);
    EXPECT_EQ(order[1], 2);
    EXPECT_EQ(order[2], 3);
}

TEST(CoroutineSchedulerTest, MixedScheduleAndScheduleFromExternal) {
    CoroutineScheduler scheduler;

    std::vector<std::string> log;

    auto c_internal = push_string(log, "internal");
    auto c_external = push_string(log, "external");

    scheduler.schedule(c_internal.handle_);
    scheduler.schedule_from_external(c_external.handle_);

    scheduler.drain();

    // Both should have run. drain_external_queue() appends to ready_queue_:
    // ready_queue_ = [c_internal, c_external] -> FIFO pops internal first
    ASSERT_EQ(log.size(), 2u);
    EXPECT_EQ(log[0], "internal");
    EXPECT_EQ(log[1], "external");
}

TEST(CoroutineSchedulerTest, ScheduleFromExternalThreadSafety) {
    CoroutineScheduler scheduler;

    constexpr int N = 100;
    std::vector<SimpleCoroutine> coros;
    std::atomic<int> count{0};

    for (int i = 0; i < N; ++i) {
        coros.push_back(atomic_increment(count));
    }

    // Schedule from multiple threads concurrently
    std::vector<std::thread> threads;
    for (int i = 0; i < N; ++i) {
        threads.emplace_back([&scheduler, &coros, i]() {
            scheduler.schedule_from_external(coros[static_cast<size_t>(i)].handle_);
        });
    }
    for (auto& t : threads) {
        t.join();
    }

    // Drain on the "main" thread
    scheduler.drain();

    EXPECT_EQ(count.load(), N);
}

TEST(CoroutineSchedulerTest, DrainEmptyWithEmptyExternalQueue) {
    CoroutineScheduler scheduler;
    // Both queues empty
    EXPECT_FALSE(scheduler.drain());
}
