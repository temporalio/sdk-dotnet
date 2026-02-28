#include "temporalio/coro/coroutine_scheduler.h"

namespace temporalio::coro {

void CoroutineScheduler::schedule(std::coroutine_handle<> handle) {
    ready_queue_.push_back(handle);
}

void CoroutineScheduler::schedule_from_external(
    std::coroutine_handle<> handle) {
    std::lock_guard lock(external_mutex_);
    external_queue_.push_back(handle);
}

void CoroutineScheduler::drain_external_queue() {
    std::lock_guard lock(external_mutex_);
    for (auto& h : external_queue_) {
        ready_queue_.push_back(h);
    }
    external_queue_.clear();
}

bool CoroutineScheduler::drain() {
    // Move any externally-enqueued handles into the ready queue first.
    // This ensures that coroutines resumed from FFI callback threads
    // actually execute on the workflow thread.
    drain_external_queue();

    if (ready_queue_.empty()) {
        return false;
    }

    // Process all currently queued coroutines plus any newly enqueued ones.
    // Uses FIFO order (front/pop_front) to match the C# pattern in
    // WorkflowInstance.RunOnce():
    //   scheduledTasks is a LinkedList where QueueTask does AddFirst (front)
    //   and RunOnce pops from Last (back), giving FIFO execution order.
    // Here, schedule() appends to back (push_back) and we pop from front
    // (pop_front), giving the same FIFO semantics. This ensures
    // deterministic replay processes signal handlers in the correct order.
    //
    // After each resume, also drain external queue in case the resumed
    // coroutine triggered an FFI call that completed synchronously on
    // another thread.
    while (!ready_queue_.empty()) {
        auto handle = ready_queue_.front();
        ready_queue_.pop_front();
        handle.resume();
        // Check for new external handles after each resume
        drain_external_queue();
    }

    return true;
}

}  // namespace temporalio::coro
