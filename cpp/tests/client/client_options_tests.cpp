#include <gtest/gtest.h>

#include <chrono>
#include <memory>
#include <optional>
#include <string>
#include <utility>
#include <vector>

#include "temporalio/client/temporal_client.h"
#include "temporalio/client/temporal_connection.h"
#include "temporalio/client/workflow_handle.h"
#include "temporalio/client/workflow_options.h"
#include "temporalio/common/enums.h"
#include "temporalio/common/retry_policy.h"
#include "temporalio/converters/data_converter.h"

using namespace temporalio::client;
using namespace temporalio::common;
using namespace std::chrono_literals;

// ===========================================================================
// TlsOptions tests
// ===========================================================================

TEST(TlsOptionsTest, DefaultValues) {
    TlsOptions opts;
    EXPECT_TRUE(opts.server_root_ca_cert.empty());
    EXPECT_TRUE(opts.client_cert.empty());
    EXPECT_TRUE(opts.client_private_key.empty());
    EXPECT_FALSE(opts.server_name.has_value());
    EXPECT_FALSE(opts.disabled);
}

TEST(TlsOptionsTest, CustomValues) {
    TlsOptions opts{
        .server_root_ca_cert = "-----BEGIN CERTIFICATE-----\nMIIB...",
        .client_cert = "-----BEGIN CERTIFICATE-----\nMIIC...",
        .client_private_key = "-----BEGIN PRIVATE KEY-----\nMIIE...",
        .server_name = "my-server.example.com",
        .disabled = false,
    };
    EXPECT_FALSE(opts.server_root_ca_cert.empty());
    EXPECT_FALSE(opts.client_cert.empty());
    EXPECT_FALSE(opts.client_private_key.empty());
    EXPECT_EQ(opts.server_name.value(), "my-server.example.com");
}

// ===========================================================================
// KeepAliveOptions tests
// ===========================================================================

TEST(KeepAliveOptionsTest, DefaultValues) {
    KeepAliveOptions opts;
    EXPECT_EQ(opts.interval, std::chrono::seconds{30});
    EXPECT_EQ(opts.timeout, std::chrono::seconds{15});
}

TEST(KeepAliveOptionsTest, CustomValues) {
    KeepAliveOptions opts{
        .interval = 10000ms,
        .timeout = 5000ms,
    };
    EXPECT_EQ(opts.interval, 10000ms);
    EXPECT_EQ(opts.timeout, 5000ms);
}

// ===========================================================================
// HttpConnectProxyOptions tests
// ===========================================================================

TEST(HttpConnectProxyOptionsTest, DefaultValues) {
    HttpConnectProxyOptions opts;
    EXPECT_TRUE(opts.target_host.empty());
    EXPECT_FALSE(opts.basic_auth_user.has_value());
    EXPECT_FALSE(opts.basic_auth_password.has_value());
}

TEST(HttpConnectProxyOptionsTest, CustomValues) {
    HttpConnectProxyOptions opts{
        .target_host = "proxy.example.com:8080",
        .basic_auth_user = "user",
        .basic_auth_password = "pass",
    };
    EXPECT_EQ(opts.target_host, "proxy.example.com:8080");
    EXPECT_EQ(opts.basic_auth_user.value(), "user");
    EXPECT_EQ(opts.basic_auth_password.value(), "pass");
}

// ===========================================================================
// RpcRetryOptions tests
// ===========================================================================

TEST(RpcRetryOptionsTest, DefaultValues) {
    RpcRetryOptions opts;
    EXPECT_EQ(opts.initial_interval, 100ms);
    EXPECT_DOUBLE_EQ(opts.randomization_factor, 0.2);
    EXPECT_DOUBLE_EQ(opts.multiplier, 1.5);
    EXPECT_EQ(opts.max_interval, 5000ms);
    EXPECT_EQ(opts.max_elapsed_time, 10000ms);
    EXPECT_EQ(opts.max_retries, 10);
}

