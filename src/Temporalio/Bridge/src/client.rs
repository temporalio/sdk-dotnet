use crate::runtime::Runtime;
use crate::ByteArray;
use crate::ByteArrayRef;
use crate::CancellationToken;
use crate::UserDataHandle;

use parking_lot::RwLock;
use std::str::FromStr;
use std::sync::Arc;
use std::time::Duration;
use temporal_client::{
    ClientOptions as CoreClientOptions, ClientOptionsBuilder, ClientTlsConfig, ConfiguredClient,
    HealthService, OperatorService, RetryClient, RetryConfig, TemporalServiceClientWithMetrics,
    TestService, TlsConfig, WorkflowService,
};
use tonic::metadata::MetadataKey;
use url::Url;

/// Metadata is <key1>\n<value1>\n<key2>\n<value2>. Metadata keys or
/// values cannot contain a newline within.
type MetadataRef = ByteArrayRef;

#[repr(C)]
pub struct ClientOptions {
    target_url: ByteArrayRef,
    client_name: ByteArrayRef,
    client_version: ByteArrayRef,
    metadata: MetadataRef,
    identity: ByteArrayRef,
    tls_options: *const ClientTlsOptions,
    retry_options: *const ClientRetryOptions,
}

#[repr(C)]
pub struct ClientTlsOptions {
    server_root_ca_cert: ByteArrayRef,
    domain: ByteArrayRef,
    client_cert: ByteArrayRef,
    client_private_key: ByteArrayRef,
}

#[repr(C)]
pub struct ClientRetryOptions {
    pub initial_interval_millis: u64,
    pub randomization_factor: f64,
    pub multiplier: f64,
    pub max_interval_millis: u64,
    pub max_elapsed_time_millis: u64,
    pub max_retries: usize,
}

type CoreClient = RetryClient<ConfiguredClient<TemporalServiceClientWithMetrics>>;

pub struct Client {
    pub(crate) runtime: Runtime,
    pub(crate) core: CoreClient,
}

// Expected to outlive all async calls that use it
unsafe impl Send for Client {}
unsafe impl Sync for Client {}

/// If success or fail are not null, they must be manually freed when done.
type ClientConnectCallback = unsafe extern "C" fn(
    user_data: *mut libc::c_void,
    success: *mut Client,
    fail: *const ByteArray,
);

/// Runtime must live as long as client. Options and user data must live through
/// callback.
#[no_mangle]
pub extern "C" fn client_connect(
    runtime: *mut Runtime,
    options: *const ClientOptions,
    user_data: *mut libc::c_void,
    callback: ClientConnectCallback,
) {
    let runtime = unsafe { &mut *runtime };
    // Convert opts
    let options = unsafe { &*options };
    let headers = if options.metadata.size == 0 {
        None
    } else {
        Some(Arc::new(RwLock::new(
            options.metadata.to_string_map_on_newlines(),
        )))
    };
    let core_options: CoreClientOptions = match options.try_into() {
        Ok(v) => v,
        Err(err) => {
            unsafe {
                callback(
                    user_data,
                    std::ptr::null_mut(),
                    runtime
                        .alloc_utf8(&format!("Invalid options: {}", err))
                        .into_raw(),
                );
            }
            return;
        }
    };
    // Spawn async call
    let user_data = UserDataHandle(user_data);
    let core = runtime.core.clone();
    runtime.core.tokio_handle().spawn(async move {
        match core_options
            .connect_no_namespace(core.metric_meter().as_deref(), headers)
            .await
        {
            Ok(core) => {
                let owned_client = Box::into_raw(Box::new(Client {
                    runtime: runtime.clone(),
                    core,
                }));
                unsafe {
                    callback(user_data.into(), owned_client, std::ptr::null());
                }
            }
            Err(err) => unsafe {
                callback(
                    user_data.into(),
                    std::ptr::null_mut(),
                    runtime
                        .alloc_utf8(&format!("Connection failed: {}", err))
                        .into_raw(),
                );
            },
        }
    });
}

#[no_mangle]
pub extern "C" fn client_free(client: *mut Client) {
    unsafe {
        let _ = Box::from_raw(client);
    }
}

#[no_mangle]
pub extern "C" fn client_update_metadata(client: *mut Client, metadata: ByteArrayRef) {
    let client = unsafe { &*client };
    client
        .core
        .get_client()
        .set_headers(metadata.to_string_map_on_newlines());
}

#[repr(C)]
pub struct RpcCallOptions {
    service: RpcService,
    rpc: ByteArrayRef,
    req: ByteArrayRef,
    retry: bool,
    metadata: MetadataRef,
    /// 0 means no timeout
    timeout_millis: u32,
    cancellation_token: *const CancellationToken,
}

