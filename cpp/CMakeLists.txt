cmake_minimum_required(VERSION 3.20)
project(temporalio VERSION 0.1.0 LANGUAGES CXX)

# ── C++20 standard ──────────────────────────────────────────────────────────
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ── Symbol visibility ─────────────────────────────────────────────────────
# Hide symbols by default on all platforms. Only symbols explicitly marked
# with TEMPORALIO_EXPORT are visible in shared library builds.
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN YES)

# MSVC: generator-conditional debug info and parallelism flags.
# Ninja handles parallelism itself, so /MP (MSBuild multi-process compilation)
# must NOT be used. /Zi (shared PDB) causes lock contention with Ninja's
# parallel compilation; /Z7 (embedded debug info) avoids this entirely.
# For MSBuild generators, /FS (force synchronous PDB writes) prevents C1041
# errors when parallel projects compile into the same PDB.
if(MSVC)
    if(CMAKE_GENERATOR MATCHES "Ninja")
        # Ninja handles parallelism; /Z7 embeds debug info to avoid PDB contention
        add_compile_options(/Z7)
    else()
        # MSBuild needs /FS for parallel PDB writes
        add_compile_options(/FS)
    endif()
endif()

# ── Options ─────────────────────────────────────────────────────────────────
option(BUILD_SHARED_LIBS "Build shared libraries" OFF)
option(TEMPORALIO_BUILD_EXTENSIONS "Build OpenTelemetry and Diagnostics extensions" ON)
option(TEMPORALIO_BUILD_TESTS "Build test suite" ON)
option(TEMPORALIO_BUILD_EXAMPLES "Build example programs" ON)
option(TEMPORALIO_BUILD_PROTOS "Generate and build Temporal API protobuf types" ON)

# ── CMake modules ───────────────────────────────────────────────────────────
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
include(Platform)
include(CompilerWarnings)

# ── Dependencies ───────────────────────────────────────────────────────────
# Try vcpkg / system packages first, fall back to FetchContent.
include(FetchContent)

find_package(Protobuf CONFIG QUIET)
if(NOT Protobuf_FOUND)
    find_package(Protobuf QUIET)