TEST(RpcRetryOptionsTest, CustomValues) {
    RpcRetryOptions opts{
        .initial_interval = 200ms,
        .randomization_factor = 0.5,
        .multiplier = 2.0,
        .max_interval = 10000ms,
        .max_elapsed_time = 60000ms,
        .max_retries = 5,
    };
    EXPECT_EQ(opts.initial_interval, 200ms);
    EXPECT_DOUBLE_EQ(opts.randomization_factor, 0.5);
    EXPECT_DOUBLE_EQ(opts.multiplier, 2.0);
    EXPECT_EQ(opts.max_interval, 10000ms);
    EXPECT_EQ(opts.max_elapsed_time, 60000ms);
    EXPECT_EQ(opts.max_retries, 5);
}

// ===========================================================================
// TemporalConnectionOptions tests
// ===========================================================================

TEST(TemporalConnectionOptionsTest, DefaultValues) {
    TemporalConnectionOptions opts;
    EXPECT_TRUE(opts.target_host.empty());
    EXPECT_FALSE(opts.tls.has_value());
    EXPECT_FALSE(opts.rpc_retry.has_value());
    EXPECT_TRUE(opts.keep_alive.has_value());  // Default enabled
    EXPECT_FALSE(opts.http_connect_proxy.has_value());
    EXPECT_TRUE(opts.rpc_metadata.empty());
    EXPECT_FALSE(opts.api_key.has_value());
    EXPECT_FALSE(opts.identity.has_value());
    EXPECT_EQ(opts.runtime, nullptr);
}

TEST(TemporalConnectionOptionsTest, CustomValues) {
    TemporalConnectionOptions opts{
        .target_host = "localhost:7233",
        .tls = TlsOptions{},
        .rpc_retry = RpcRetryOptions{.max_retries = 3},
        .keep_alive = KeepAliveOptions{.interval = 20000ms},
        .rpc_metadata = {{"key", "value"}},
        .api_key = "my-api-key",
        .identity = "worker-1",
    };
    EXPECT_EQ(opts.target_host, "localhost:7233");
    EXPECT_TRUE(opts.tls.has_value());
    EXPECT_TRUE(opts.rpc_retry.has_value());
    EXPECT_EQ(opts.rpc_retry->max_retries, 3);
    EXPECT_EQ(opts.keep_alive->interval, 20000ms);
    ASSERT_EQ(opts.rpc_metadata.size(), 1u);
    EXPECT_EQ(opts.rpc_metadata[0].first, "key");
    EXPECT_EQ(opts.rpc_metadata[0].second, "value");
    EXPECT_EQ(opts.api_key.value(), "my-api-key");
    EXPECT_EQ(opts.identity.value(), "worker-1");
}

TEST(TemporalConnectionOptionsTest, DisabledKeepAlive) {
    TemporalConnectionOptions opts{
        .keep_alive = std::nullopt,
    };
    EXPECT_FALSE(opts.keep_alive.has_value());
}

// ===========================================================================
// TemporalClientOptions tests
// ===========================================================================

TEST(TemporalClientOptionsTest, DefaultValues) {
    TemporalClientOptions opts;
    EXPECT_EQ(opts.ns, "default");
    EXPECT_TRUE(opts.interceptors.empty());
    EXPECT_FALSE(opts.data_converter.has_value());
}

TEST(TemporalClientOptionsTest, CustomDataConverter) {
    TemporalClientOptions opts;
    opts.data_converter = temporalio::converters::DataConverter::default_instance();
    EXPECT_TRUE(opts.data_converter.has_value());
    EXPECT_NE(opts.data_converter->payload_converter, nullptr);
    EXPECT_NE(opts.data_converter->failure_converter, nullptr);
}

TEST(TemporalClientOptionsTest, CustomNamespace) {
    TemporalClientOptions opts{.ns = "production"};
    EXPECT_EQ(opts.ns, "production");
}

