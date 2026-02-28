#pragma once

/// @file Deterministic single-threaded coroutine scheduler for workflow replay.

#include <temporalio/export.h>

#include <coroutine>
#include <deque>
#include <mutex>
#include <vector>

namespace temporalio::coro {

/// Deterministic single-threaded executor for workflow replay.
///
/// Replaces C# WorkflowInstance : TaskScheduler (MaximumConcurrencyLevel = 1).
///
/// The scheduler has two queues:
/// - ready_queue_: NOT thread-safe, used by the workflow thread only.
/// - external_queue_: thread-safe (mutex-protected), for handles enqueued
///   from external threads (e.g., Rust FFI callbacks via ResumeCallback).
///
/// On each drain(), external handles are moved into the ready queue first,
/// then all ready coroutines are resumed deterministically.
///
/// Usage:
///   CoroutineScheduler scheduler;
///   scheduler.schedule(some_coroutine.handle());
///   while (scheduler.drain()) {
///       // process results, schedule more work
///   }
class TEMPORALIO_EXPORT CoroutineScheduler {
public:
    CoroutineScheduler() = default;

    // Non-copyable, non-movable
    CoroutineScheduler(const CoroutineScheduler&) = delete;
    CoroutineScheduler& operator=(const CoroutineScheduler&) = delete;
    CoroutineScheduler(CoroutineScheduler&&) = delete;
    CoroutineScheduler& operator=(CoroutineScheduler&&) = delete;

    /// Enqueue a coroutine handle to the ready queue.
    /// The handle will be resumed on the next drain() call.
    /// NOT thread-safe -- must be called from the workflow thread only.
    void schedule(std::coroutine_handle<> handle);

    /// Enqueue a coroutine handle from an external (non-workflow) thread.
    /// Thread-safe. The handle will be moved into the ready queue on the
    /// next drain() call, ensuring it resumes on the workflow thread.
    void schedule_from_external(std::coroutine_handle<> handle);

    /// Run all queued coroutines until the ready queue is empty.
    /// Returns true if any coroutines were executed, false if the queue
    /// was already empty.
    ///
    /// First moves any externally-enqueued handles into the ready queue,
    /// then processes all handles in FIFO order. This mirrors the C#
    /// WorkflowInstance RunOnce() semantics: tasks queued first execute
    /// first, ensuring deterministic signal handler execution order.
    bool drain();

    /// Returns the number of coroutines currently in the ready queue.
    /// Does NOT include external queue items (not thread-safe to check).
    size_t size() const noexcept { return ready_queue_.size(); }

    /// Returns true if the ready queue is empty.
    /// Does NOT include external queue items (not thread-safe to check).
    bool empty() const noexcept { return ready_queue_.empty(); }

private:
    /// Move any externally-enqueued handles into the ready queue.
    void drain_external_queue();

    std::deque<std::coroutine_handle<>> ready_queue_;

    /// Thread-safe queue for handles enqueued from external threads.
    std::mutex external_mutex_;
    std::vector<std::coroutine_handle<>> external_queue_;
};

}  // namespace temporalio::coro