// Expected to outlive all async calls that use it
unsafe impl Send for RpcCallOptions {}
unsafe impl Sync for RpcCallOptions {}

#[repr(C)]
pub enum RpcService {
    Workflow = 1,
    Operator,
    Test,
    Health,
}

/// If success or failure byte arrays inside fail are not null, they must be
/// manually freed when done. Either success or failure_message are always
/// present. Status code may still be 0 with a failure message. Failure details
/// represent a protobuf gRPC status message.
type ClientRpcCallCallback = unsafe extern "C" fn(
    user_data: *mut libc::c_void,
    success: *const ByteArray,
    status_code: u32,
    failure_message: *const ByteArray,
    failure_details: *const ByteArray,
);

macro_rules! service_call {
    ($service_fn:ident, $client:ident, $options:ident, $cancel_token:ident) => {{
        let call_future = $service_fn(&$client.core, &$options);
        if let Some(cancel_token) = $cancel_token {
            tokio::select! {
                _ = cancel_token.cancelled() => Err(anyhow::anyhow!("Cancelled")),
                v = call_future => v,
            }
        } else {
            call_future.await
        }
    }};
}

/// Client, options, and user data must live through callback.
#[no_mangle]
pub extern "C" fn client_rpc_call(
    client: *mut Client,
    options: *const RpcCallOptions,
    user_data: *mut libc::c_void,
    callback: ClientRpcCallCallback,
) {
    let client = unsafe { &*client };
    let options = unsafe { &*options };
    let cancel_token = unsafe { options.cancellation_token.as_ref() }.map(|v| v.token.clone());
    let user_data = UserDataHandle(user_data);
    client.runtime.core.tokio_handle().spawn(async move {
        let res = match options.service {
            RpcService::Workflow => {
                service_call!(call_workflow_service, client, options, cancel_token)
            }
            RpcService::Operator => {
                service_call!(call_operator_service, client, options, cancel_token)
            }
            RpcService::Test => service_call!(call_test_service, client, options, cancel_token),
            RpcService::Health => service_call!(call_health_service, client, options, cancel_token),
        };
        let (success, status_code, failure_message, failure_details) = match res {
            Ok(b) => (
                ByteArray::from_vec(b).into_raw(),
                0,
                std::ptr::null_mut(),
                std::ptr::null_mut(),
            ),
            Err(err) => match err.downcast::<tonic::Status>() {
                Ok(status) => (
                    std::ptr::null_mut(),
                    status.code() as u32,
                    ByteArray::from_utf8(status.message().to_string()).into_raw(),
                    ByteArray::from_vec(status.details().to_owned()).into_raw(),
                ),
                Err(err) => (
                    std::ptr::null_mut(),
                    0,
                    ByteArray::from_utf8(format!("{}", err)).into_raw(),
                    std::ptr::null_mut(),
                ),
            },
        };
        unsafe {
            callback(
                user_data.into(),
                success,
                status_code,
                failure_message,
                failure_details,
            );
        }
    });
}

macro_rules! rpc_call {
    ($client:ident, $call:ident, $call_name:ident) => {
        if $call.retry {
            rpc_resp($client.$call_name(rpc_req($call)?).await)
        } else {
            rpc_resp($client.into_inner().$call_name(rpc_req($call)?).await)
        }
    };
}

