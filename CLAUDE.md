# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

A C++20 port of the Temporal SDK — a client library for authoring and running [Temporal](https://temporal.io/) workflows (durable execution) in C++. It wraps the shared Rust `sdk-core` engine via a C FFI bridge. The original C# SDK source lives alongside for reference.

## C++ Build & Development Commands

**Prerequisites:** C++20 compiler (MSVC 2022 or GCC 13+), CMake 3.20+, Ninja, Rust (`cargo`), Protobuf Compiler (`protoc`). Clone recursively (git submodule in `src/Temporalio/Bridge/sdk-core/`).

### Windows (MSVC + MSBuild)

```bash
cmake -B cpp/build -S cpp                                     # Configure (MSBuild generator)
cmake --build cpp/build --config Debug                        # Build all (Debug)
cmake --build cpp/build --config Debug --target temporalio_tests  # Build tests only
ctest --test-dir cpp/build --output-on-failure -C Debug       # Run all tests
```

### Windows (MSVC + Ninja) — used in CI

```bash
# Run from a "Developer Command Prompt for VS 2022" or after calling vcvarsall.bat
cmake -B cpp/build -S cpp -G Ninja -DCMAKE_BUILD_TYPE=Debug
cmake --build cpp/build
ctest --test-dir cpp/build --output-on-failure --timeout 120
```

### Linux (GCC 13)

```bash
CC=gcc-13 CXX=g++-13 cmake -B cpp/build -S cpp -G Ninja -DCMAKE_BUILD_TYPE=Debug
cmake --build cpp/build
ctest --test-dir cpp/build --output-on-failure --timeout 120
```

### Linux (Clang 18 + ASan/UBSan)

```bash
CC=clang-18 CXX=clang++-18 cmake -B cpp/build -S cpp -G Ninja \
    -DCMAKE_BUILD_TYPE=Debug \
    -DCMAKE_CXX_FLAGS="-fsanitize=address,undefined -fno-omit-frame-pointer" \
    -DCMAKE_EXE_LINKER_FLAGS="-fsanitize=address,undefined"
cmake --build cpp/build
ASAN_OPTIONS=detect_leaks=1:halt_on_error=1 UBSAN_OPTIONS=print_stacktrace=1:halt_on_error=1 \
    ctest --test-dir cpp/build --output-on-failure --timeout 180
```

### Common CMake Options

```bash
-DTEMPORALIO_BUILD_TESTS=ON       # Build test executable (default: ON)
-DTEMPORALIO_BUILD_EXAMPLES=ON    # Build example executables (default: ON)
-DTEMPORALIO_BUILD_EXTENSIONS=OFF # Skip OpenTelemetry/DiagnosticSource extensions
-DTEMPORALIO_BUILD_PROTOS=ON      # Generate protobuf types (default: ON)
```

### Run a single test

```bash
# By test name pattern (GTest filter)
ctest --test-dir cpp/build -R "WorkflowInstanceTest" --output-on-failure
# Or run the test binary directly with GTest filter
cpp/build/tests/Debug/temporalio_tests --gtest_filter="*WorkflowInstanceTest*"
```

### Run examples

Examples require a running Temporal server at `localhost:7233` (e.g., via Docker).

```bash
# Windows
cpp/build/examples/Debug/example_hello_world.exe
# Linux
cpp/build/examples/example_hello_world
```

## Debugging

### Windows (Visual Studio / MSVC)

- Open `cpp/build/temporalio.sln` in Visual Studio 2022
- Set `temporalio_tests` as startup project for test debugging
- Set any `example_*` project as startup project for example debugging
- Debug builds include `/Z7` embedded debug info (Ninja) or PDB files (MSBuild)
- The Rust bridge DLL (`temporalio_sdk_core_c_bridge.dll`) is auto-copied to the output directory
- Use Visual Studio's built-in debugger; breakpoints work across C++ code but not into the Rust bridge

### Linux (GDB)

```bash
# Debug tests with GDB
gdb --args cpp/build/tests/temporalio_tests --gtest_filter="*YourTestName*"

# Debug an example with GDB
LD_LIBRARY_PATH=cpp/build:cpp/build/rust-target/debug gdb cpp/build/examples/example_hello_world

# Common GDB commands for coroutine debugging
(gdb) catch throw                    # Break on C++ exceptions
(gdb) break workflow_instance.cpp:123  # Break at specific line
(gdb) info threads                   # Show all threads (worker thread vs main)
(gdb) thread 2                       # Switch to worker thread
(gdb) bt                             # Backtrace (note: coroutine frames may be opaque)
```

### Linux via Docker (for testing on Windows host)

Temporal runs in Docker locally. To build and test the C++ SDK in a Linux container connected to the same Temporal server:

**1. Create a `.dockerignore`** to exclude build artifacts (critical — without it, context transfer can be 40+ GB):
```
cpp/build/
cpp/build-*/
**/bin/
**/obj/
**/Debug/
**/Release/
src/Temporalio/Bridge/sdk-core/target/
**/Testing/
.vs/
.git/
*.zip
.claude/
```

**2. Build a Linux container** with GCC 13, Rust, protoc, and the full C++ SDK:
```dockerfile
FROM ubuntu:24.04
RUN apt-get update && apt-get install -y gcc-13 g++-13 ninja-build cmake curl unzip pkg-config
RUN curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
ENV PATH="/root/.cargo/bin:${PATH}"
RUN curl -LO https://github.com/protocolbuffers/protobuf/releases/download/v23.4/protoc-23.4-linux-x86_64.zip \
    && unzip protoc-23.4-linux-x86_64.zip -d /usr/local && rm protoc-23.4-linux-x86_64.zip
WORKDIR /src
COPY . .
ENV CC=gcc-13 CXX=g++-13
RUN cmake -B cpp/build -S cpp -G Ninja -DCMAKE_BUILD_TYPE=Debug -DTEMPORALIO_BUILD_EXTENSIONS=OFF
RUN cmake --build cpp/build
ENV LD_LIBRARY_PATH=/src/cpp/build:/src/cpp/build/rust-target/debug
```

**3. Build and run:**
```bash
docker build -f Dockerfile.linux -t temporal-cpp-linux .
# Run tests
docker run --rm temporal-cpp-linux ctest --test-dir cpp/build --output-on-failure --timeout 120
# Run examples (connect to Temporal on Docker network)
docker run --rm --network temporal-network temporal-cpp-linux cpp/build/examples/example_hello_world
```

**4. Interactive debugging with GDB in Docker:**
```bash
# Install GDB in the container and run interactively
docker run --rm -it --network temporal-network temporal-cpp-linux bash
apt-get update && apt-get install -y gdb
gdb --args cpp/build/tests/temporalio_tests --gtest_filter="*YourTest*"
```

**Note:** Examples hardcode `localhost:7233` as the Temporal host. When running in Docker containers on the `temporal-network`, patch the examples to use `temporal:7233` (the Docker service name) before building:
```bash
find cpp/examples -name '*.cpp' -exec sed -i 's/localhost:7233/temporal:7233/g' {} +
```

### AddressSanitizer (ASan) on Linux

ASan catches memory errors (use-after-free, buffer overflow, heap corruption). Run in a Docker container for consistent results:

```bash
# Build with ASan
CC=clang-18 CXX=clang++-18 cmake -B cpp/build -S cpp -G Ninja \
    -DCMAKE_BUILD_TYPE=Debug \
    -DCMAKE_CXX_FLAGS="-fsanitize=address,undefined -fno-omit-frame-pointer" \
    -DCMAKE_EXE_LINKER_FLAGS="-fsanitize=address,undefined"
cmake --build cpp/build

# Run with ASan options
ASAN_OPTIONS=detect_leaks=1:halt_on_error=1 \
UBSAN_OPTIONS=print_stacktrace=1:halt_on_error=1 \
    cpp/build/tests/temporalio_tests
```

### Known Platform Differences

- **GCC 13 required on Linux** — GCC 12 has coroutine codegen bugs causing heap corruption
- **GCC 13 symmetric transfer bug** — `FinalAwaiter` uses `void await_suspend()` (direct resume) instead of returning `coroutine_handle<>`, which is broken in GCC 13 for certain frame layouts
- **GCC 13 lambda coroutine bug** — lambdas with reference captures and no internal `co_await` produce broken resume code; workaround: use free-function coroutines in tests
- **Linux shutdown** — always call `TemporalRuntime::reset_default()` before `main()` returns to avoid races between static destruction and glibc TLS cleanup

## C++ Project Structure

```
cpp/
├── include/temporalio/     # Public headers
├── src/temporalio/         # Implementation
│   └── bridge/             # Rust FFI bridge wrappers
├── tests/                  # Google Test (36 test files, 742+ tests)
├── examples/               # 6 example programs
│   ├── hello_world/
│   ├── workflow_activity/
│   ├── activity_worker/
│   ├── signal_workflow/
│   ├── timer_workflow/
│   └── update_workflow/
├── extensions/
│   ├── opentelemetry/      # OTel tracing interceptor
│   └── diagnostics/        # Metrics adapter
└── cmake/                  # CMake modules (Platform, CompilerWarnings, ProtobufGenerate)
```

**Rust bridge:** `src/Temporalio/Bridge/sdk-core/` (git submodule) — builds `temporalio_sdk_core_c_bridge` cdylib.

## Architecture

```
User Code (C++20)
     │
Public API Layer (Client, Workflows, Activities, Worker, Testing, Converters)
     │
Bridge Layer (temporalio::bridge — SafeHandle wrappers, C FFI)
     │
Native Library (temporalio_sdk_core_c_bridge — Rust compiled per-platform)
```

### Key Namespaces in `cpp/include/temporalio/`

- **`client/`** — `TemporalClient`, `TemporalConnection`, `WorkflowHandle`, `WorkflowOptions`, interceptor chain
- **`workflows/`** — `Workflow` static class (ambient API), `WorkflowDefinition` builder
- **`activities/`** — `ActivityDefinition`, `ActivityContext`
- **`worker/`** — `TemporalWorker`, `TemporalWorkerOptions`, internal dispatchers
- **`bridge/`** — Rust interop: `Runtime`, `Client`, `Worker` (SafeHandle wrappers), `CallScope`
- **`converters/`** — `DataConverter`, `PayloadConverter`, `JsonPlainConverter`
- **`coro/`** — `Task<T>`, `run_task_sync()`, `CoroutineScheduler`, `CancellationToken`, `TaskCompletionSource`
- **`runtime/`** — `TemporalRuntime`, telemetry configuration
- **`testing/`** — `WorkflowEnvironment` (local dev server + time-skipping)
- **`nexus/`** — Nexus RPC operation handlers

### Core Abstractions Flow

1. **`TemporalRuntime`** → holds Rust thread pool + telemetry config
2. **`TemporalConnection`** → gRPC connection to Temporal server (thread-safe, reusable)
3. **`TemporalClient`** → workflow CRUD, schedule management, built on a connection
4. **`TemporalWorker`** → polls a task queue, dispatches to registered workflows/activities
5. **`WorkflowInstance`** → per-execution determinism engine, `CoroutineScheduler` for single-threaded coroutine execution

### Interceptor System

Two independent chains: **client-side** (`ClientInterceptor` → `ClientOutboundInterceptor`) and **worker-side** (`WorkerInterceptor` → inbound/outbound interceptors for workflows, activities, and Nexus operations).

## C++ Code Conventions

- **C++20** with coroutines (`co_await`/`co_return`)
- **snake_case** for files, methods, variables; **PascalCase** for classes; **SCREAMING_SNAKE** for constants
- **`-Werror`/`/WX`** — all warnings are errors on all platforms
- **`-Wsign-conversion`** — implicit signed/unsigned conversions are errors; use explicit `static_cast<size_t>(i)` for vector indexing with int loop variables
- Allman brace style, space indentation
- Public headers use angle bracket includes (`#include <temporalio/...>`)
- `invalid_argument` for preconditions, `runtime_error` for runtime failures, `logic_error` for programming errors

## Testing Conventions

- **Google Test** (GTest) — tests are in `cpp/tests/`
- Integration tests requiring a live Temporal server use `WorkflowEnvironmentFixture`
- Test project is also buildable via `ctest` or by running the test binary directly
- CI runs: GCC 13 (coverage), Clang 18 (ASan/UBSan), MSVC 2022, GCC 13 + vcpkg
