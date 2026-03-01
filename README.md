# Temporal C++ SDK

[![MIT](https://img.shields.io/github/license/temporalio/sdk-dotnet.svg?style=for-the-badge)](LICENSE)

[Temporal](https://temporal.io/) is a distributed, scalable, durable, and highly available orchestration engine used to
execute asynchronous, long-running business logic in a scalable and resilient way.

**Temporal C++ SDK** is a C++20 client library for authoring and running Temporal workflows, activities, and Nexus
operations. It wraps the shared Rust `sdk-core` engine via a C FFI bridge, providing fully asynchronous coroutine-based
APIs.

> **Status: Functionally Complete** -- The SDK builds on MSVC 2022 (Windows) with **715/715 unit tests passing**.
> All 3 self-contained examples run end-to-end against a live Temporal server with clean shutdown (exit code 0).
> Workflows execute activities, handle signals/queries/updates, use timers, and return results correctly.
> The public API uses typed arguments via variadic templates and DataConverter -- no raw JSON strings.

## Features

- **C++20 coroutines** -- `co_await` / `co_return` for all async operations (workflows, client calls, timers)
- **Lazy `Task<T>`** with symmetric transfer for efficient coroutine chaining
- **Deterministic `CoroutineScheduler`** for workflow replay (single-threaded FIFO, matching the Temporal execution model)
- **Full Temporal API coverage** -- workflows, activities (with `execute_activity()`), signals, queries, updates, child workflows, Nexus operations
- **Type-safe workflow/activity signatures** via variadic template deduction -- no `std::any` in user code
- **DataConverter integration** -- transparent serialization for all client and worker APIs
- **Interceptor chains** for both client-side and worker-side operations
- **Protobuf integration** -- 74 Temporal API proto files auto-generated at build time
- **OpenTelemetry extension** -- tracing interceptor for workflow/activity spans
- **Diagnostics extension** -- metrics adapter for counters, histograms, and gauges

## Prerequisites

- **CMake** 3.20+
- **C++20 compiler**: MSVC 2022+ (Windows), GCC 11+ or Clang 14+ (Linux)
- **Rust toolchain** (`cargo`) -- for building the `sdk-core` C bridge library
- **Git** -- clone recursively to fetch the `sdk-core` submodule

## Quick Start

### Clone

```bash
git clone --recursive https://github.com/temporalio/sdk-dotnet.git temporal-sdk-cpp
cd temporal-sdk-cpp
```

### Build

Dependencies (abseil, protobuf, nlohmann/json, Google Test) are fetched automatically via CMake FetchContent.

**Fast build with Ninja (recommended):**

```bash
# Windows (uses C:\ninja\ninja.exe automatically)
cmake --preset windows-debug
cmake --build --preset windows-debug

# Linux
cmake --preset linux-debug
cmake --build --preset linux-debug

# Or manually specify Ninja
cmake -B cpp/build -S cpp -G "Ninja Multi-Config" -DCMAKE_MAKE_PROGRAM=C:/ninja/ninja.exe
cmake --build cpp/build --config Debug

# If Ninja is on PATH, no -DCMAKE_MAKE_PROGRAM needed
cmake -B cpp/build -S cpp -G "Ninja Multi-Config"
cmake --build cpp/build --config Debug
```

**Standard build (without Ninja):**

```bash
# Configure
cmake -B cpp/build -S cpp

# Build the library
cmake --build cpp/build --target temporalio

# Build and run tests
cmake --build cpp/build --target temporalio_tests
ctest --test-dir cpp/build --output-on-failure
```

To skip optional components:

```bash
cmake -B cpp/build -S cpp \
  -DTEMPORALIO_BUILD_EXTENSIONS=OFF \
  -DTEMPORALIO_BUILD_EXAMPLES=OFF \
  -DTEMPORALIO_BUILD_TESTS=OFF
```

User-local overrides (e.g., different Ninja path, different compiler) can be placed in
`cpp/CMakeUserPresets.json`, which is `.gitignore`'d.

### Usage Example

```cpp
#include <temporalio/client/temporal_client.h>
#include <temporalio/client/temporal_connection.h>
#include <temporalio/worker/temporal_worker.h>
#include <temporalio/workflows/workflow.h>
#include <temporalio/workflows/workflow_definition.h>

using namespace temporalio;

// Activity: takes a name, returns a greeting
coro::Task<std::string> greet(std::string name) {
    co_return "Hello, " + name + "!";
}

// Workflow: calls the greet activity and returns its result
class GreetingWorkflow {
public:
    coro::Task<std::string> run(std::string name) {
        workflows::ActivityOptions opts;
        opts.start_to_close_timeout = std::chrono::seconds(30);
        co_return co_await workflows::Workflow::execute_activity<std::string>(
            "greet", name, opts);
    }
};

int main() {
    // Connect to Temporal
    auto tc = coro::run_task_sync(client::TemporalClient::connect(
        {.connection = {.target_host = "localhost:7233"}}));

    // Register activity and workflow
    auto activity = activities::ActivityDefinition::create("greet", &greet);
    auto workflow = workflows::WorkflowDefinition::create<GreetingWorkflow>(
        "GreetingWorkflow").run(&GreetingWorkflow::run).build();

    // Start worker on a background thread
    worker::TemporalWorkerOptions opts;
    opts.task_queue = "greeting-queue";
    opts.activities.push_back(activity);
    opts.workflows.push_back(workflow);
    worker::TemporalWorker w(tc, opts);

    std::stop_source stop;
    std::jthread worker_thread([&w, token = stop.get_token()]() {
        coro::run_task_sync(w.execute_async(token));
    });

    // Start workflow and get result (type-safe, no raw JSON)
    client::WorkflowOptions wf_opts;
    wf_opts.id = "my-workflow";
    wf_opts.task_queue = "greeting-queue";
    auto handle = coro::run_task_sync(tc->start_workflow(
        "GreetingWorkflow", wf_opts, std::string("World")));
    auto result = coro::run_task_sync(handle.get_result<std::string>());
    std::cout << "Result: " << result << "\n";

    // Clean shutdown on main thread
    stop.request_stop();
    worker_thread.join();
}
```

## Project Structure

```
cpp/
  CMakeLists.txt                    # Top-level CMake build
  CMakePresets.json                 # Ninja Multi-Config presets (windows/linux)
  vcpkg.json                       # Dependency manifest
  cmake/
    Platform.cmake                 # Platform detection, Rust cargo build
    CompilerWarnings.cmake         # Warning flags
    ProtobufGenerate.cmake         # Proto code generation
    temporalioConfig.cmake.in      # CMake config template for find_package()

  include/temporalio/              # Public headers (35+ files)
    export.h                       # TEMPORALIO_EXPORT macro (GenerateExportHeader)
    coro/                          # Task<T>, CancellationToken, CoroutineScheduler, TaskCompletionSource
    client/                        # TemporalClient, TemporalConnection, WorkflowHandle
      interceptors/                # IClientInterceptor, ClientOutboundInterceptor
    common/                        # RetryPolicy, SearchAttributes, MetricMeter, enums
    converters/                    # DataConverter, IPayloadConverter, IFailureConverter
    exceptions/                    # 20+ exception types (TemporalException hierarchy)
    nexus/                         # NexusServiceDefinition, OperationHandler
    runtime/                       # TemporalRuntime, telemetry config
    testing/                       # WorkflowEnvironment, ActivityEnvironment
    worker/                        # TemporalWorker, WorkflowInstance
      interceptors/                # IWorkerInterceptor, inbound/outbound interceptors
      internal/                    # ActivityWorker, WorkflowWorker, NexusWorker
    workflows/                     # Workflow ambient API, WorkflowDefinition builder, ActivityOptions

  src/temporalio/                  # Private implementation (27 .cpp + 8 .h)
    bridge/                        # Rust FFI wrappers (SafeHandle, CallScope, interop)

  extensions/
    opentelemetry/                 # TracingInterceptor
    diagnostics/                   # CustomMetricMeter

  tests/                           # Google Test suite (37 files, 715 tests)
  examples/                        # 6 examples (see Examples section below)

vcpkg-port/                        # vcpkg port files for registry submission
vcpkg-overlay/                     # Local development overlay port
test-consumer/                     # Consumer integration test (find_package)
```

## Examples

Six examples in `cpp/examples/` demonstrate key Temporal patterns. All require a running Temporal server (`temporal server start-dev`).

| Example | What it demonstrates |
|---------|---------------------|
| `hello_world` | Client-only: connect and start a workflow |
| `signal_workflow` | Client-only: send signals and query workflow state |
| `activity_worker` | Worker-only: define and register activities |
| `workflow_activity` | **E2E**: workflow calls `execute_activity()`, returns result, clean shutdown |
| `timer_workflow` | **E2E**: deterministic timers, conditions, signals, queries |
| `update_workflow` | **E2E**: update handlers with validators, queries, signals |

```bash
# Build all examples
cmake --build cpp/build --config Debug

# Run an example (requires: temporal server start-dev)
./cpp/build/examples/Debug/example_workflow_activity
```

## Architecture

```
User Code (C++20)
       |
Public API Layer (Client, Workflows, Activities, Worker, Testing, Converters)
       |
Bridge Layer (SafeHandle + CallScope + FFI wrappers)
       |
Native Library (temporal_sdk_core_c_bridge -- Rust, compiled via cargo)
```

### Key Design Decisions

| Concept | C++ Approach |
|---------|-------------|
| Async/await | C++20 coroutines (`co_await` / `co_return`) with lazy `Task<T>` |
| Deterministic execution | `CoroutineScheduler` (FIFO deque, single-threaded) |
| Cancellation | `std::stop_token` / `std::stop_source` |
| Context propagation | `thread_local` pointers with RAII scopes |
| FFI memory management | `CallScope` (scoped allocations with `std::deque` for stable references) |
| Rust handle ownership | `SafeHandle<T>` RAII template with custom deleters |
| Callback-to-coroutine bridge | `TaskCompletionSource<T>` |
| Type-erased payloads | `std::any` |
| Shared ownership | `std::shared_ptr` (runtime, connection, client) |
| Pimpl pattern | `std::unique_ptr<Impl>` for ABI stability |

## CMake Options

| Option | Default | Description |
|--------|---------|-------------|
| `TEMPORALIO_BUILD_EXTENSIONS` | `ON` | Build OpenTelemetry and Diagnostics extensions |
| `TEMPORALIO_BUILD_TESTS` | `ON` | Build the Google Test suite |
| `TEMPORALIO_BUILD_EXAMPLES` | `ON` | Build example programs |
| `TEMPORALIO_BUILD_PROTOS` | `ON` | Generate C++ types from Temporal API `.proto` files |
| `BUILD_SHARED_LIBS` | `OFF` | Build as shared library instead of static |

## Dependencies

| Library | Purpose | Source |
|---------|---------|--------|
| **Protobuf** v29.3 | Temporal API types, bridge serialization | FetchContent or vcpkg |
| **abseil-cpp** | Required by Protobuf | FetchContent or vcpkg |
| **nlohmann/json** | JSON payload conversion | FetchContent or vcpkg |
| **Google Test** | Unit testing framework | FetchContent or vcpkg |
| **OpenTelemetry C++** | Tracing extension (optional) | vcpkg |
| **Rust toolchain** | Building the `sdk-core` bridge library | Pre-installed |

All dependencies except Rust are fetched automatically via CMake FetchContent if not found on the system.

## Testing

```bash
# Build and run all tests
cmake --build cpp/build --target temporalio_tests
ctest --test-dir cpp/build --output-on-failure

# Run tests with verbose output
ctest --test-dir cpp/build --output-on-failure --verbose
```

715 unit tests (all passing) cover:
- Async primitives (Task, CancellationToken, CoroutineScheduler, TaskCompletionSource)
- Bridge layer (CallScope, SafeHandle)
- Client (connection options, interceptors, workflow handle)
- Common types (enums, metrics, retry policy, search attributes)
- Converters (data converter, failure converter)
- Exceptions (full hierarchy)
- Nexus (operation handlers)
- Runtime (lifecycle)
- Testing utilities (activity environment, workflow environment)
- Worker (options, interceptors, workflow instance, replayer)
- Workflows (ambient API, definitions, info types)

Set environment variables to test against an external Temporal server:

```bash
export TEMPORAL_TEST_CLIENT_TARGET_HOST="localhost:7233"
export TEMPORAL_TEST_CLIENT_NAMESPACE="default"
```

## vcpkg Packaging

The SDK supports installation via vcpkg with `find_package()` integration.

### Install from local overlay

```bash
vcpkg install temporalio --overlay-ports=./vcpkg-overlay
```

### Use in your CMake project

```cmake
find_package(temporalio CONFIG REQUIRED)
target_link_libraries(my_app PRIVATE temporalio::temporalio)
```

### Shared library support

Build as a shared library (DLL/SO) with proper symbol export via `TEMPORALIO_EXPORT`:

```bash
cmake -B cpp/build -S cpp -DBUILD_SHARED_LIBS=ON
```

## Extensions

### OpenTelemetry Tracing

```cpp
#include <temporalio/extensions/opentelemetry/tracing_interceptor.h>

// Add tracing to client and worker
auto tracing = std::make_shared<extensions::opentelemetry::TracingInterceptor>();
client_opts.interceptors.push_back(tracing);
worker_opts.interceptors.push_back(tracing);
```

### Diagnostics Metrics

```cpp
#include <temporalio/extensions/diagnostics/custom_metric_meter.h>

// Adapt Temporal metrics to OpenTelemetry Metrics API
auto meter = std::make_shared<extensions::diagnostics::CustomMetricMeter>(otel_meter);
runtime_opts.metrics.custom_meter = meter;
```

## Syncing Upstream C# Changes

This SDK is a fork of the [Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet).
The `main` branch tracks upstream C# releases; the `cpp-conversion` branch contains the C++ port.

A Claude Code slash command automates porting new upstream C# changes to C++:

### Setup

Ensure the upstream remote is configured:

```bash
git remote add upstream https://github.com/temporalio/sdk-dotnet.git
```

### Usage

From the `cpp-conversion` branch with a clean working tree:

```bash
# Preview what upstream changes exist (no modifications)
/update-cpp --dry-run

# Port all new upstream changes to C++
/update-cpp
```

The command will:
1. Fetch latest upstream C# changes
2. Identify new/modified/deleted files since the last sync
3. Categorize changes and present a summary for approval
4. Merge upstream into your local branches
5. Port each C# change to the corresponding C++ code
6. Build and run the test suite
7. Update `.claude/sync-state.json` with the new sync point

### Sync State

The file `.claude/sync-state.json` tracks the last upstream commit that was synced.
On the first run, it uses the initial port baseline (`589c7c3`) as the starting point.
Each sync appends to the history, recording what was ported and what was skipped.

## Ported From

This SDK is a C++20 port of the [Temporal .NET SDK](https://github.com/temporalio/sdk-dotnet). The original C# SDK
(~469 source files, ~412 tests) was systematically converted to idiomatic C++20, replacing:

- C# `async`/`await` with C++20 coroutines
- .NET `Task<T>` with a custom lazy coroutine `Task<T>`
- P/Invoke with direct C FFI calls
- `CancellationToken` with `std::stop_token`
- Reflection-based registration with template-based builders
- xUnit with Google Test

## License

MIT License - see [LICENSE](LICENSE) for details.
