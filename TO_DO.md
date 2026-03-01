# TO_DO.md — Remaining Work for Temporal C++ SDK

> **Generated:** 2026-02-28
> **Current state:** 715/715 unit tests passing (MSVC 2022). All 3 self-contained examples
> run E2E against a live Temporal server. 9 OTel tests excluded (opentelemetry-cpp not installed).

---

## 1. OpenTelemetry Extension — Stub Implementation (HIGH)

The `TracingInterceptor` in `cpp/extensions/opentelemetry/` compiles and links but
**does not actually create any OpenTelemetry spans**. Every interceptor method is a
pass-through stub that delegates to `next()` without instrumentation.

### 1.1 Span Creation (9 TODOs)

All in `extensions/opentelemetry/src/tracing_interceptor.cpp`:

| Line | Stub | Expected Behavior |
|------|------|-------------------|
| 32 | `execute_workflow_async()` | Create `"RunWorkflow:<type>"` span, extract parent context from headers, record result/exception |
| 41 | `handle_signal_async()` | Create `"HandleSignal:<name>"` span |
| 47 | `handle_query()` | Create `"HandleQuery:<name>"` span |
| 53 | `validate_update()` | Create `"ValidateUpdate:<name>"` span |
| 59 | `handle_update_async()` | Create `"HandleUpdate:<name>"` span |
| 86 | `execute_activity_async()` | Create `"RunActivity:<name>"` span, extract parent context, record result/exception |
| 132 | `inject_context()` | Use `TextMapPropagator` to inject current span context into headers |
| 144–155 | `extract_context()` | Use `TextMapPropagator` to extract trace context from headers (currently returns `true` as placeholder) |

### 1.2 Missing Client-Side Interceptor

The C# `TracingInterceptor` implements **both** `IClientInterceptor` (for `StartWorkflow`,
`SignalWorkflow`, `QueryWorkflow`, etc.) **and** `IWorkerInterceptor`. The C++ version only
implements `IWorkerInterceptor`. The `IClientInterceptor` half is missing entirely — no client
operations are traced.

**Reference:** `src/Temporalio.Extensions.OpenTelemetry/TracingInterceptor.cs`

### 1.3 Missing Outbound Interceptor Wrapping

Lines 24–28 and 78–81 of `tracing_interceptor.cpp` note:
> "a full implementation would also wrap outbound to inject trace context on outgoing calls
> (delay, schedule activity, start child workflow, etc.)"

The outbound interceptors are not wrapped, so trace context is never propagated to child
operations.

### 1.4 Dependency: opentelemetry-cpp Not Installed

The extension requires `opentelemetry-cpp` (via vcpkg or FetchContent). It is not currently
installed in the build environment. The 9 excluded tests are gated on
`#ifdef TEMPORALIO_HAS_OPENTELEMETRY` or the `opentelemetry-cpp_FOUND` CMake check.

**To unblock:**
```bash
vcpkg install opentelemetry-cpp
# or add to cpp/vcpkg.json dependencies
```

### 1.5 Run the 9 Excluded OTel Tests

Once opentelemetry-cpp is installed and the span creation is implemented, run the 9 excluded
tests in:
- `cpp/tests/extensions/opentelemetry/tracing_interceptor_tests.cpp` (15 tests, all currently pass against stubs)
- `cpp/tests/extensions/opentelemetry/tracing_options_tests.cpp` (5 tests, all pass)

The existing tests only verify the skeleton structure (construction, constants, interface
conformance). **New tests are needed** that verify actual span creation, parent-child
relationships, tag population, and context propagation.

---

## 2. Nexus Operation Handler — Incomplete Methods (MEDIUM)

File: `cpp/src/temporalio/nexus/operation_handler.cpp`

| Line | Method | Issue |
|------|--------|-------|
| 193–197 | `WorkflowStartOperationHandler::start_async()` | Has a TODO: outbound links, callbacks not wired |
| 231–236 | `WorkflowRunOperationHandler::fetch_result_async()` | `throw std::logic_error("fetch_result_async not implemented")` |
| 238–244 | `WorkflowRunOperationHandler::fetch_info_async()` | `throw std::logic_error("fetch_info_async not implemented")` |

`fetch_result_async` should poll/wait on the workflow execution result.
`fetch_info_async` should describe the workflow (depends on item 3 below).

---

## 3. WorkflowHandle::describe() — Stub (MEDIUM)

File: `cpp/src/temporalio/client/workflow_handle.cpp:331-335`

```cpp
coro::Task<std::string> WorkflowHandle::describe(
    const WorkflowDescribeOptions& options) {
    // TODO: Implement via client RPC
    co_return std::string{};
}
```

The RPC infrastructure exists in `TemporalClient` (7 other RPCs are wired). This needs a
`DescribeWorkflowExecution` RPC call, similar to the existing `start_workflow()` or
`terminate_workflow()` pattern.

