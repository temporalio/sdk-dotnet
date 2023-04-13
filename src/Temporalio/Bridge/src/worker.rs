use crate::client::Client;
use crate::runtime::Runtime;
use crate::ByteArray;
use crate::ByteArrayRef;
use crate::UserDataHandle;
use prost::Message;
use temporal_sdk_core::WorkerConfigBuilder;
use temporal_sdk_core_api::errors::PollActivityError;
use temporal_sdk_core_api::errors::PollWfError;
use temporal_sdk_core_api::Worker as CoreWorker;
use temporal_sdk_core_protos::coresdk::workflow_completion::WorkflowActivationCompletion;
use temporal_sdk_core_protos::coresdk::ActivityHeartbeat;
use temporal_sdk_core_protos::coresdk::ActivityTaskCompletion;

use std::sync::Arc;
use std::time::Duration;

#[repr(C)]
pub struct WorkerOptions {
    namespace: ByteArrayRef,
    task_queue: ByteArrayRef,
    build_id: ByteArrayRef,
    identity_override: ByteArrayRef,
    max_cached_workflows: u32,
    max_outstanding_workflow_tasks: u32,
    max_outstanding_activities: u32,
    max_outstanding_local_activities: u32,
    no_remote_activities: bool,
    sticky_queue_schedule_to_start_timeout_millis: u64,
    max_heartbeat_throttle_interval_millis: u64,
    default_heartbeat_throttle_interval_millis: u64,
    max_activities_per_second: f64,
    max_task_queue_activities_per_second: f64,
    graceful_shutdown_period_millis: u64,
}

#[derive(Clone)]
pub struct Worker {
    worker: Option<Arc<temporal_sdk_core::Worker>>,
    runtime: Runtime,
}

/// Only runtime or fail will be non-null. Whichever is must be freed when done.
#[repr(C)]
pub struct WorkerOrFail {
    worker: *mut Worker,
    fail: *const ByteArray,
}

macro_rules! enter_sync {
    ($runtime:expr) => {
        temporal_sdk_core::telemetry::set_trace_subscriber_for_current_thread(
            $runtime.core.trace_subscriber(),
        );
        let _guard = $runtime.core.tokio_handle().enter();
    };
}

#[no_mangle]
pub extern "C" fn worker_new(client: *mut Client, options: *const WorkerOptions) -> WorkerOrFail {
    let client = unsafe { &mut *client };
    enter_sync!(client.runtime);
    let options = unsafe { &*options };

    let (worker, fail) = match options.try_into() {
        Err(err) => (
            std::ptr::null_mut(),
            client
                .runtime
                .alloc_utf8(&format!("Invalid options: {}", err))
                .into_raw()
                .cast_const(),
        ),
        Ok(config) => match temporal_sdk_core::init_worker(
            &client.runtime.core,
            config,
            client.core.clone().into_inner(),
        ) {
            Err(err) => (
                std::ptr::null_mut(),
                client
                    .runtime
                    .alloc_utf8(&format!("Worker start failed: {}", err))
                    .into_raw()
                    .cast_const(),
            ),
            Ok(worker) => (
                Box::into_raw(Box::new(Worker {
                    worker: Some(Arc::new(worker)),
                    runtime: client.runtime.clone(),
                })),
                std::ptr::null(),
            ),
        },
    };
    WorkerOrFail { worker, fail }
}

#[no_mangle]
pub extern "C" fn worker_free(worker: *mut Worker) {
    unsafe {
        let _ = Box::from_raw(worker);
    }
}

/// If success or fail are present, they must be freed. They will both be null
/// if this is a result of a poll shutdown.
type WorkerPollCallback = unsafe extern "C" fn(
    user_data: *mut libc::c_void,
    success: *const ByteArray,
    fail: *const ByteArray,
);

