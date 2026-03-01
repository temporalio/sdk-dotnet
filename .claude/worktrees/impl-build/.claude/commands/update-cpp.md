# Sync Upstream C# SDK Changes to C++ Port

You are performing an incremental sync of upstream Temporal .NET SDK changes into this
repository's C++ port. Follow each step carefully and report progress as you go.

## Constants

- **Initial port base commit:** `589c7c316a159e2a27151b9a52191c6f8572e4b9`
- **Sync state file:** `.claude/sync-state.json`
- **Upstream remote:** `upstream` → `https://github.com/temporalio/sdk-dotnet.git`
- **Working branch:** `cpp-conversion`

## Arguments

Parse `$ARGUMENTS` for flags:
- `--dry-run` — Show what would change but make no modifications
- No arguments — Perform the full sync

---

## Step 1: Pre-flight Checks

1. Verify you are on the `cpp-conversion` branch. If not, stop with an error.
2. Verify a clean working tree (`git status --porcelain`). If dirty, stop and warn the user.
3. Read `.claude/sync-state.json`. If the file does not exist, this is the first run — use
   the initial port base commit as the starting point.
4. Check that the `upstream` remote exists (`git remote get-url upstream`). If missing, add it:
   `git remote add upstream https://github.com/temporalio/sdk-dotnet.git`

## Step 2: Fetch & Analyze Upstream Changes

1. Run `git fetch upstream`.
2. Determine the base commit:
   - If sync state exists: use `last_synced_upstream_commit`
   - Otherwise: use `589c7c316a159e2a27151b9a52191c6f8572e4b9`
3. List new commits: `git log --oneline <base>..upstream/main`
4. Get changed files: `git diff --name-status <base>..upstream/main -- src/Temporalio/ tests/Temporalio.Tests/`
5. Categorize every changed file into one of these groups:

| Category | Paths | Action |
|----------|-------|--------|
| **Port-relevant** | `Client/`, `Workflows/`, `Activities/`, `Worker/`, `Converters/`, `Runtime/`, `Testing/`, `Common/`, `Exceptions/`, `Nexus/` | Port to C++ |
| **Bridge-internal** | `Bridge/` | Skip unless new C API surface functions are added |
| **Infra-only** | `.github/`, `*.csproj`, NuGet configs, `.editorconfig` | Skip |
| **Proto** | `Api/` | Note for proto regeneration, do not auto-port |
| **Submodule** | `sdk-core` | Note version bump, do not auto-port |

6. Present the categorized summary to the user in a clear table.

## Step 3: User Confirmation

- If `--dry-run` was passed: **stop here**. Print the summary and exit without modifying anything.
- Otherwise: ask the user which change groups to port. Default is all port-relevant changes.
  Let the user exclude specific files or groups if desired.

## Step 4: Merge Upstream into Local Branches

1. `git checkout main && git pull upstream main && git checkout cpp-conversion`
2. `git merge main` — this brings the C# reference code up to date in the working branch.
3. Handle merge conflicts:
   - Conflicts in C# files (`src/Temporalio/`, `tests/Temporalio.Tests/`): accept upstream version.
   - Conflicts in C++ files (`cpp/`): **stop and alert the user**. Do not auto-resolve these.
   - Conflicts in `.claude/`: accept the `cpp-conversion` version (ours).

## Step 5: Port Changes

For each port-relevant changed file, in dependency order:

### For modified files (M):
1. Read the C# diff (`git diff <base>..upstream/main -- <file>`)
2. Use the namespace mapping table (below) to find the corresponding C++ header and source
3. Read the current C++ files
4. Apply the equivalent change following the porting conventions (below)
5. If the C# change adds a new public method/class, add it to the C++ header + implementation

### For new files (A):
1. Read the full new C# file
2. Create the corresponding C++ header in `cpp/include/temporalio/` and source in `cpp/src/temporalio/`
3. Add the new files to the appropriate `CMakeLists.txt`
4. Follow all naming and convention rules