---

## 4. WorkflowEnvironment::delay() — Blocking Sleep (LOW)

File: `cpp/src/temporalio/testing/workflow_environment.cpp:182-188`

```cpp
coro::Task<void> WorkflowEnvironment::delay(std::chrono::milliseconds duration) {
    // TODO: Use async sleep when async runtime is wired up
    std::this_thread::sleep_for(duration);
    co_return;
}
```

Uses `std::this_thread::sleep_for` (blocks the calling thread) instead of an async sleep.
Works for tests but is not appropriate for production use with the testing framework.

---

## 5. Test Coverage Gaps (MEDIUM)

From IMPLEMENTATION_PLAN.md section 11. These tests were identified after the double-encoding
bug was only caught by running examples against a live server.

- [ ] **Payload round-trip tests** — verify `payload_to_any()` -> typed wrapper ->
  `decode_payload_value<T>()` for json/plain, binary/null, binary/plain payloads
  (std::string, int, double, bool)
- [ ] **Activity arg decoding tests** — create `ActivityDefinition` with typed parameters,
  pass `converters::Payload` args (simulating server delivery), verify decoded values
- [ ] **Workflow arg decoding tests** — create `WorkflowDefinition` with typed `run(std::string)`,
  pass `converters::Payload` args through `WorkflowInstance` activation, verify decoded values
- [ ] **Signal arg decoding tests** — same for signal handlers with typed parameters
- [ ] **Query arg decoding tests** — same for query handlers with typed parameters
- [ ] **Activity result decoding tests** — verify `execute_activity<T>()` decodes
  `converters::Payload` results (json/plain encoded return values)
- [ ] **End-to-end example smoke tests** — start worker, run workflow with activities,
  verify final result (requires local dev server)

---

## 6. WorkflowEnvironmentFixture Auto-Download (LOW)

From IMPLEMENTATION_PLAN.md section 3:
- [ ] Set up `WorkflowEnvironmentFixture` to auto-download the local Temporal dev server

The C# test suite uses `WorkflowEnvironment.StartLocalAsync()` which auto-downloads
`temporal-test-server`. The C++ equivalent needs similar auto-download logic or documentation
on how to set `TEMPORAL_TEST_CLIENT_TARGET_HOST`.

---

## 7. Protobuf Integration Test (LOW)

From IMPLEMENTATION_PLAN.md section 4:
- [ ] End-to-end protobuf serialization test (integration testing)

Verify that protobuf messages serialize/deserialize correctly through the full
client -> bridge -> server -> bridge -> worker pipeline.

---

## 8. Linux/Cross-Platform Build (MEDIUM)

From IMPLEMENTATION_PLAN.md section 1:
- [ ] Test on GCC 11+ / Clang 14+ (Linux)

The entire project has only been built and tested on Windows (MSVC 2022). Linux builds are
untested. Potential issues: `<stop_token>` support (GCC 12+ needed), coroutine support
differences, Rust bridge `.so` vs `.dll` linking.

---

## 9. CI/CD Pipeline (LOW)

From IMPLEMENTATION_PLAN.md section 10:
- [ ] GitHub Actions workflow for Windows (MSVC) + Linux (GCC + Clang)
- [ ] Ninja-based CI builds
- [ ] AddressSanitizer and UndefinedBehaviorSanitizer CI runs
- [ ] Code coverage reporting
- [ ] Example programs verified in CI
- [ ] vcpkg install verification in CI

---

## 10. Documentation & Packaging Verification (LOW)

From IMPLEMENTATION_PLAN.md section 12:
- [ ] Doxygen generation from `@file` / `///` doc comments
- [ ] Verify `cmake --install` produces correct header + lib layout
- [ ] Verify `find_package(temporalio)` works via CMake config files

The install targets and vcpkg port files exist but have not been verified end-to-end.

---

## Summary

| # | Item | Priority | Type |
|---|------|----------|------|
| 1 | OpenTelemetry extension (span creation, TextMapPropagator, client interceptor) | **HIGH** | Implementation |
| 2 | Nexus fetch_result_async / fetch_info_async | MEDIUM | Implementation |
| 3 | WorkflowHandle::describe() RPC | MEDIUM | Implementation |
| 4 | WorkflowEnvironment::delay() async | LOW | Implementation |
| 5 | Test coverage gaps (payload round-trip, arg decoding) | MEDIUM | Testing |
| 6 | WorkflowEnvironmentFixture auto-download | LOW | Testing |
| 7 | Protobuf integration test | LOW | Testing |
| 8 | Linux/cross-platform build | MEDIUM | Build |
| 9 | CI/CD pipeline | LOW | DevOps |
| 10 | Documentation & packaging verification | LOW | DevOps |