// ===========================================================================
// TemporalClientConnectOptions tests
// ===========================================================================

TEST(TemporalClientConnectOptionsTest, DefaultValues) {
    TemporalClientConnectOptions opts;
    EXPECT_TRUE(opts.connection.target_host.empty());
    EXPECT_EQ(opts.client.ns, "default");
}

TEST(TemporalClientConnectOptionsTest, CustomValues) {
    TemporalClientConnectOptions opts{
        .connection = {.target_host = "localhost:7233"},
        .client = {.ns = "staging"},
    };
    EXPECT_EQ(opts.connection.target_host, "localhost:7233");
    EXPECT_EQ(opts.client.ns, "staging");
}

// ===========================================================================
// WorkflowExecution tests
// ===========================================================================

TEST(WorkflowExecutionTest, DefaultValues) {
    WorkflowExecution exec;
    EXPECT_TRUE(exec.workflow_id.empty());
    EXPECT_TRUE(exec.run_id.empty());
    EXPECT_FALSE(exec.workflow_type.has_value());
}

TEST(WorkflowExecutionTest, CustomValues) {
    WorkflowExecution exec{
        .workflow_id = "wf-123",
        .run_id = "run-456",
        .workflow_type = "MyWorkflow",
    };
    EXPECT_EQ(exec.workflow_id, "wf-123");
    EXPECT_EQ(exec.run_id, "run-456");
    EXPECT_EQ(exec.workflow_type.value(), "MyWorkflow");
}

// ===========================================================================
// WorkflowExecutionCount tests
// ===========================================================================

TEST(WorkflowExecutionCountTest, DefaultValue) {
    WorkflowExecutionCount count;
    EXPECT_EQ(count.count, 0);
}

TEST(WorkflowExecutionCountTest, CustomValue) {
    WorkflowExecutionCount count{.count = 42};
    EXPECT_EQ(count.count, 42);
}

// ===========================================================================
// WorkflowOptions tests
// ===========================================================================

TEST(WorkflowOptionsTest, DefaultValues) {
    WorkflowOptions opts;
    EXPECT_TRUE(opts.id.empty());
    EXPECT_TRUE(opts.task_queue.empty());
    EXPECT_FALSE(opts.execution_timeout.has_value());
    EXPECT_FALSE(opts.run_timeout.has_value());
    EXPECT_FALSE(opts.task_timeout.has_value());
    EXPECT_EQ(opts.id_reuse_policy, WorkflowIdReusePolicy::kUnspecified);
    EXPECT_EQ(opts.id_conflict_policy,
              WorkflowIdConflictPolicy::kUnspecified);
    EXPECT_FALSE(opts.retry_policy.has_value());
    EXPECT_FALSE(opts.cron_schedule.has_value());
    EXPECT_TRUE(opts.memo.empty());
    EXPECT_FALSE(opts.search_attributes.has_value());
    EXPECT_FALSE(opts.start_delay.has_value());
    EXPECT_FALSE(opts.request_id.has_value());
    EXPECT_FALSE(opts.priority.has_value());
}