### For deleted files (D):
1. Confirm with the user before removing any C++ files
2. Remove from `CMakeLists.txt` if confirmed

### For test changes:
1. Map `tests/Temporalio.Tests/{Area}/` to `cpp/tests/{area}/`
2. Convert xUnit patterns to Google Test (see conventions below)
3. Add new test files to `cpp/tests/CMakeLists.txt`

## Step 6: Build & Test

1. Configure if needed: `cmake -S cpp -B cpp/build -DCMAKE_BUILD_TYPE=Debug`
2. Build: `cmake --build cpp/build`
3. Test: `ctest --test-dir cpp/build --output-on-failure`
4. If build or test failures:
   - Diagnose the error
   - Fix the issue
   - Rebuild and re-test
   - If you cannot fix it after 2 attempts, ask the user for guidance

## Step 7: Update Sync State & Commit

1. Determine the new upstream HEAD: `git rev-parse upstream/main`
2. Write (or update) `.claude/sync-state.json` with this schema:

```json
{
  "version": 1,
  "initial_port_base_commit": "589c7c316a159e2a27151b9a52191c6f8572e4b9",
  "last_synced_upstream_commit": "<new upstream/main hash>",
  "last_synced_upstream_date": "<ISO-8601 date of that commit>",
  "sync_timestamp": "<ISO-8601 now>",
  "sync_history": [
    {
      "timestamp": "<ISO-8601 now>",
      "from_commit": "<previous base hash>",
      "to_commit": "<new upstream/main hash>",
      "changes_summary": "Brief description of what was ported",
      "files_created": ["list of new C++ files"],
      "files_modified": ["list of modified C++ files"],
      "skipped": ["list of skipped files with reasons"]
    }
  ]
}
```

3. Stage all changed files: C++ sources, test files, CMakeLists.txt, sync state
4. Commit with a message like: `Sync upstream C# changes <short-hash>..<short-hash> to C++ port`
5. Do NOT push unless the user explicitly asks.

---

## Namespace → Directory Mapping

Use this table to find the C++ counterpart of each C# file:

```
src/Temporalio/Client/*.cs              → cpp/include/temporalio/client/*.h + cpp/src/temporalio/client/*.cpp
src/Temporalio/Client/Interceptors/     → cpp/include/temporalio/client/interceptors/*.h
src/Temporalio/Workflows/*.cs           → cpp/include/temporalio/workflows/*.h + cpp/src/temporalio/workflows/*.cpp
src/Temporalio/Activities/*.cs          → cpp/include/temporalio/activities/*.h + cpp/src/temporalio/activities/*.cpp
src/Temporalio/Worker/*.cs              → cpp/include/temporalio/worker/*.h + cpp/src/temporalio/worker/*.cpp
src/Temporalio/Worker/Interceptors/     → cpp/include/temporalio/worker/interceptors/*.h
src/Temporalio/Converters/*.cs          → cpp/include/temporalio/converters/*.h + cpp/src/temporalio/converters/*.cpp
src/Temporalio/Runtime/*.cs             → cpp/include/temporalio/runtime/*.h + cpp/src/temporalio/runtime/*.cpp
src/Temporalio/Testing/*.cs             → cpp/include/temporalio/testing/*.h + cpp/src/temporalio/testing/*.cpp
src/Temporalio/Common/*.cs              → cpp/include/temporalio/common/*.h + cpp/src/temporalio/common/*.cpp
src/Temporalio/Exceptions/*.cs          → cpp/include/temporalio/exceptions/temporal_exception.h (consolidated)
src/Temporalio/Nexus/*.cs               → cpp/include/temporalio/nexus/*.h + cpp/src/temporalio/nexus/*.cpp
tests/Temporalio.Tests/{Area}/          → cpp/tests/{area}/
```

**File naming:** C# `PascalCase.cs` → C++ `snake_case.h` / `snake_case.cpp`
**Partial classes:** Merge into a single C++ class.

---

