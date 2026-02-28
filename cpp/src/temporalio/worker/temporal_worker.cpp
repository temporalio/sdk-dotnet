#include "temporalio/worker/temporal_worker.h"

#include <atomic>
#include <mutex>
#include <stdexcept>
#include <thread>
#include <utility>

#include "temporalio/coro/run_sync.h"
#include "temporalio/client/temporal_client.h"
#include "temporalio/client/temporal_connection.h"
#include "temporalio/exceptions/temporal_exception.h"
#include "temporalio/worker/internal/activity_worker.h"
#include "temporalio/worker/internal/nexus_worker.h"
#include "temporalio/worker/internal/workflow_worker.h"

#include "temporalio/bridge/call_scope.h"
#include "temporalio/bridge/client.h"
#include "temporalio/bridge/worker.h"
#include "temporalio/converters/data_converter.h"

namespace temporalio::worker {

TemporalWorker::TemporalWorker(std::shared_ptr<client::TemporalClient> client,
                               TemporalWorkerOptions options)
    : client_(std::move(client)), options_(std::move(options)) {
    // Default data converter if none provided
    if (!options_.data_converter) {
        options_.data_converter = std::make_shared<converters::DataConverter>(
            converters::DataConverter::default_instance());
    }
    if (options_.task_queue.empty()) {
        throw std::invalid_argument(
            "TemporalWorkerOptions must have a task_queue set");
    }
    if (options_.workflows.empty() && options_.activities.empty() &&
        options_.nexus_services.empty()) {
        throw std::invalid_argument(
            "Must have at least one workflow, activity, and/or Nexus service");
    }

    // Build workflow name -> definition lookup map
    for (const auto& wf : options_.workflows) {
        if (!wf) {
            throw std::invalid_argument("Null workflow definition provided");
        }
        const auto& name = wf->name();
        if (name.empty()) {
            // Dynamic workflow (unnamed) -- only one allowed
            if (dynamic_workflow_) {
                throw std::invalid_argument(
                    "Multiple dynamic workflows provided");
            }
            dynamic_workflow_ = wf;
        } else {
            auto [_, inserted] = workflow_map_.emplace(name, wf);
            if (!inserted) {
                throw std::invalid_argument(
                    "Duplicate workflow named " + name);
            }
        }
    }

    // Build activity name -> definition lookup map
    for (const auto& act : options_.activities) {
        if (!act) {
            throw std::invalid_argument("Null activity definition provided");
        }
        const auto& name = act->name();
        if (name.empty()) {
            throw std::invalid_argument(
                "Activity definition must have a name");
        }
        auto [_, inserted] = activity_map_.emplace(name, act);
        if (!inserted) {
            throw std::invalid_argument(
                "Duplicate activity named " + name);
        }
    }

    // Create bridge worker from the client's bridge connection.
    // If the bridge client is available (non-null), construct the
    // TemporalCoreWorkerOptions and create the bridge::Worker.
    auto* bridge_client = client_->bridge_client();
    if (bridge_client) {
        TemporalCoreWorkerOptions core_opts{};
        // Rust requires non-null pointers for slice::from_raw_parts even with size 0
        core_opts.nondeterminism_as_workflow_fail_for_types =
            bridge::CallScope::empty_byte_array_ref_array();
        core_opts.plugins = bridge::CallScope::empty_byte_array_ref_array();
        auto ns_str = options_.ns.empty() ? client_->ns() : options_.ns;

        // Build CallScope-compatible byte arrays inline.
        // bridge::Worker constructor copies what it needs.
        bridge::CallScope scope;
        core_opts.namespace_ = scope.byte_array(ns_str);
        core_opts.task_queue = scope.byte_array(options_.task_queue);

        // Versioning strategy
        core_opts.versioning_strategy.tag =
            TemporalCoreWorkerVersioningStrategy_Tag::None;
        core_opts.versioning_strategy.none.build_id =
            scope.byte_array(options_.build_id);

        // Identity override
        core_opts.identity_override = scope.byte_array(options_.identity);

        // Workflow cache
        core_opts.max_cached_workflows = options_.max_cached_workflows;

        // Tuner: fixed-size slot suppliers by default
        core_opts.tuner.workflow_slot_supplier.tag =
            TemporalCoreSlotSupplier_Tag::FixedSize;
        core_opts.tuner.workflow_slot_supplier.fixed_size.num_slots =
            options_.max_concurrent_workflow_tasks;

        core_opts.tuner.activity_slot_supplier.tag =
            TemporalCoreSlotSupplier_Tag::FixedSize;
        core_opts.tuner.activity_slot_supplier.fixed_size.num_slots =
            options_.max_concurrent_activities;

        core_opts.tuner.local_activity_slot_supplier.tag =
            TemporalCoreSlotSupplier_Tag::FixedSize;
        core_opts.tuner.local_activity_slot_supplier.fixed_size.num_slots =
            options_.max_concurrent_local_activities;

        core_opts.tuner.nexus_task_slot_supplier.tag =
            TemporalCoreSlotSupplier_Tag::FixedSize;
        core_opts.tuner.nexus_task_slot_supplier.fixed_size.num_slots =
            options_.max_concurrent_nexus_tasks;

        // Task types: enable based on what's registered
        core_opts.task_types.enable_workflows =
            !options_.workflows.empty();
        core_opts.task_types.enable_remote_activities =
            !options_.activities.empty() &&
            !options_.local_activity_worker_only;
        core_opts.task_types.enable_local_activities =
            !options_.workflows.empty();
        core_opts.task_types.enable_nexus =
            !options_.nexus_services.empty();

        // Sticky queue timeout
        core_opts.sticky_queue_schedule_to_start_timeout_millis =
            static_cast<uint64_t>(
                options_.sticky_queue_schedule_to_start_timeout.count());

        // Activity rate limits
        core_opts.max_activities_per_second =
            options_.max_activities_per_second.value_or(0.0);
        core_opts.max_task_queue_activities_per_second =
            options_.max_task_queue_activities_per_second.value_or(0.0);

        // Graceful shutdown period
        core_opts.graceful_shutdown_period_millis =
            static_cast<uint64_t>(
                options_.graceful_shutdown_timeout.count());

        // Poller behaviors (simple maximum by default)
        TemporalCorePollerBehaviorSimpleMaximum wf_poller{
            options_.max_concurrent_workflow_task_polls};
        core_opts.workflow_task_poller_behavior.simple_maximum = &wf_poller;
        core_opts.workflow_task_poller_behavior.autoscaling = nullptr;

        TemporalCorePollerBehaviorSimpleMaximum act_poller{
            options_.max_concurrent_activity_task_polls};
        core_opts.activity_task_poller_behavior.simple_maximum = &act_poller;
        core_opts.activity_task_poller_behavior.autoscaling = nullptr;

        TemporalCorePollerBehaviorSimpleMaximum nexus_poller{
            options_.max_concurrent_nexus_task_polls};
        core_opts.nexus_task_poller_behavior.simple_maximum = &nexus_poller;
        core_opts.nexus_task_poller_behavior.autoscaling = nullptr;

        core_opts.nonsticky_to_sticky_poll_ratio =
            options_.non_sticky_to_sticky_poll_ratio;

        core_opts.nondeterminism_as_workflow_fail = false;

        bridge_worker_ = std::make_unique<bridge::Worker>(
            *bridge_client, core_opts);
    }
}

TemporalWorker::~TemporalWorker() = default;

coro::Task<void> TemporalWorker::execute_async(
    std::stop_token shutdown_token) {
    // Prevent double-start
    bool expected = false;
    if (!started_.compare_exchange_strong(expected, true)) {
        throw std::runtime_error("Worker already started");
    }

    // Validate the bridge worker if available. C# calls
    // BridgeWorker.ValidateAsync() before starting poll loops.
    if (bridge_worker_) {
        auto tcs = std::make_shared<coro::TaskCompletionSource<void>>();
        bridge_worker_->validate_async([tcs](std::string error) {
            if (error.empty()) {
                tcs->try_set_result();
            } else {
                tcs->try_set_exception(
                    std::make_exception_ptr(std::runtime_error(
                        "Worker validation failed: " + error)));
            }
        });
        co_await tcs->task();
    }

    // Resolve the effective namespace
    std::string effective_ns =
        options_.ns.empty() ? client_->ns() : options_.ns;

    // Create sub-workers based on what's registered
    if (!options_.workflows.empty()) {
        internal::WorkflowWorkerOptions wf_opts;
        wf_opts.bridge_worker = bridge_worker_.get();
        wf_opts.task_queue = options_.task_queue;
        wf_opts.ns = effective_ns;
        wf_opts.workflows = workflow_map_;
        wf_opts.dynamic_workflow = dynamic_workflow_;
        wf_opts.data_converter = options_.data_converter;
        wf_opts.interceptors = options_.interceptors;
        wf_opts.debug_mode = options_.debug_mode;
        workflow_worker_ =
            std::make_unique<internal::WorkflowWorker>(std::move(wf_opts));
    }

    if (!options_.activities.empty()) {
        internal::ActivityWorkerOptions act_opts;
        act_opts.bridge_worker = bridge_worker_.get();
        act_opts.task_queue = options_.task_queue;
        act_opts.ns = effective_ns;
        act_opts.activities = activity_map_;
        act_opts.data_converter = options_.data_converter;
        act_opts.interceptors = options_.interceptors;
        act_opts.max_concurrent = options_.max_concurrent_activities;
        act_opts.graceful_shutdown_timeout = options_.graceful_shutdown_timeout;
        activity_worker_ =
            std::make_unique<internal::ActivityWorker>(std::move(act_opts));
    }

    if (!options_.nexus_services.empty()) {
        internal::NexusWorkerOptions nexus_opts;
        nexus_opts.bridge_worker = bridge_worker_.get();
        nexus_opts.task_queue = options_.task_queue;
        nexus_opts.ns = effective_ns;
        nexus_opts.services.reserve(options_.nexus_services.size());
        for (auto& svc : options_.nexus_services) {
            nexus_opts.services.push_back(svc);
        }
        nexus_opts.client = client_;
        nexus_worker_ =
            std::make_unique<internal::NexusWorker>(std::move(nexus_opts));
    }

    // Start all poll loops concurrently on separate threads.
    // Each sub-worker's execute_async() returns a lazy Task<void> that polls
    // the bridge for work. We use run_task_sync to drive each coroutine to
    // completion on its own thread, providing true concurrency.
    //
    // The shutdown flow mirrors C#:
    // 1. Wait for shutdown_token or any poll loop failure
    // 2. Initiate bridge shutdown
    // 3. Notify activity worker of shutdown (for graceful drain)
    // 4. Wait for all poll loops to complete (they drain remaining work)
    // 5. Finalize shutdown on the bridge
    //
    // IMPORTANT: Poll threads are detached (fire-and-forget) rather than
    // joined. Two constraints drive this:
    //   (a) After co_await validate_async(), this coroutine resumes on a
    //       Rust callback thread. We must NOT block it (done_cv.wait would
    //       starve the tokio runtime).
    //   (b) co_await all_done_tcs resumes this coroutine on the LAST poll
    //       thread that signaled. Joining poll threads from a poll thread
    //       would deadlock (thread joining itself).
    // Detached threads solve both: co_await suspends (no thread blocked),
    // and no join is attempted. Thread lambdas capture shared_ptrs so all
    // shared state outlives the threads.

    auto all_done_tcs =
        std::make_shared<coro::TaskCompletionSource<void>>();
    auto remaining = std::make_shared<std::atomic<size_t>>(0);
    auto first_exception = std::make_shared<std::exception_ptr>(nullptr);
    auto exception_mutex = std::make_shared<std::mutex>();

    // Helper: launch a detached thread to drive a poll task.
    auto launch_poll = [&](coro::Task<void> task) {
        remaining->fetch_add(1, std::memory_order_relaxed);
        std::thread([all_done_tcs, remaining, first_exception,
                     exception_mutex,
                     t = std::move(task)]() mutable {
            try {
                coro::run_task_sync(std::move(t));
            } catch (...) {
                std::lock_guard lock(*exception_mutex);
                if (!*first_exception) {
                    *first_exception = std::current_exception();
                }
            }
            if (remaining->fetch_sub(1, std::memory_order_acq_rel) == 1) {
                all_done_tcs->try_set_result();
            }
        }).detach();
    };

    if (workflow_worker_) {
        launch_poll(workflow_worker_->execute_async());
    }
    if (activity_worker_) {
        launch_poll(activity_worker_->execute_async());
    }
    if (nexus_worker_) {
        launch_poll(nexus_worker_->execute_async());
    }

    // If no sub-workers were launched, nothing to do
    if (remaining->load(std::memory_order_relaxed) == 0) {
        co_return;
    }

    // Monitor the shutdown token: when triggered, initiate bridge shutdown
    // which causes polls to return nullopt.
    std::stop_callback shutdown_cb(shutdown_token, [this]() {
        // Initiate bridge shutdown
        if (bridge_worker_) {
            bridge_worker_->initiate_shutdown();
        }

        // Notify activity worker to begin graceful shutdown
        if (activity_worker_) {
            activity_worker_->notify_shutdown();
        }
    });

    // Wait for all poll threads to complete. This co_await suspends the
    // coroutine (freeing the current thread) until the last poll thread
    // signals completion.
    co_await all_done_tcs->task();

    // Propagate the first exception from any poll loop.
    if (*first_exception) {
        std::rethrow_exception(*first_exception);
    }

    // Finalize shutdown on the bridge
    if (bridge_worker_) {
        auto tcs = std::make_shared<coro::TaskCompletionSource<void>>();
        bridge_worker_->finalize_shutdown_async([tcs](std::string error) {
            if (error.empty()) {
                tcs->try_set_result();
            } else {
                tcs->try_set_exception(
                    std::make_exception_ptr(std::runtime_error(
                        "Worker finalization failed: " + error)));
            }
        });
        // Ignore finalization errors (same as C#)
        try {
            co_await tcs->task();
        } catch (...) {
            // Ignore finalization errors
        }
    }
}

}  // namespace temporalio::worker