async fn call_workflow_service(
    client: &CoreClient,
    call: &RpcCallOptions,
) -> anyhow::Result<Vec<u8>> {
    let rpc = call.rpc.to_str();
    let mut client = client.clone();
    match rpc {
        "CountWorkflowExecutions" => rpc_call!(client, call, count_workflow_executions),
        "CreateSchedule" => rpc_call!(client, call, create_schedule),
        "DeleteSchedule" => rpc_call!(client, call, delete_schedule),
        "DeprecateNamespace" => rpc_call!(client, call, deprecate_namespace),
        "DescribeNamespace" => rpc_call!(client, call, describe_namespace),
        "DescribeSchedule" => rpc_call!(client, call, describe_schedule),
        "DescribeTaskQueue" => rpc_call!(client, call, describe_task_queue),
        "DescribeWorkflowExecution" => rpc_call!(client, call, describe_workflow_execution),
        "GetClusterInfo" => rpc_call!(client, call, get_cluster_info),
        "GetSearchAttributes" => rpc_call!(client, call, get_search_attributes),
        "GetSystemInfo" => rpc_call!(client, call, get_system_info),
        "GetWorkerBuildIdCompatibility" => {
            rpc_call!(client, call, get_worker_build_id_compatibility)
        }
        "GetWorkflowExecutionHistory" => rpc_call!(client, call, get_workflow_execution_history),
        "GetWorkflowExecutionHistoryReverse" => {
            rpc_call!(client, call, get_workflow_execution_history_reverse)
        }
        "ListArchivedWorkflowExecutions" => {
            rpc_call!(client, call, list_archived_workflow_executions)
        }
        "ListClosedWorkflowExecutions" => rpc_call!(client, call, list_closed_workflow_executions),
        "ListNamespaces" => rpc_call!(client, call, list_namespaces),
        "ListOpenWorkflowExecutions" => rpc_call!(client, call, list_open_workflow_executions),
        "ListScheduleMatchingTimes" => rpc_call!(client, call, list_schedule_matching_times),
        "ListSchedules" => rpc_call!(client, call, list_schedules),
        "ListTaskQueuePartitions" => rpc_call!(client, call, list_task_queue_partitions),
        "ListWorkflowExecutions" => rpc_call!(client, call, list_workflow_executions),
        "PatchSchedule" => rpc_call!(client, call, patch_schedule),
        "PollActivityTaskQueue" => rpc_call!(client, call, poll_activity_task_queue),
        "PollWorkflowExecutionUpdate" => rpc_call!(client, call, poll_workflow_execution_update),
        "PollWorkflowTaskQueue" => rpc_call!(client, call, poll_workflow_task_queue),
        "QueryWorkflow" => rpc_call!(client, call, query_workflow),
        "RecordActivityTaskHeartbeat" => rpc_call!(client, call, record_activity_task_heartbeat),
        "RecordActivityTaskHeartbeatById" => {
            rpc_call!(client, call, record_activity_task_heartbeat_by_id)
        }
        "RegisterNamespace" => rpc_call!(client, call, register_namespace),
        "RequestCancelWorkflowExecution" => {
            rpc_call!(client, call, request_cancel_workflow_execution)
        }
        "ResetStickyTaskQueue" => rpc_call!(client, call, reset_sticky_task_queue),
        "ResetWorkflowExecution" => rpc_call!(client, call, reset_workflow_execution),
        "RespondActivityTaskCanceled" => rpc_call!(client, call, respond_activity_task_canceled),
        "RespondActivityTaskCanceledById" => {
            rpc_call!(client, call, respond_activity_task_canceled_by_id)
        }
        "RespondActivityTaskCompleted" => rpc_call!(client, call, respond_activity_task_completed),
        "RespondActivityTaskCompletedById" => {
            rpc_call!(client, call, respond_activity_task_completed_by_id)
        }
        "RespondActivityTaskFailed" => rpc_call!(client, call, respond_activity_task_failed),
        "RespondActivityTaskFailedById" => {
            rpc_call!(client, call, respond_activity_task_failed_by_id)
        }
        "RespondQueryTaskCompleted" => rpc_call!(client, call, respond_query_task_completed),
        "RespondWorkflowTaskCompleted" => rpc_call!(client, call, respond_workflow_task_completed),
        "RespondWorkflowTaskFailed" => rpc_call!(client, call, respond_workflow_task_failed),
        "ScanWorkflowExecutions" => rpc_call!(client, call, scan_workflow_executions),
        "SignalWithStartWorkflowExecution" => {
            rpc_call!(client, call, signal_with_start_workflow_execution)
        }
        "SignalWorkflowExecution" => rpc_call!(client, call, signal_workflow_execution),
        "StartWorkflowExecution" => rpc_call!(client, call, start_workflow_execution),
        "TerminateWorkflowExecution" => rpc_call!(client, call, terminate_workflow_execution),
        "UpdateNamespace" => rpc_call!(client, call, update_namespace),
        "UpdateSchedule" => rpc_call!(client, call, update_schedule),
        "UpdateWorkflowExecution" => rpc_call!(client, call, update_workflow_execution),
        "UpdateWorkerBuildIdCompatibility" => {
            rpc_call!(client, call, update_worker_build_id_compatibility)
        }
        rpc => Err(anyhow::anyhow!("Unknown RPC call {}", rpc)),
    }
}

async fn call_operator_service(
    client: &CoreClient,
    call: &RpcCallOptions,
) -> anyhow::Result<Vec<u8>> {
    let rpc = call.rpc.to_str();
    let mut client = client.clone();
    match rpc {
        "AddOrUpdateRemoteCluster" => rpc_call!(client, call, add_or_update_remote_cluster),
        "AddSearchAttributes" => rpc_call!(client, call, add_search_attributes),
        "DeleteNamespace" => rpc_call!(client, call, delete_namespace),
        "DeleteWorkflowExecution" => rpc_call!(client, call, delete_workflow_execution),
        "ListClusters" => rpc_call!(client, call, list_clusters),
        "ListSearchAttributes" => rpc_call!(client, call, list_search_attributes),
        "RemoveRemoteCluster" => rpc_call!(client, call, remove_remote_cluster),
        "RemoveSearchAttributes" => rpc_call!(client, call, remove_search_attributes),
        rpc => Err(anyhow::anyhow!("Unknown RPC call {}", rpc)),
    }
}