endif()
if(NOT Protobuf_FOUND)
    message(STATUS "Temporalio: Protobuf not found via find_package, using FetchContent")
    set(protobuf_BUILD_TESTS OFF CACHE BOOL "" FORCE)
    set(protobuf_BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
    set(protobuf_INSTALL OFF CACHE BOOL "" FORCE)
    set(protobuf_MSVC_STATIC_RUNTIME OFF CACHE BOOL "" FORCE)
    set(ABSL_PROPAGATE_CXX_STD ON CACHE BOOL "" FORCE)
    set(ABSL_ENABLE_INSTALL OFF CACHE BOOL "" FORCE)
    set(ABSL_MSVC_STATIC_RUNTIME OFF CACHE BOOL "" FORCE)
    # Fetch abseil-cpp first (protobuf depends on it)
    FetchContent_Declare(
        abseil-cpp
        URL https://github.com/abseil/abseil-cpp/releases/download/20240722.0/abseil-cpp-20240722.0.tar.gz
        DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    )
    FetchContent_MakeAvailable(abseil-cpp)
    set(protobuf_ABSL_PROVIDER "package" CACHE STRING "" FORCE)
    set(utf8_range_ENABLE_INSTALL OFF CACHE BOOL "" FORCE)
    FetchContent_Declare(
        protobuf
        URL https://github.com/protocolbuffers/protobuf/releases/download/v29.3/protobuf-29.3.tar.gz
        DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    )
    FetchContent_MakeAvailable(protobuf)

    # Suppress all warnings from FetchContent-built protobuf library targets.
    # These targets compile protobuf .cc files which emit -Wmissing-requires
    # and -Wdeprecated warnings we don't control.
    foreach(_pb_target libprotobuf libprotobuf-lite libprotoc)
        if(TARGET ${_pb_target})
            target_compile_options(${_pb_target} PRIVATE -w)
        endif()
    endforeach()
else()
    message(STATUS "Temporalio: Found Protobuf via find_package")
endif()

find_package(nlohmann_json CONFIG QUIET)
if(NOT nlohmann_json_FOUND)
    message(STATUS "Temporalio: nlohmann_json not found via find_package, using FetchContent")
    set(JSON_BuildTests OFF CACHE BOOL "" FORCE)
    FetchContent_Declare(
        nlohmann_json
        URL https://github.com/nlohmann/json/releases/download/v3.11.3/json.tar.xz
        DOWNLOAD_EXTRACT_TIMESTAMP TRUE
    )
    FetchContent_MakeAvailable(nlohmann_json)
else()
    message(STATUS "Temporalio: Found nlohmann_json via find_package")
endif()

# Determine protobuf target name (differs between find_package and FetchContent).
# This must be set before ProtobufGenerate.cmake is included, since the proto
# library target links against it.
if(TARGET protobuf::libprotobuf)
    set(TEMPORALIO_PROTOBUF_TARGET protobuf::libprotobuf)
elseif(TARGET protobuf::libprotobuf-lite)
    set(TEMPORALIO_PROTOBUF_TARGET protobuf::libprotobuf-lite)
else()
    set(TEMPORALIO_PROTOBUF_TARGET protobuf::libprotobuf)
endif()

# Mark protobuf library includes as SYSTEM so GCC/Clang suppress warnings
# from protobuf headers (e.g. -Wconversion in map.h template instantiations).
# FetchContent creates ALIAS targets, so resolve to the real target first.
set(_pb_real_target ${TEMPORALIO_PROTOBUF_TARGET})
if(TARGET ${_pb_real_target})
    get_target_property(_pb_aliased ${_pb_real_target} ALIASED_TARGET)
    if(_pb_aliased)
        set(_pb_real_target ${_pb_aliased})
    endif()
    get_target_property(_pb_iid ${_pb_real_target} INTERFACE_INCLUDE_DIRECTORIES)
    if(_pb_iid)
        set_target_properties(${_pb_real_target} PROPERTIES
            INTERFACE_SYSTEM_INCLUDE_DIRECTORIES "${_pb_iid}")
    endif()
endif()

# ── Rust bridge custom build (must be before library target) ────────────────
temporalio_build_rust_bridge(
    TARGET temporalio_rust_bridge
    CARGO_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../src/Temporalio/Bridge/sdk-core"
    PACKAGE_NAME temporalio-sdk-core-c-bridge
    CRATE_NAME temporalio_sdk_core_c_bridge
)

# ── Protobuf code generation ──────────────────────────────────────────────
# Generate C++ types from Temporal API .proto files. The generated .pb.h/.pb.cc
# files are placed in ${CMAKE_CURRENT_BINARY_DIR}/proto_gen/ and compiled into
# the temporalio_proto static library.
set(TEMPORALIO_HAS_PROTOS FALSE)
if(TEMPORALIO_BUILD_PROTOS)
    # ProtobufGenerate must be included AFTER Protobuf is found (needs protoc)
    include(ProtobufGenerate)
    set(TEMPORALIO_PROTO_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../src/Temporalio/Bridge/sdk-core/crates/common/protos")
    if(EXISTS "${TEMPORALIO_PROTO_ROOT}")
        temporalio_generate_protos(
            TARGET temporalio_proto
            PROTO_ROOT "${TEMPORALIO_PROTO_ROOT}"
        )
        set(TEMPORALIO_HAS_PROTOS TRUE)
    else()
        message(WARNING "Temporalio: Proto root not found at ${TEMPORALIO_PROTO_ROOT} - "
            "run 'git submodule update --init --recursive' to fetch proto definitions")
    endif()
endif()

# ── Collect C++ source files ────────────────────────────────────────────────
# All C++ code lives under cpp/ (this directory). Source files are under src/.
# NOTE: GLOB_RECURSE is convenient during early development but fragile -- new
# files won't trigger a reconfigure. Switch to explicit source lists before release.
file(GLOB_RECURSE TEMPORALIO_SOURCES
    "${CMAKE_CURRENT_SOURCE_DIR}/src/temporalio/*.cpp"
)

file(GLOB_RECURSE TEMPORALIO_HEADERS
    "${CMAKE_CURRENT_SOURCE_DIR}/include/temporalio/*.h"
)

# When the real Rust bridge is available (cargo found), exclude the FFI stubs
# to avoid duplicate symbol definitions.
if(CARGO_EXECUTABLE)
    list(FILTER TEMPORALIO_SOURCES EXCLUDE REGEX "ffi_stubs\\.cpp$")
endif()

# Guard: if no .cpp files exist yet, generate a placeholder so the target is
# always valid even in a fresh checkout.
if(NOT TEMPORALIO_SOURCES)
    message(STATUS "Temporalio: No .cpp source files found — creating placeholder library")
    file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/temporalio_placeholder.cpp"
        "// Auto-generated placeholder — remove once real sources exist\n")
    set(TEMPORALIO_SOURCES "${CMAKE_CURRENT_BINARY_DIR}/temporalio_placeholder.cpp")
endif()

# ── Main library target ────────────────────────────────────────────────────
add_library(temporalio ${TEMPORALIO_SOURCES})

# Generate export header for shared library symbol visibility.
# Produces temporalio/export.h with TEMPORALIO_EXPORT and TEMPORALIO_NO_EXPORT macros.
include(GenerateExportHeader)
generate_export_header(temporalio
    EXPORT_FILE_NAME "${CMAKE_CURRENT_BINARY_DIR}/include/temporalio/export.h"
    EXPORT_MACRO_NAME TEMPORALIO_EXPORT
    STATIC_DEFINE TEMPORALIO_STATIC
)

target_include_directories(temporalio
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/include>
        $<INSTALL_INTERFACE:include>
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

target_link_libraries(temporalio
    PUBLIC
        ${TEMPORALIO_PROTOBUF_TARGET}
        nlohmann_json::nlohmann_json
        $<BUILD_INTERFACE:temporalio_rust_bridge>
)

# Link generated proto types if available
if(TEMPORALIO_HAS_PROTOS)
    target_link_libraries(temporalio PUBLIC temporalio_proto)
endif()

target_compile_features(temporalio PUBLIC cxx_std_20)

# Define TEMPORALIO_HAS_PROTOBUF so that proto payload converters are compiled in.
# If protobuf is not available, data_converter.cpp omits JsonProtoConverter and
# BinaryProtoConverter from the default converter set.
target_compile_definitions(temporalio PUBLIC TEMPORALIO_HAS_PROTOBUF)

temporalio_set_compiler_warnings(temporalio)

# ── Extensions ──────────────────────────────────────────────────────────────
if(TEMPORALIO_BUILD_EXTENSIONS)
    add_subdirectory(extensions/opentelemetry)
    add_subdirectory(extensions/diagnostics)
endif()

# ── Tests ───────────────────────────────────────────────────────────────────
if(TEMPORALIO_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests)
endif()

# ── Examples ────────────────────────────────────────────────────────────────
if(TEMPORALIO_BUILD_EXAMPLES)
    add_subdirectory(examples)
endif()

# ── Documentation ──────────────────────────────────────────────────────────
find_package(Doxygen QUIET)
if(DOXYGEN_FOUND)
    add_custom_target(docs
        COMMAND ${DOXYGEN_EXECUTABLE} "${CMAKE_CURRENT_SOURCE_DIR}/Doxyfile"
        WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
        COMMENT "Generating API documentation with Doxygen"
        VERBATIM
    )
else()
    message(STATUS "Temporalio: Doxygen not found - 'docs' target will not be available")
endif()

# ── Install & Packaging ─────────────────────────────────────────────────────
include(GNUInstallDirs)
include(CMakePackageConfigHelpers)

# Determine whether CMake export sets are possible. install(EXPORT ...) requires
# every linked dependency target to also be in an export set. When dependencies
# come from FetchContent (not system find_package), they are not exported and
# CMake generation will fail. Only enable EXPORT when deps are system-provided.
set(TEMPORALIO_CAN_EXPORT TRUE)
if(NOT Protobuf_FOUND OR NOT nlohmann_json_FOUND)
    set(TEMPORALIO_CAN_EXPORT FALSE)
endif()

# Collect all installable library targets.
set(_temporalio_install_targets temporalio)
if(TEMPORALIO_HAS_PROTOS AND TARGET temporalio_proto)
    list(APPEND _temporalio_install_targets temporalio_proto)
endif()

if(TEMPORALIO_CAN_EXPORT)
    install(TARGETS ${_temporalio_install_targets}
        EXPORT temporalioTargets
        ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
        LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
    )
else()
    install(TARGETS ${_temporalio_install_targets}
        ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
        LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
        RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    )
endif()

# Install public headers preserving directory structure.
install(DIRECTORY include/temporalio
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
    FILES_MATCHING PATTERN "*.h"
)

# Install the generated export.h header.
install(FILES "${CMAKE_CURRENT_BINARY_DIR}/include/temporalio/export.h"
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/temporalio
)

# Install generated protobuf headers so consumers can use Temporal API types.
if(TEMPORALIO_HAS_PROTOS AND DEFINED TEMPORALIO_PROTO_GEN_DIR)
    install(DIRECTORY "${TEMPORALIO_PROTO_GEN_DIR}/"
        DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
        FILES_MATCHING PATTERN "*.pb.h"
    )
endif()

# Install the Rust bridge shared library (DLL/SO/dylib) alongside the SDK.
# The Rust bridge is always a shared library (cdylib crate type) even when the
# C++ SDK itself is built as a static library. Consumers need it at runtime.
if(CARGO_EXECUTABLE AND TARGET temporalio_rust_bridge)
    get_target_property(_bridge_location temporalio_rust_bridge IMPORTED_LOCATION)
    if(_bridge_location)
        if(WIN32)
            # On Windows: DLL goes to bin/, import lib (.dll.lib) goes to lib/.
            install(FILES "${_bridge_location}" DESTINATION ${CMAKE_INSTALL_BINDIR})
            get_target_property(_bridge_implib temporalio_rust_bridge IMPORTED_IMPLIB)
            if(_bridge_implib)
                install(FILES "${_bridge_implib}" DESTINATION ${CMAKE_INSTALL_LIBDIR})
            endif()
        else()
            # On Unix: shared library (.so/.dylib) goes to lib/.
            install(FILES "${_bridge_location}" DESTINATION ${CMAKE_INSTALL_LIBDIR})
        endif()
    endif()
endif()

# Install extension targets if they were built.
if(TARGET temporalio_diagnostics)
    if(TEMPORALIO_CAN_EXPORT)
        install(TARGETS temporalio_diagnostics
            EXPORT temporalioTargets
            ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
            LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
            RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
            INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
        )
    else()
        install(TARGETS temporalio_diagnostics
            ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
            LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
            RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        )
    endif()
    install(DIRECTORY extensions/diagnostics/include/
        DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
        FILES_MATCHING PATTERN "*.h"
    )
endif()

if(TARGET temporalio_opentelemetry)
    if(TEMPORALIO_CAN_EXPORT)
        install(TARGETS temporalio_opentelemetry
            EXPORT temporalioTargets
            ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
            LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
            RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
            INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
        )
    else()
        install(TARGETS temporalio_opentelemetry
            ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
            LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
            RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
        )
    endif()
    install(DIRECTORY extensions/opentelemetry/include/
        DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
        FILES_MATCHING PATTERN "*.h"
    )
endif()

# Export targets and CMake package config for downstream find_package().
# Only available when all dependencies were found via find_package (system-installed).
if(TEMPORALIO_CAN_EXPORT)
    install(EXPORT temporalioTargets
        FILE temporalioTargets.cmake
        NAMESPACE temporalio::
        DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/temporalio
    )

    configure_package_config_file(
        "${CMAKE_CURRENT_SOURCE_DIR}/cmake/temporalioConfig.cmake.in"
        "${CMAKE_CURRENT_BINARY_DIR}/temporalioConfig.cmake"
        INSTALL_DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/temporalio
    )

    write_basic_package_version_file(
        "${CMAKE_CURRENT_BINARY_DIR}/temporalioConfigVersion.cmake"
        VERSION ${PROJECT_VERSION}
        COMPATIBILITY SameMajorVersion
    )

    install(FILES
        "${CMAKE_CURRENT_BINARY_DIR}/temporalioConfig.cmake"
        "${CMAKE_CURRENT_BINARY_DIR}/temporalioConfigVersion.cmake"
        DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/temporalio
    )
else()
    message(STATUS "Temporalio: CMake export/find_package support disabled "
        "(dependencies come from FetchContent, not system packages)")
endif()

# Generate pkg-config file (always available regardless of export support).
configure_file(
    "${CMAKE_CURRENT_SOURCE_DIR}/cmake/temporalio.pc.in"
    "${CMAKE_CURRENT_BINARY_DIR}/temporalio.pc"
    @ONLY
)

install(FILES "${CMAKE_CURRENT_BINARY_DIR}/temporalio.pc"
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/pkgconfig
)