#[no_mangle]
pub extern "C" fn worker_poll_workflow_activation(
    worker: *mut Worker,
    user_data: *mut libc::c_void,
    callback: WorkerPollCallback,
) {
    let worker = unsafe { &*worker };
    let user_data = UserDataHandle(user_data);
    let core_worker = worker.worker.as_ref().unwrap().clone();
    worker.runtime.core.tokio_handle().spawn(async move {
        let (success, fail) = match core_worker.poll_workflow_activation().await {
            Ok(act) => (
                ByteArray::from_vec(act.encode_to_vec())
                    .into_raw()
                    .cast_const(),
                std::ptr::null(),
            ),
            Err(PollWfError::ShutDown) => (std::ptr::null(), std::ptr::null()),
            Err(err) => (
                std::ptr::null(),
                worker
                    .runtime
                    .clone()
                    .alloc_utf8(&format!("Poll failure: {}", err))
                    .into_raw()
                    .cast_const(),
            ),
        };
        unsafe {
            callback(user_data.into(), success, fail);
        }
    });
}

#[no_mangle]
pub extern "C" fn worker_poll_activity_task(
    worker: *mut Worker,
    user_data: *mut libc::c_void,
    callback: WorkerPollCallback,
) {
    let worker = unsafe { &*worker };
    let user_data = UserDataHandle(user_data);
    let core_worker = worker.worker.as_ref().unwrap().clone();
    worker.runtime.core.tokio_handle().spawn(async move {
        let (success, fail) = match core_worker.poll_activity_task().await {
            Ok(act) => (
                ByteArray::from_vec(act.encode_to_vec())
                    .into_raw()
                    .cast_const(),
                std::ptr::null(),
            ),
            Err(PollActivityError::ShutDown) => (std::ptr::null(), std::ptr::null()),
            Err(err) => (
                std::ptr::null(),
                worker
                    .runtime
                    .clone()
                    .alloc_utf8(&format!("Poll failure: {}", err))
                    .into_raw()
                    .cast_const(),
            ),
        };
        unsafe {
            callback(user_data.into(), success, fail);
        }
    });
}

/// If fail is present, it must be freed.
type WorkerCallback = unsafe extern "C" fn(user_data: *mut libc::c_void, fail: *const ByteArray);

#[no_mangle]
pub extern "C" fn worker_complete_workflow_activation(
    worker: *mut Worker,
    completion: ByteArrayRef,
    user_data: *mut libc::c_void,
    callback: WorkerCallback,
) {
    let worker = unsafe { &*worker };
    let completion = match WorkflowActivationCompletion::decode(completion.to_slice()) {
        Ok(completion) => completion,
        Err(err) => {
            unsafe {
                callback(
                    user_data.into(),
                    worker
                        .runtime
                        .clone()
                        .alloc_utf8(&format!("Decode failure: {}", err))
                        .into_raw(),
                );
            }
            return;
        }
    };
    let user_data = UserDataHandle(user_data);
    let core_worker = worker.worker.as_ref().unwrap().clone();
    worker.runtime.core.tokio_handle().spawn(async move {
        let fail = match core_worker.complete_workflow_activation(completion).await {
            Ok(_) => std::ptr::null(),
            Err(err) => worker
                .runtime
                .clone()
                .alloc_utf8(&format!("Completion failure: {}", err))
                .into_raw()
                .cast_const(),
        };
        unsafe {
            callback(user_data.into(), fail);
        }
    });
}

#[no_mangle]
pub extern "C" fn worker_complete_activity_task(
    worker: *mut Worker,
    completion: ByteArrayRef,
    user_data: *mut libc::c_void,
    callback: WorkerCallback,
) {
    let worker = unsafe { &*worker };
    let completion = match ActivityTaskCompletion::decode(completion.to_slice()) {
        Ok(completion) => completion,
        Err(err) => {
            unsafe {
                callback(
                    user_data.into(),
                    worker
                        .runtime
                        .clone()
                        .alloc_utf8(&format!("Decode failure: {}", err))
                        .into_raw(),
                );
            }
            return;
        }
    };
    let user_data = UserDataHandle(user_data);
    let core_worker = worker.worker.as_ref().unwrap().clone();
    worker.runtime.core.tokio_handle().spawn(async move {
        let fail = match core_worker.complete_activity_task(completion).await {
            Ok(_) => std::ptr::null(),
            Err(err) => worker
                .runtime
                .clone()
                .alloc_utf8(&format!("Completion failure: {}", err))
                .into_raw()
                .cast_const(),
        };
        unsafe {
            callback(user_data.into(), fail);
        }
    });
}

