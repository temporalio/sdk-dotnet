use crate::client::Client;
use crate::runtime::Runtime;
use crate::ByteArray;
use crate::ByteArrayRef;
use crate::ByteArrayRefArray;
use crate::UserDataHandle;
use anyhow::{bail, Context};
use prost::Message;
use temporal_sdk_core::replay::HistoryForReplay;
use temporal_sdk_core::replay::ReplayWorkerInput;
use temporal_sdk_core::WorkerConfigBuilder;
use temporal_sdk_core_api::errors::PollError;
use temporal_sdk_core_api::errors::WorkflowErrorType;
use temporal_sdk_core_api::worker::SlotInfoTrait;
use temporal_sdk_core_api::worker::SlotKind;
use temporal_sdk_core_api::worker::SlotMarkUsedContext;
use temporal_sdk_core_api::worker::SlotReleaseContext;
use temporal_sdk_core_api::worker::SlotReservationContext;
use temporal_sdk_core_api::worker::SlotSupplierPermit;
use temporal_sdk_core_api::Worker as CoreWorker;
use temporal_sdk_core_protos::coresdk::workflow_completion::WorkflowActivationCompletion;
use temporal_sdk_core_protos::coresdk::ActivityHeartbeat;
use temporal_sdk_core_protos::coresdk::ActivityTaskCompletion;
use temporal_sdk_core_protos::temporal::api::history::v1::History;
use tokio::sync::mpsc::{channel, Sender};
use tokio::sync::oneshot;
use tokio_stream::wrappers::ReceiverStream;

use std::collections::HashMap;
use std::collections::HashSet;
use std::sync::Arc;
use std::time::Duration;

#[repr(C)]
pub struct WorkerOptions {
    namespace: ByteArrayRef,
    task_queue: ByteArrayRef,
    build_id: ByteArrayRef,
    identity_override: ByteArrayRef,
    max_cached_workflows: u32,
    tuner: TunerHolder,
    no_remote_activities: bool,
    sticky_queue_schedule_to_start_timeout_millis: u64,
    max_heartbeat_throttle_interval_millis: u64,
    default_heartbeat_throttle_interval_millis: u64,
    max_activities_per_second: f64,
    max_task_queue_activities_per_second: f64,
    graceful_shutdown_period_millis: u64,
    use_worker_versioning: bool,
    max_concurrent_workflow_task_polls: u32,
    nonsticky_to_sticky_poll_ratio: f32,
    max_concurrent_activity_task_polls: u32,
    nondeterminism_as_workflow_fail: bool,
    nondeterminism_as_workflow_fail_for_types: ByteArrayRefArray,
}