async fn call_test_service(client: &CoreClient, call: &RpcCallOptions) -> anyhow::Result<Vec<u8>> {
    let rpc = call.rpc.to_str();
    let mut client = client.clone();
    match rpc {
        "GetCurrentTime" => rpc_call!(client, call, get_current_time),
        "LockTimeSkipping" => rpc_call!(client, call, lock_time_skipping),
        "SleepUntil" => rpc_call!(client, call, sleep_until),
        "Sleep" => rpc_call!(client, call, sleep),
        "UnlockTimeSkippingWithSleep" => rpc_call!(client, call, unlock_time_skipping_with_sleep),
        "UnlockTimeSkipping" => rpc_call!(client, call, unlock_time_skipping),
        rpc => Err(anyhow::anyhow!("Unknown RPC call {}", rpc)),
    }
}

async fn call_health_service(
    client: &CoreClient,
    call: &RpcCallOptions,
) -> anyhow::Result<Vec<u8>> {
    let rpc = call.rpc.to_str();
    let mut client = client.clone();
    match rpc {
        "Check" => rpc_call!(client, call, check),
        rpc => Err(anyhow::anyhow!("Unknown RPC call {}", rpc)),
    }
}

fn rpc_req<P: prost::Message + Default>(
    call: &RpcCallOptions,
) -> anyhow::Result<tonic::Request<P>> {
    let proto = P::decode(call.req.to_slice())?;
    let mut req = tonic::Request::new(proto);
    if call.metadata.size > 0 {
        for (k, v) in call.metadata.to_str_map_on_newlines() {
            req.metadata_mut()
                .insert(MetadataKey::from_str(k)?, v.parse()?);
        }
    }
    if call.timeout_millis > 0 {
        req.set_timeout(Duration::from_millis(call.timeout_millis.into()));
    }
    Ok(req)
}

fn rpc_resp<P>(res: Result<tonic::Response<P>, tonic::Status>) -> anyhow::Result<Vec<u8>>
where
    P: prost::Message,
    P: Default,
{
    Ok(res?.get_ref().encode_to_vec())
}

impl TryFrom<&ClientOptions> for CoreClientOptions {
    type Error = anyhow::Error;

    fn try_from(opts: &ClientOptions) -> anyhow::Result<Self> {
        let mut opts_builder = ClientOptionsBuilder::default();
        opts_builder
            .target_url(Url::parse(opts.target_url.to_str())?)
            .client_name(opts.client_name.to_string())
            .client_version(opts.client_version.to_string())
            .identity(opts.identity.to_string())
            .retry_config(
                unsafe { opts.retry_options.as_ref() }.map_or(RetryConfig::default(), |c| c.into()),
            );
        if let Some(tls_config) = unsafe { opts.tls_options.as_ref() } {
            opts_builder.tls_cfg(tls_config.try_into()?);
        }
        Ok(opts_builder.build()?)
    }
}

impl TryFrom<&ClientTlsOptions> for TlsConfig {
    type Error = anyhow::Error;

    fn try_from(opts: &ClientTlsOptions) -> anyhow::Result<Self> {
        Ok(TlsConfig {
            server_root_ca_cert: opts.server_root_ca_cert.to_option_vec(),
            domain: opts.domain.to_option_string(),
            client_tls_config: match (
                opts.client_cert.to_option_vec(),
                opts.client_private_key.to_option_vec(),
            ) {
                (None, None) => None,
                (Some(client_cert), Some(client_private_key)) => Some(ClientTlsConfig {
                    client_cert,
                    client_private_key,
                }),
                _ => {
                    return Err(anyhow::anyhow!(
                        "Must have both client cert and private key or neither"
                    ));
                }
            },
        })
    }
}

impl From<&ClientRetryOptions> for RetryConfig {
    fn from(opts: &ClientRetryOptions) -> Self {
        RetryConfig {
            initial_interval: Duration::from_millis(opts.initial_interval_millis),
            randomization_factor: opts.randomization_factor,
            multiplier: opts.multiplier,
            max_interval: Duration::from_millis(opts.max_interval_millis),
            max_elapsed_time: if opts.max_elapsed_time_millis == 0 {
                None
            } else {
                Some(Duration::from_millis(opts.max_elapsed_time_millis))
            },
            max_retries: opts.max_retries,
        }
    }
}