/// Returns error if any. Must be freed if returned.
#[no_mangle]
pub extern "C" fn worker_record_activity_heartbeat(
    worker: *mut Worker,
    heartbeat: ByteArrayRef,
) -> *const ByteArray {
    let worker = unsafe { &*worker };
    enter_sync!(worker.runtime);
    match ActivityHeartbeat::decode(heartbeat.to_slice()) {
        Ok(heartbeat) => {
            worker
                .worker
                .as_ref()
                .unwrap()
                .record_activity_heartbeat(heartbeat);
            std::ptr::null()
        }
        Err(err) => worker
            .runtime
            .clone()
            .alloc_utf8(&format!("Decode failure: {}", err))
            .into_raw(),
    }
}

#[no_mangle]
pub extern "C" fn worker_request_workflow_eviction(worker: *mut Worker, run_id: ByteArrayRef) {
    let worker = unsafe { &*worker };
    enter_sync!(worker.runtime);
    worker
        .worker
        .as_ref()
        .unwrap()
        .request_workflow_eviction(run_id.to_str());
}

#[no_mangle]
pub extern "C" fn worker_initiate_shutdown(
    worker: *mut Worker,
) {
    let worker = unsafe { &*worker };
    worker.worker.as_ref().unwrap().initiate_shutdown();
}

#[no_mangle]
pub extern "C" fn worker_finalize_shutdown(
    worker: *mut Worker,
    user_data: *mut libc::c_void,
    callback: WorkerCallback,
) {
    let worker = unsafe { &mut *worker };
    let user_data = UserDataHandle(user_data);
    worker.runtime.core.tokio_handle().spawn(async move {
        // Take the worker out of the option and leave None. This should be the
        // only reference remaining to the worker so try_unwrap will work.
        let core_worker = match Arc::try_unwrap(worker.worker.take().unwrap()) {
            Ok(core_worker) => core_worker,
            Err(arc) => {
                unsafe {
                    callback(
                        user_data.into(),
                        worker
                            .runtime
                            .clone()
                            .alloc_utf8(&format!(
                                "Cannot finalize, expected 1 reference, got {}",
                                Arc::strong_count(&arc)
                            ))
                            .into_raw(),
                    );
                }
                return;
            }
        };
        core_worker.finalize_shutdown().await;
        unsafe {
            callback(user_data.into(), std::ptr::null());
        }
    });
}

impl TryFrom<&WorkerOptions> for temporal_sdk_core::WorkerConfig {
    type Error = anyhow::Error;

    fn try_from(opt: &WorkerOptions) -> anyhow::Result<Self> {
        WorkerConfigBuilder::default()
            .namespace(opt.namespace.to_str())
            .task_queue(opt.task_queue.to_str())
            .worker_build_id(opt.build_id.to_str())
            .client_identity_override(opt.identity_override.to_option_string())
            .max_cached_workflows(opt.max_cached_workflows as usize)
            .max_outstanding_workflow_tasks(opt.max_outstanding_workflow_tasks as usize)
            .max_outstanding_activities(opt.max_outstanding_activities as usize)
            .max_outstanding_local_activities(opt.max_outstanding_local_activities as usize)
            .no_remote_activities(opt.no_remote_activities)
            .sticky_queue_schedule_to_start_timeout(Duration::from_millis(
                opt.sticky_queue_schedule_to_start_timeout_millis,
            ))
            .max_heartbeat_throttle_interval(Duration::from_millis(
                opt.max_heartbeat_throttle_interval_millis,
            ))
            .default_heartbeat_throttle_interval(Duration::from_millis(
                opt.default_heartbeat_throttle_interval_millis,
            ))
            .max_worker_activities_per_second(if opt.max_activities_per_second == 0.0 {
                None
            } else {
                Some(opt.max_activities_per_second)
            })
            .max_task_queue_activities_per_second(
                if opt.max_task_queue_activities_per_second == 0.0 {
                    None
                } else {
                    Some(opt.max_task_queue_activities_per_second)
                },
            )
            // Even though grace period is optional, if it is not set then the
            // auto-cancel-activity behavior of shutdown will not occur, so we
            // always set it even if 0.
            .graceful_shutdown_period(Duration::from_millis(opt.graceful_shutdown_period_millis))
            .build()
            .map_err(|err| anyhow::anyhow!(err))
    }
}
