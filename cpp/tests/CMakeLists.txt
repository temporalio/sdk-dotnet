# tests/CMakeLists.txt - Google Test targets for Temporal C++ SDK

find_package(GTest CONFIG QUIET)
if(NOT GTest_FOUND)
    find_package(GTest QUIET)
endif()
if(NOT GTest_FOUND)
    message(STATUS "Temporalio: GTest not found via find_package, using FetchContent")
    include(FetchContent)
    FetchContent_Declare(
        googletest
        URL https://github.com/google/googletest/releases/download/v1.15.2/googletest-1.15.2.tar.gz
        DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    )
    # Prevent overriding parent project compiler/linker settings on Windows
    set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)
    FetchContent_MakeAvailable(googletest)
else()
    message(STATUS "Temporalio: Found GTest via find_package")
endif()

include(GoogleTest)

# ── Collect test source files ───────────────────────────────────────────────
file(GLOB_RECURSE TEMPORALIO_TEST_SOURCES
    "${CMAKE_CURRENT_SOURCE_DIR}/*.cpp"
)

# Exclude extension test files if extensions are not being built
if(NOT TARGET temporalio_opentelemetry)
    list(FILTER TEMPORALIO_TEST_SOURCES EXCLUDE REGEX ".*/extensions/opentelemetry/.*")
endif()
if(NOT TARGET temporalio_diagnostics)
    list(FILTER TEMPORALIO_TEST_SOURCES EXCLUDE REGEX ".*/extensions/diagnostics/.*")
endif()

if(NOT TEMPORALIO_TEST_SOURCES)
    message(STATUS "Temporalio: No test sources found yet, creating placeholder target")
    # Create a minimal test main so the target always exists
    file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/test_main_placeholder.cpp"
        "#include <gtest/gtest.h>\n"
        "// Placeholder - real tests will be added as source files appear\n"
    )
    set(TEMPORALIO_TEST_SOURCES "${CMAKE_CURRENT_BINARY_DIR}/test_main_placeholder.cpp")
endif()

# ── Test executable ─────────────────────────────────────────────────────────
add_executable(temporalio_tests ${TEMPORALIO_TEST_SOURCES})

target_link_libraries(temporalio_tests
    PRIVATE
        temporalio
        GTest::gtest
        GTest::gmock
)

# Link extension libraries if they are being built, so tests can
# include extension headers.
if(TARGET temporalio_diagnostics)
    target_link_libraries(temporalio_tests PRIVATE temporalio_diagnostics)
endif()
if(TARGET temporalio_opentelemetry)
    target_link_libraries(temporalio_tests PRIVATE temporalio_opentelemetry)
endif()

target_include_directories(temporalio_tests
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${CMAKE_CURRENT_SOURCE_DIR}/fixtures
        # Bridge headers live under src/ (PRIVATE to the main lib),
        # but tests may need direct access for unit-testing bridge code.
        ${CMAKE_SOURCE_DIR}/src
)

temporalio_set_compiler_warnings(temporalio_tests)

# ── Copy Rust bridge DLL to test output directory (Windows) ─────────────────
if(WIN32 AND TARGET temporalio_rust_bridge)
    add_custom_command(TARGET temporalio_tests POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "$<TARGET_FILE:temporalio_rust_bridge>"
            "$<TARGET_FILE_DIR:temporalio_tests>"
        COMMENT "Copying Rust bridge DLL to test directory"
        VERBATIM
    )
endif()

# ── Register tests with CTest ──────────────────────────────────────────────
# Run all tests in a single process rather than per-test processes.
# The WorkflowEnvironmentFixture is a Google Test global environment that
# starts a dev server once in SetUp() and shuts it down in TearDown().
# gtest_discover_tests() would run each of 742 tests as a separate process,
# each starting/stopping the dev server via Rust FFI — causing process-exit
# race conditions that manifest as heap corruption on Linux/GCC.
add_test(NAME temporalio_tests COMMAND temporalio_tests)
set_tests_properties(temporalio_tests PROPERTIES
    WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
    TIMEOUT 300
)