#[repr(C)]
pub struct TunerHolder {
    workflow_slot_supplier: SlotSupplier,
    activity_slot_supplier: SlotSupplier,
    local_activity_slot_supplier: SlotSupplier,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub enum SlotSupplier {
    FixedSize(FixedSizeSlotSupplier),
    ResourceBased(ResourceBasedSlotSupplier),
    Custom(CustomSlotSupplierCallbacksImpl),
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct FixedSizeSlotSupplier {
    num_slots: usize,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct ResourceBasedSlotSupplier {
    minimum_slots: usize,
    maximum_slots: usize,
    ramp_throttle_ms: u64,
    tuner_options: ResourceBasedTunerOptions,
}

#[repr(C)]
pub struct CustomSlotSupplier<SK> {
    inner: CustomSlotSupplierCallbacksImpl,
    _pd: std::marker::PhantomData<SK>,
}

unsafe impl<SK> Send for CustomSlotSupplier<SK> {}
unsafe impl<SK> Sync for CustomSlotSupplier<SK> {}

type CustomReserveSlotCallback =
    unsafe extern "C" fn(ctx: *const SlotReserveCtx, sender: *mut libc::c_void);
type CustomCancelReserveCallback = unsafe extern "C" fn(token_source: *mut libc::c_void);
/// Must return C#-tracked id for the permit. A zero value means no permit was reserved.
type CustomTryReserveSlotCallback = unsafe extern "C" fn(ctx: *const SlotReserveCtx) -> usize;
type CustomMarkSlotUsedCallback = unsafe extern "C" fn(ctx: *const SlotMarkUsedCtx);
type CustomReleaseSlotCallback = unsafe extern "C" fn(ctx: *const SlotReleaseCtx);
type CustomSlotImplFreeCallback =
    unsafe extern "C" fn(userimpl: *const CustomSlotSupplierCallbacks);

#[repr(C)]
#[derive(Clone, Copy)]
pub struct CustomSlotSupplierCallbacksImpl(*const CustomSlotSupplierCallbacks);

#[repr(C)]
pub struct CustomSlotSupplierCallbacks {
    reserve: CustomReserveSlotCallback,
    cancel_reserve: CustomCancelReserveCallback,
    try_reserve: CustomTryReserveSlotCallback,
    mark_used: CustomMarkSlotUsedCallback,
    release: CustomReleaseSlotCallback,
    free: CustomSlotImplFreeCallback,
}

impl CustomSlotSupplierCallbacksImpl {
    fn into_ss<SK: SlotKind + Send + Sync + 'static>(
        self,
    ) -> Arc<dyn temporal_sdk_core_api::worker::SlotSupplier<SlotKind = SK> + Send + Sync + 'static>
    {
        Arc::new(CustomSlotSupplier {
            inner: self,
            _pd: Default::default(),
        })
    }
}
impl Drop for CustomSlotSupplierCallbacks {
    fn drop(&mut self) {
        unsafe {
            (self.free)(&*self);
        }
    }
}

#[repr(C)]
pub enum SlotKindType {
    WorkflowSlotKindType,
    ActivitySlotKindType,
    LocalActivitySlotKindType,
    NexusSlotKindType,
}

#[repr(C)]
pub struct SlotReserveCtx {
    slot_type: SlotKindType,
    task_queue: ByteArrayRef,
    worker_identity: ByteArrayRef,
    worker_build_id: ByteArrayRef,
    is_sticky: bool,
    // The C# side will store a pointer here to the cancellation token source
    token_src: *mut libc::c_void,
}
unsafe impl Send for SlotReserveCtx {}

#[repr(C)]
pub enum SlotInfo {
    WorkflowSlotInfo {
        workflow_type: ByteArrayRef,
        is_sticky: bool,
    },
    ActivitySlotInfo {
        activity_type: ByteArrayRef,
    },
    LocalActivitySlotInfo {
        activity_type: ByteArrayRef,
    },
    NexusSlotInfo {
        operation: ByteArrayRef,
        service: ByteArrayRef,
    },
}

#[repr(C)]
pub struct SlotMarkUsedCtx {
    slot_info: SlotInfo,
    /// C# id for the slot permit.
    slot_permit: usize,
}

#[repr(C)]
pub struct SlotReleaseCtx {
    slot_info: *const SlotInfo,
    /// C# id for the slot permit.
    slot_permit: usize,
}

struct CancelReserveGuard {
    token_src: *mut libc::c_void,
    callback: CustomCancelReserveCallback,
}
impl Drop for CancelReserveGuard {
    fn drop(&mut self) {
        if !self.token_src.is_null() {
            unsafe {
                (self.callback)(self.token_src);
            }
        }
    }
}
unsafe impl Send for CancelReserveGuard {}

#[async_trait::async_trait]
impl<SK: SlotKind + Send + Sync> temporal_sdk_core_api::worker::SlotSupplier
    for CustomSlotSupplier<SK>
{
    type SlotKind = SK;

    async fn reserve_slot(&self, ctx: &dyn SlotReservationContext) -> SlotSupplierPermit {
        let (tx, rx) = oneshot::channel();
        let ctx = Self::convert_reserve_ctx(ctx);
        let tx = Box::into_raw(Box::new(tx)) as *mut libc::c_void;
        unsafe {
            let _drop_guard = CancelReserveGuard {
                token_src: ctx.token_src,
                callback: (*self.inner.0).cancel_reserve,
            };
            ((*self.inner.0).reserve)(&ctx, tx);
            rx.await.expect("reserve channel is not closed")
        }
    }

    fn try_reserve_slot(&self, ctx: &dyn SlotReservationContext) -> Option<SlotSupplierPermit> {
        let ctx = Self::convert_reserve_ctx(ctx);
        let permit_id = unsafe { ((*self.inner.0).try_reserve)(&ctx) };
        if permit_id == 0 {
            None
        } else {
            Some(SlotSupplierPermit::with_user_data(permit_id))
        }
    }

    fn mark_slot_used(&self, ctx: &dyn SlotMarkUsedContext<SlotKind = Self::SlotKind>) {
        let ctx = SlotMarkUsedCtx {
            slot_info: Self::convert_slot_info(ctx.info().downcast()),
            slot_permit: ctx.permit().user_data::<usize>().map(|u| *u).unwrap_or(0),
        };
        unsafe {
            ((*self.inner.0).mark_used)(&ctx);
        }
    }

    fn release_slot(&self, ctx: &dyn SlotReleaseContext<SlotKind = Self::SlotKind>) {
        let mut info_ptr = std::ptr::null();
        let converted_slot_info = ctx.info().map(|i| Self::convert_slot_info(i.downcast()));
        if let Some(ref converted) = converted_slot_info {
            info_ptr = converted;
        }
        let ctx = SlotReleaseCtx {
            slot_info: info_ptr,
            slot_permit: ctx.permit().user_data::<usize>().map(|u| *u).unwrap_or(0),
        };
        unsafe {
            ((*self.inner.0).release)(&ctx);
        }
    }

    fn available_slots(&self) -> Option<usize> {
        None
    }
}

impl<SK: SlotKind + Send + Sync> CustomSlotSupplier<SK> {
    fn convert_reserve_ctx(ctx: &dyn SlotReservationContext) -> SlotReserveCtx {
        SlotReserveCtx {
            slot_type: match SK::kind() {
                temporal_sdk_core_api::worker::SlotKindType::Workflow => {
                    SlotKindType::WorkflowSlotKindType
                }
                temporal_sdk_core_api::worker::SlotKindType::Activity => {
                    SlotKindType::ActivitySlotKindType
                }
                temporal_sdk_core_api::worker::SlotKindType::LocalActivity => {
                    SlotKindType::LocalActivitySlotKindType
                }
                temporal_sdk_core_api::worker::SlotKindType::Nexus => {
                    SlotKindType::NexusSlotKindType
                }
            },
            task_queue: ctx.task_queue().into(),
            worker_identity: ctx.worker_identity().into(),
            worker_build_id: ctx.worker_build_id().into(),
            is_sticky: ctx.is_sticky(),
            token_src: std::ptr::null_mut(),
        }
    }

    fn convert_slot_info(info: temporal_sdk_core_api::worker::SlotInfo) -> SlotInfo {
        match info {
            temporal_sdk_core_api::worker::SlotInfo::Workflow(w) => SlotInfo::WorkflowSlotInfo {
                workflow_type: w.workflow_type.as_str().into(),
                is_sticky: w.is_sticky,
            },
            temporal_sdk_core_api::worker::SlotInfo::Activity(a) => SlotInfo::ActivitySlotInfo {
                activity_type: a.activity_type.as_str().into(),
            },
            temporal_sdk_core_api::worker::SlotInfo::LocalActivity(a) => {
                SlotInfo::LocalActivitySlotInfo {
                    activity_type: a.activity_type.as_str().into(),
                }
            }
            temporal_sdk_core_api::worker::SlotInfo::Nexus(n) => SlotInfo::NexusSlotInfo {
                operation: n.operation.as_str().into(),
                service: n.operation.as_str().into(),
            },
        }
    }
}

#[repr(C)]
#[derive(Clone, Copy, PartialEq, Debug)]
pub struct ResourceBasedTunerOptions {
    target_memory_usage: f64,
    target_cpu_usage: f64,
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

pub struct WorkerReplayPusher {
    tx: Sender<HistoryForReplay>,
}

#[repr(C)]
pub struct WorkerReplayerOrFail {
    worker: *mut Worker,
    worker_replay_pusher: *mut WorkerReplayPusher,
    fail: *const ByteArray,
}

#[repr(C)]
pub struct WorkerReplayPushResult {
    fail: *const ByteArray,
}

macro_rules! enter_sync {
    ($runtime:expr) => {
        if let Some(subscriber) = $runtime.core.telemetry().trace_subscriber() {
            temporal_sdk_core::telemetry::set_trace_subscriber_for_current_thread(subscriber);
        }
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
    if worker.is_null() {
        return;
    }
    unsafe {
        let _ = Box::from_raw(worker);
    }
}

/// If fail is present, it must be freed.
type WorkerCallback = unsafe extern "C" fn(user_data: *mut libc::c_void, fail: *const ByteArray);

#[no_mangle]
pub extern "C" fn worker_validate(
    worker: *mut Worker,
    user_data: *mut libc::c_void,
    callback: WorkerCallback,
) {
    let worker = unsafe { &*worker };
    let user_data = UserDataHandle(user_data);
    let core_worker = worker.worker.as_ref().unwrap().clone();
    worker.runtime.core.tokio_handle().spawn(async move {
        let fail = match core_worker.validate().await {
            Ok(_) => std::ptr::null(),
            Err(err) => worker
                .runtime
                .clone()
                .alloc_utf8(&format!("Worker validation failed: {}", err))
                .into_raw()
                .cast_const(),
        };
        unsafe {
            callback(user_data.into(), fail);
        }
    });
}

#[no_mangle]
pub extern "C" fn worker_replace_client(worker: *mut Worker, new_client: *mut Client) {
    let worker = unsafe { &*worker };
    let core_worker = worker.worker.as_ref().expect("missing worker").clone();
    let client = unsafe { &*new_client };
    core_worker.replace_client(client.core.get_client().clone());
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
            Err(PollError::ShutDown) => (std::ptr::null(), std::ptr::null()),
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
            Err(PollError::ShutDown) => (std::ptr::null(), std::ptr::null()),
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
pub extern "C" fn worker_initiate_shutdown(worker: *mut Worker) {
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

#[no_mangle]
pub extern "C" fn worker_replayer_new(
    runtime: *mut Runtime,
    options: *const WorkerOptions,
) -> WorkerReplayerOrFail {
    let runtime = unsafe { &mut *runtime };
    enter_sync!(runtime);
    let options = unsafe { &*options };

    let (worker, worker_replay_pusher, fail) = match options.try_into() {
        Err(err) => (
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            runtime
                .alloc_utf8(&format!("Invalid options: {}", err))
                .into_raw()
                .cast_const(),
        ),
        Ok(config) => {
            let (tx, rx) = channel(1);
            match temporal_sdk_core::init_replay_worker(ReplayWorkerInput::new(
                config,
                ReceiverStream::new(rx),
            )) {
                Err(err) => (
                    std::ptr::null_mut(),
                    std::ptr::null_mut(),
                    runtime
                        .alloc_utf8(&format!("Worker replay init failed: {}", err))
                        .into_raw()
                        .cast_const(),
                ),
                Ok(worker) => (
                    Box::into_raw(Box::new(Worker {
                        worker: Some(Arc::new(worker)),
                        runtime: runtime.clone(),
                    })),
                    Box::into_raw(Box::new(WorkerReplayPusher { tx: tx })),
                    std::ptr::null(),
                ),
            }
        }
    };
    WorkerReplayerOrFail {
        worker,
        worker_replay_pusher,
        fail,
    }
}

#[no_mangle]
pub extern "C" fn worker_replay_pusher_free(worker_replay_pusher: *mut WorkerReplayPusher) {
    unsafe {
        let _ = Box::from_raw(worker_replay_pusher);
    }
}

#[no_mangle]
pub extern "C" fn worker_replay_push(
    worker: *mut Worker,
    worker_replay_pusher: *mut WorkerReplayPusher,
    workflow_id: ByteArrayRef,
    history: ByteArrayRef,
) -> WorkerReplayPushResult {
    let worker = unsafe { &mut *worker };
    let worker_replay_pusher = unsafe { &*worker_replay_pusher };
    let workflow_id = workflow_id.to_string();
    match History::decode(history.to_slice()) {
        Err(err) => {
            return WorkerReplayPushResult {
                fail: worker
                    .runtime
                    .alloc_utf8(&format!("Worker replay init failed: {}", err))
                    .into_raw()
                    .cast_const(),
            }
        }
        Ok(history) => worker.runtime.core.tokio_handle().spawn(async move {
            // Intentionally ignoring error here
            let _ = worker_replay_pusher
                .tx
                .send(HistoryForReplay::new(history, workflow_id))
                .await;
        }),
    };
    WorkerReplayPushResult {
        fail: std::ptr::null(),
    }
}

#[no_mangle]
pub extern "C" fn complete_async_reserve(sender: *mut libc::c_void, permit_id: usize) {
    if !sender.is_null() {
        unsafe {
            let sender = Box::from_raw(sender as *mut oneshot::Sender<SlotSupplierPermit>);
            let permit = SlotSupplierPermit::with_user_data(permit_id);
            let _ = sender.send(permit);
        }
    } else {
        panic!("ReserveSlot sender must not be null!");
    }
}

#[no_mangle]
pub unsafe extern "C" fn set_reserve_cancel_target(
    ctx: *mut SlotReserveCtx,
    token_ptr: *mut libc::c_void,
) {
    if let Some(ctx) = ctx.as_mut() {
        ctx.token_src = token_ptr;
    }
}

impl TryFrom<&WorkerOptions> for temporal_sdk_core::WorkerConfig {
    type Error = anyhow::Error;

    fn try_from(opt: &WorkerOptions) -> anyhow::Result<Self> {
        let converted_tuner: temporal_sdk_core::TunerHolder = (&opt.tuner).try_into()?;
        WorkerConfigBuilder::default()
            .namespace(opt.namespace.to_str())
            .task_queue(opt.task_queue.to_str())
            .worker_build_id(opt.build_id.to_str())
            .use_worker_versioning(opt.use_worker_versioning)
            .client_identity_override(opt.identity_override.to_option_string())
            .max_cached_workflows(opt.max_cached_workflows as usize)
            .tuner(Arc::new(converted_tuner))
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
            // auto-cancel-activity behavior or shutdown will not occur, so we
            // always set it even if 0.
            .graceful_shutdown_period(Duration::from_millis(opt.graceful_shutdown_period_millis))
            .max_concurrent_wft_polls(opt.max_concurrent_workflow_task_polls as usize)
            .nonsticky_to_sticky_poll_ratio(opt.nonsticky_to_sticky_poll_ratio)
            .max_concurrent_at_polls(opt.max_concurrent_activity_task_polls as usize)
            .workflow_failure_errors(if opt.nondeterminism_as_workflow_fail {
                HashSet::from([WorkflowErrorType::Nondeterminism])
            } else {
                HashSet::new()
            })
            .workflow_types_to_failure_errors(
                opt.nondeterminism_as_workflow_fail_for_types
                    .to_str_vec()
                    .into_iter()
                    .map(|s| {
                        (
                            s.to_owned(),
                            HashSet::from([WorkflowErrorType::Nondeterminism]),
                        )
                    })
                    .collect::<HashMap<String, HashSet<WorkflowErrorType>>>(),
            )
            .build()
            .map_err(|err| anyhow::anyhow!(err))
    }
}

impl TryFrom<&TunerHolder> for temporal_sdk_core::TunerHolder {
    type Error = anyhow::Error;

    fn try_from(holder: &TunerHolder) -> anyhow::Result<Self> {
        // Verify all resource-based options are the same if any are set
        let maybe_wf_resource_opts =
            if let SlotSupplier::ResourceBased(ref ss) = holder.workflow_slot_supplier {
                Some(&ss.tuner_options)
            } else {
                None
            };
        let maybe_act_resource_opts =
            if let SlotSupplier::ResourceBased(ref ss) = holder.activity_slot_supplier {
                Some(&ss.tuner_options)
            } else {
                None
            };
        let maybe_local_act_resource_opts =
            if let SlotSupplier::ResourceBased(ref ss) = holder.local_activity_slot_supplier {
                Some(&ss.tuner_options)
            } else {
                None
            };
        let all_resource_opts = [
            maybe_wf_resource_opts,
            maybe_act_resource_opts,
            maybe_local_act_resource_opts,
        ];
        let mut set_resource_opts = all_resource_opts.iter().flatten();
        let first = set_resource_opts.next();
        let all_are_same = if let Some(first) = first {
            set_resource_opts.all(|elem| elem == first)
        } else {
            true
        };
        if !all_are_same {
            bail!("All resource-based slot suppliers must have the same ResourceBasedTunerOptions",);
        }

        let mut options = temporal_sdk_core::TunerHolderOptionsBuilder::default();
        if let Some(first) = first {
            options.resource_based_options(
                temporal_sdk_core::ResourceBasedSlotsOptionsBuilder::default()
                    .target_mem_usage(first.target_memory_usage)
                    .target_cpu_usage(first.target_cpu_usage)
                    .build()
                    .expect("Building ResourceBasedSlotsOptions is infallible"),
            );
        };
        options
            .workflow_slot_options(holder.workflow_slot_supplier.try_into()?)
            .activity_slot_options(holder.activity_slot_supplier.try_into()?)
            .local_activity_slot_options(holder.local_activity_slot_supplier.try_into()?);
        Ok(options
            .build()
            .context("Invalid tuner holder options")?
            .build_tuner_holder()
            .context("Failed building tuner holder")?)
    }
}

impl<SK: SlotKind + Send + Sync + 'static> TryFrom<SlotSupplier>
    for temporal_sdk_core::SlotSupplierOptions<SK>
{
    type Error = anyhow::Error;

    fn try_from(
        supplier: SlotSupplier,
    ) -> anyhow::Result<temporal_sdk_core::SlotSupplierOptions<SK>> {
        Ok(match supplier {
            SlotSupplier::FixedSize(fs) => temporal_sdk_core::SlotSupplierOptions::FixedSize {
                slots: fs.num_slots,
            },
            SlotSupplier::ResourceBased(ss) => {
                temporal_sdk_core::SlotSupplierOptions::ResourceBased(
                    temporal_sdk_core::ResourceSlotOptions::new(
                        ss.minimum_slots,
                        ss.maximum_slots,
                        Duration::from_millis(ss.ramp_throttle_ms),
                    ),
                )
            }
            SlotSupplier::Custom(cs) => {
                temporal_sdk_core::SlotSupplierOptions::Custom(cs.into_ss())
            }
        })
    }
}
