#include "workflow_environment_fixture.h"

#include <temporalio/coro/run_sync.h>
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/temporal_connection.h>
#include <temporalio/testing/workflow_environment.h>

#include <cstdlib>
#include <iostream>
#include <memory>
#include <string>

// MSVC deprecates std::getenv in favor of _dupenv_s. Provide a safe wrapper.
namespace {
std::string safe_getenv(const char* name) {
#ifdef _MSC_VER
    char* buf = nullptr;
    size_t len = 0;
    if (_dupenv_s(&buf, &len, name) == 0 && buf != nullptr) {
        std::string result(buf);
        free(buf);
        return result;
    }
    return {};
#else
    const char* val = std::getenv(name);
    return val ? std::string(val) : std::string{};
#endif
}
}  // namespace

using temporalio::coro::run_task_sync;

namespace temporalio::testing {

// Static member definitions.
std::unique_ptr<WorkflowEnvironment> WorkflowEnvironmentFixture::env_;
std::shared_ptr<client::TemporalClient> WorkflowEnvironmentFixture::client_;
std::atomic<bool> WorkflowEnvironmentFixture::available_{false};

void WorkflowEnvironmentFixture::SetUp() {
    // Check if an external server is specified via environment variables.
    std::string target_host_env = safe_getenv("TEMPORAL_TEST_CLIENT_TARGET_HOST");
    std::string namespace_env = safe_getenv("TEMPORAL_TEST_CLIENT_NAMESPACE");
    std::string dev_server_path_env = safe_getenv("TEMPORAL_DEV_SERVER_PATH");

    try {
        if (!target_host_env.empty()) {
            // Connect to an external server.
            std::string target_host = target_host_env;
            std::string ns = !namespace_env.empty()
                ? namespace_env
                : "default";

            std::cout << "[WorkflowEnvironmentFixture] Connecting to external server: "
                      << target_host << " (namespace: " << ns << ")" << std::endl;

            client::TemporalConnectionOptions conn_opts;
            conn_opts.target_host = target_host;

            auto conn = run_task_sync(
                client::TemporalConnection::connect(conn_opts));

            client::TemporalClientOptions client_opts;
            client_opts.ns = ns;

            client_ = client::TemporalClient::create(conn, client_opts);
            env_ = std::make_unique<WorkflowEnvironment>(client_);
            available_ = true;

            std::cout << "[WorkflowEnvironmentFixture] Connected to external server."
                      << std::endl;
        } else {
            // Start a local dev server (auto-downloads if not present).
            WorkflowEnvironmentStartLocalOptions local_opts;

            // Allow overriding the dev server binary path via env var.
            if (!dev_server_path_env.empty()) {
                local_opts.existing_path = dev_server_path_env;
                std::cout << "[WorkflowEnvironmentFixture] Using dev server at: "
                          << dev_server_path_env << std::endl;
            } else {
                std::cout << "[WorkflowEnvironmentFixture] Starting local dev server "
                          << "(will auto-download if not cached)..." << std::endl;
            }

            local_opts.extra_args = {
                "--dynamic-config-value",
                "system.forceSearchAttributesCacheRefreshOnRead=true",
            };

            env_ = run_task_sync(WorkflowEnvironment::start_local(local_opts));
            client_ = env_->client();
            available_ = true;

            std::cout << "[WorkflowEnvironmentFixture] Local dev server started."
                      << std::endl;
        }
    } catch (const std::exception& e) {
        std::cerr << "[WorkflowEnvironmentFixture] WARNING: Failed to initialize "
                  << "workflow environment: " << e.what() << std::endl;
        std::cerr << "[WorkflowEnvironmentFixture] Set TEMPORAL_TEST_CLIENT_TARGET_HOST "
                  << "to connect to an existing server, or TEMPORAL_DEV_SERVER_PATH to "
                  << "use a local binary. Otherwise, the bridge will auto-download."
                  << std::endl;
        std::cerr << "[WorkflowEnvironmentFixture] Integration tests will be skipped."
                  << std::endl;
        available_ = false;
    }
}

void WorkflowEnvironmentFixture::TearDown() {
    if (env_) {
        try {
            run_task_sync(env_->shutdown());
        } catch (const std::exception& e) {
            std::cerr << "[WorkflowEnvironmentFixture] WARNING: Error during shutdown: "
                      << e.what() << std::endl;
        }
        env_.reset();
    }
    client_.reset();
    available_ = false;
}

WorkflowEnvironment* WorkflowEnvironmentFixture::environment() {
    return env_.get();
}

std::shared_ptr<client::TemporalClient> WorkflowEnvironmentFixture::client() {
    return client_;
}

bool WorkflowEnvironmentFixture::is_available() {
    return available_;
}

// ── WorkflowEnvironmentTestBase ──────────────────────────────────────────

std::atomic<uint64_t> WorkflowEnvironmentTestBase::task_queue_counter_{0};

void WorkflowEnvironmentTestBase::SetUp() {
    if (!WorkflowEnvironmentFixture::is_available()) {
        GTEST_SKIP() << "Workflow environment not available "
                     << "(no local dev server or external server configured)";
    }
}

WorkflowEnvironment* WorkflowEnvironmentTestBase::env() {
    return WorkflowEnvironmentFixture::environment();
}

std::shared_ptr<client::TemporalClient> WorkflowEnvironmentTestBase::client() {
    return WorkflowEnvironmentFixture::client();
}

std::string WorkflowEnvironmentTestBase::unique_task_queue() {
    uint64_t id = task_queue_counter_.fetch_add(1, std::memory_order_relaxed);
    const auto* test_info = ::testing::UnitTest::GetInstance()->current_test_info();
    std::string name = "cpp-test-";
    if (test_info != nullptr) {
        name += test_info->test_suite_name();
        name += "-";
        name += test_info->name();
        name += "-";
    }
    name += std::to_string(id);
    return name;
}

} // namespace temporalio::testing