TEST(WorkflowOptionsTest, CustomValues) {
    WorkflowOptions opts{
        .id = "my-workflow-1",
        .task_queue = "my-queue",
        .execution_timeout = 60000ms,
        .run_timeout = 30000ms,
        .task_timeout = 10000ms,
        .id_reuse_policy = WorkflowIdReusePolicy::kAllowDuplicate,
        .id_conflict_policy = WorkflowIdConflictPolicy::kFail,
        .retry_policy = RetryPolicy{.maximum_attempts = 3},
        .cron_schedule = "0 * * * *",
        .start_delay = 5000ms,
        .request_id = "req-abc",
    };
    EXPECT_EQ(opts.id, "my-workflow-1");
    EXPECT_EQ(opts.task_queue, "my-queue");
    EXPECT_EQ(opts.execution_timeout.value(), 60000ms);
    EXPECT_EQ(opts.run_timeout.value(), 30000ms);
    EXPECT_EQ(opts.task_timeout.value(), 10000ms);
    EXPECT_EQ(opts.id_reuse_policy, WorkflowIdReusePolicy::kAllowDuplicate);
    EXPECT_EQ(opts.id_conflict_policy, WorkflowIdConflictPolicy::kFail);
    EXPECT_TRUE(opts.retry_policy.has_value());
    EXPECT_EQ(opts.retry_policy->maximum_attempts, 3);
    EXPECT_EQ(opts.cron_schedule.value(), "0 * * * *");
    EXPECT_EQ(opts.start_delay.value(), 5000ms);
    EXPECT_EQ(opts.request_id.value(), "req-abc");
}

// ===========================================================================
// WorkflowSignalOptions tests
// ===========================================================================

TEST(WorkflowSignalOptionsTest, DefaultValues) {
    WorkflowSignalOptions opts;
    EXPECT_FALSE(opts.request_id.has_value());
}

TEST(WorkflowSignalOptionsTest, CustomValues) {
    WorkflowSignalOptions opts{.request_id = "sig-req-1"};
    EXPECT_EQ(opts.request_id.value(), "sig-req-1");
}

// ===========================================================================
// WorkflowQueryOptions tests
// ===========================================================================

TEST(WorkflowQueryOptionsTest, DefaultValues) {
    WorkflowQueryOptions opts;
    EXPECT_FALSE(opts.reject_condition.has_value());
}

TEST(WorkflowQueryOptionsTest, CustomValues) {
    WorkflowQueryOptions opts{.reject_condition = 1};
    EXPECT_EQ(opts.reject_condition.value(), 1);
}

// ===========================================================================
// WorkflowTerminateOptions tests
// ===========================================================================

TEST(WorkflowTerminateOptionsTest, DefaultValues) {
    WorkflowTerminateOptions opts;
    EXPECT_FALSE(opts.reason.has_value());
}

TEST(WorkflowTerminateOptionsTest, WithReason) {
    WorkflowTerminateOptions opts{.reason = "test termination"};
    EXPECT_EQ(opts.reason.value(), "test termination");
}

// ===========================================================================
// WorkflowCountOptions tests
// ===========================================================================

TEST(WorkflowCountOptionsTest, DefaultValues) {
    WorkflowCountOptions opts;
    EXPECT_FALSE(opts.query.has_value());
}

TEST(WorkflowCountOptionsTest, CustomValues) {
    WorkflowCountOptions opts{.query = "WorkflowType='MyWorkflow'"};
    EXPECT_EQ(opts.query.value(), "WorkflowType='MyWorkflow'");
}

// ===========================================================================
// WorkflowListOptions tests
// ===========================================================================

TEST(WorkflowListOptionsTest, DefaultValues) {
    WorkflowListOptions opts;
    EXPECT_FALSE(opts.query.has_value());
    EXPECT_FALSE(opts.page_size.has_value());
}

TEST(WorkflowListOptionsTest, CustomValues) {
    WorkflowListOptions opts{
        .query = "ExecutionStatus='Running'",
        .page_size = 50,
    };
    EXPECT_EQ(opts.query.value(), "ExecutionStatus='Running'");
    EXPECT_EQ(opts.page_size.value(), 50);
}

// ===========================================================================
// TemporalClient type traits
// ===========================================================================

TEST(TemporalClientTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<TemporalClient>);
    EXPECT_FALSE(std::is_copy_assignable_v<TemporalClient>);
}

// ===========================================================================
// TemporalConnection type traits
// ===========================================================================

TEST(TemporalConnectionTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<TemporalConnection>);
    EXPECT_FALSE(std::is_copy_assignable_v<TemporalConnection>);
}