## C# → C++ Porting Conventions

### Type Mapping

| C# | C++ |
|----|-----|
| `async Task<T>` | `coro::Task<T>` (co_await / co_return) |
| `string` / `string?` | `std::string` / `std::optional<std::string>` |
| `T?` (nullable value type) | `std::optional<T>` |
| `T?` (nullable reference type) | `T*` or `std::optional<T>` depending on ownership |
| `List<T>` | `std::vector<T>` |
| `Dictionary<K,V>` | `std::unordered_map<K,V>` |
| `IReadOnlyCollection<T>` | `const std::vector<T>&` |
| `IReadOnlyDictionary<K,V>` | `const std::unordered_map<K,V>&` |
| `CancellationToken` | `std::stop_token` |
| `TimeSpan` | `std::chrono::milliseconds` |
| `DateTimeOffset` | `std::chrono::system_clock::time_point` |
| `byte[]` | `std::vector<uint8_t>` |
| `object` | `std::any` |
| `record` | `struct` with `operator==` |
| `interface` | pure virtual class |
| `IDisposable` | destructor (RAII) |
| `IAsyncDisposable` | `coro::Task<void> close()` |
| `event` | `std::function<void(Args...)>` |
| `Action<T>` | `std::function<void(T)>` |
| `Func<T, TResult>` | `std::function<TResult(T)>` |
| `Lazy<T>` | `std::once_flag` + `std::call_once` |

### Naming Conventions

- **Classes/Structs:** `PascalCase` (e.g., `TemporalClient`, `WorkflowOptions`)
- **Methods/Functions:** `snake_case` (e.g., `start_workflow`, `get_result`)
- **Variables/Fields:** `snake_case` with trailing underscore for members (e.g., `client_`, `options_`)
- **Constants:** `SCREAMING_SNAKE_CASE` (e.g., `DEFAULT_TIMEOUT`)
- **Files:** `snake_case.h` / `snake_case.cpp`
- **Namespaces:** `temporalio::client`, `temporalio::workflows`, etc.

### Patterns

- **Pimpl idiom:** `std::unique_ptr<Impl>` for implementation hiding
- **Factory returns:** `std::shared_ptr<T>` for objects with shared ownership
- **Context propagation:** `thread_local` + RAII scope guards (`WorkflowContextScope`, `ActivityContextScope`)
- **Interceptor chains:** raw pointer chains (factory owns lifetime), `unique_ptr` for client interceptors
- **Type erasure:** `std::any` for generic payloads

### Error Handling

- `std::invalid_argument` — precondition violations
- `std::runtime_error` — runtime failures
- `std::logic_error` — programming errors
- Custom exception hierarchy under `temporalio::exceptions::` mirrors the C# `Temporal*Exception` types

### Test Mapping

| C# (xUnit) | C++ (Google Test) |
|-------------|-------------------|
| `[Fact]` | `TEST(SuiteName, TestName)` |
| `[Theory]` + `[InlineData]` | `TEST_P` or multiple `TEST` cases |
| `Assert.Equal(a, b)` | `EXPECT_EQ(a, b)` |
| `Assert.NotNull(x)` | `EXPECT_NE(x, nullptr)` |
| `Assert.True(x)` | `EXPECT_TRUE(x)` |
| `Assert.Throws<T>(...)` | `EXPECT_THROW(..., T)` |
| `Assert.Contains(s, sub)` | `EXPECT_THAT(s, ::testing::HasSubstr(sub))` |
| `[Collection]` fixture | test fixture class inheriting `::testing::Test` |

### Important Notes

- Always read the existing C++ code before modifying it — understand current patterns
- Preserve existing C++ architectural decisions (e.g., coroutine scheduler design)
- When a C# change modifies behavior, ensure the C++ port matches the new behavior exactly
- When in doubt about a mapping, check how similar patterns were handled in existing C++ code
- Do not port C#-specific concerns (NuGet, MSBuild, .NET TFM targeting, etc.)
