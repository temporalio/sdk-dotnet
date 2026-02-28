#include <gtest/gtest.h>

#include <atomic>
#include <coroutine>
#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>
#include <vector>

#include "temporalio/coro/coroutine_scheduler.h"
#include "temporalio/coro/task.h"

using namespace temporalio::coro;

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

    auto task = []() -> Task<void> { co_return; }();
    scheduler.schedule(task.handle());

    EXPECT_FALSE(scheduler.empty());
    EXPECT_EQ(scheduler.size(), 1u);
}

TEST(CoroutineSchedulerTest, DrainRunsScheduledCoroutine) {
    CoroutineScheduler scheduler;

    bool executed = false;
    auto task = [&]() -> Task<void> {
        executed = true;
        co_return;
    }();

    // Task is lazy, suspended at initial_suspend
    EXPECT_FALSE(executed);

    scheduler.schedule(task.handle());
    bool ran = scheduler.drain();

    EXPECT_TRUE(ran);
    EXPECT_TRUE(executed);
    EXPECT_TRUE(scheduler.empty());
}

TEST(CoroutineSchedulerTest, DrainRunsMultipleCoroutines) {
    CoroutineScheduler scheduler;

    std::vector<int> order;

    auto task1 = [&]() -> Task<void> {
        order.push_back(1);
        co_return;
    }();

    auto task2 = [&]() -> Task<void> {
        order.push_back(2);
        co_return;
    }();

    auto task3 = [&]() -> Task<void> {
        order.push_back(3);
        co_return;
    }();

    scheduler.schedule(task1.handle());
    scheduler.schedule(task2.handle());
    scheduler.schedule(task3.handle());

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

    // This task, when resumed, will schedule another task.
    // We need a custom awaiter that enqueues to the scheduler.
    struct ScheduleAwaiter {
        CoroutineScheduler& sched;
        bool await_ready() const noexcept { return false; }
        void await_suspend(std::coroutine_handle<> h) {
            // Re-enqueue ourselves
            sched.schedule(h);
        }
        void await_resume() noexcept {}
    };

    // A coroutine that yields once via the scheduler
    auto task = [&](CoroutineScheduler& sched) -> Task<void> {
        log.push_back("step1");
        co_await ScheduleAwaiter{sched};
        log.push_back("step2");
    }(scheduler);

    scheduler.schedule(task.handle());

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

    auto task = []() -> Task<void> { co_return; }();
    scheduler.schedule(task.handle());

    EXPECT_TRUE(scheduler.drain());
    EXPECT_FALSE(scheduler.drain());  // Nothing left
}

// ===========================================================================
// Size tracking
// ===========================================================================

TEST(CoroutineSchedulerTest, SizeDecreasesAfterDrain) {
    CoroutineScheduler scheduler;

    auto t1 = []() -> Task<void> { co_return; }();
    auto t2 = []() -> Task<void> { co_return; }();

    scheduler.schedule(t1.handle());
    scheduler.schedule(t2.handle());
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
static Task<void> record_and_return(std::vector<int>& order, int value) {
    order.push_back(value);
    co_return;
}

TEST(CoroutineSchedulerTest, FIFOOrdering) {
    CoroutineScheduler scheduler;

    constexpr int N = 10;
    std::vector<int> execution_order;
    std::vector<Task<void>> tasks;

    for (int i = 0; i < N; ++i) {
        tasks.push_back(record_and_return(execution_order, i));
    }

    for (auto& t : tasks) {
        scheduler.schedule(t.handle());
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

    struct YieldAwaiter {
        CoroutineScheduler& sched;
        bool await_ready() const noexcept { return false; }
        void await_suspend(std::coroutine_handle<> h) {
            sched.schedule(h);
        }
        void await_resume() noexcept {}
    };

    auto task_a = [&](CoroutineScheduler& sched) -> Task<void> {
        log.push_back("A1");
        co_await YieldAwaiter{sched};
        log.push_back("A2");
        co_await YieldAwaiter{sched};
        log.push_back("A3");
    }(scheduler);

    auto task_b = [&](CoroutineScheduler& sched) -> Task<void> {
        log.push_back("B1");
        co_await YieldAwaiter{sched};
        log.push_back("B2");
    }(scheduler);

    scheduler.schedule(task_a.handle());
    scheduler.schedule(task_b.handle());

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
    auto task = [&]() -> Task<void> {
        executed = true;
        co_return;
    }();

    // Enqueue from "external" (could be any thread)
    scheduler.schedule_from_external(task.handle());

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

    auto t1 = [&]() -> Task<void> {
        order.push_back(1);
        co_return;
    }();
    auto t2 = [&]() -> Task<void> {
        order.push_back(2);
        co_return;
    }();
    auto t3 = [&]() -> Task<void> {
        order.push_back(3);
        co_return;
    }();

    scheduler.schedule_from_external(t1.handle());
    scheduler.schedule_from_external(t2.handle());
    scheduler.schedule_from_external(t3.handle());

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

    auto t_internal = [&]() -> Task<void> {
        log.push_back("internal");
        co_return;
    }();
    auto t_external = [&]() -> Task<void> {
        log.push_back("external");
        co_return;
    }();

    scheduler.schedule(t_internal.handle());
    scheduler.schedule_from_external(t_external.handle());

    scheduler.drain();

    // Both should have run. drain_external_queue() appends to ready_queue_:
    // ready_queue_ = [t_internal, t_external] -> FIFO pops internal first
    ASSERT_EQ(log.size(), 2u);
    EXPECT_EQ(log[0], "internal");
    EXPECT_EQ(log[1], "external");
}

TEST(CoroutineSchedulerTest, ScheduleFromExternalThreadSafety) {
    CoroutineScheduler scheduler;

    constexpr int N = 100;
    std::vector<Task<void>> tasks;
    std::atomic<int> count{0};

    for (int i = 0; i < N; ++i) {
        tasks.push_back([&count]() -> Task<void> {
            count.fetch_add(1, std::memory_order_relaxed);
            co_return;
        }());
    }

    // Schedule from multiple threads concurrently
    std::vector<std::thread> threads;
    for (int i = 0; i < N; ++i) {
        threads.emplace_back([&scheduler, &tasks, i]() {
            scheduler.schedule_from_external(tasks[static_cast<size_t>(i)].handle());
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
