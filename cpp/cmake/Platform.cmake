# Platform.cmake - Platform detection and Rust bridge build support

# ── Platform detection ──────────────────────────────────────────────────────
if(WIN32)
    set(TEMPORALIO_PLATFORM "windows")
    set(TEMPORALIO_RUST_LIB_PREFIX "")
    set(TEMPORALIO_RUST_STATIC_SUFFIX ".lib")
    set(TEMPORALIO_RUST_SHARED_SUFFIX ".dll")
elseif(APPLE)
    set(TEMPORALIO_PLATFORM "macos")
    set(TEMPORALIO_RUST_LIB_PREFIX "lib")
    set(TEMPORALIO_RUST_STATIC_SUFFIX ".a")
    set(TEMPORALIO_RUST_SHARED_SUFFIX ".dylib")
else()
    set(TEMPORALIO_PLATFORM "linux")
    set(TEMPORALIO_RUST_LIB_PREFIX "lib")
    set(TEMPORALIO_RUST_STATIC_SUFFIX ".a")
    set(TEMPORALIO_RUST_SHARED_SUFFIX ".so")
endif()

message(STATUS "Temporalio: Detected platform: ${TEMPORALIO_PLATFORM}")

# ── Find Rust / Cargo ──────────────────────────────────────────────────────
find_program(CARGO_EXECUTABLE cargo)
if(CARGO_EXECUTABLE)
    message(STATUS "Temporalio: Found cargo: ${CARGO_EXECUTABLE}")
else()
    message(STATUS "Temporalio: cargo not found - Rust bridge build will be stubbed")
endif()

# ── temporalio_build_rust_bridge ────────────────────────────────────────────
# Creates an IMPORTED static library target built via `cargo build`.
#
# Usage:
#   temporalio_build_rust_bridge(
#       TARGET <imported-target-name>
#       CARGO_DIR <path-to-crate-or-workspace>
#       CRATE_NAME <crate-name>
#   )
function(temporalio_build_rust_bridge)
    cmake_parse_arguments(PARSE_ARGV 0 ARG "" "TARGET;CARGO_DIR;CRATE_NAME;PACKAGE_NAME" "")

    if(NOT ARG_TARGET OR NOT ARG_CARGO_DIR OR NOT ARG_CRATE_NAME)
        message(FATAL_ERROR "temporalio_build_rust_bridge requires TARGET, CARGO_DIR, and CRATE_NAME")
    endif()

    # PACKAGE_NAME is the Cargo package name (with hyphens) for `cargo build -p`.
    # CRATE_NAME is the lib name (with underscores) for the output file.
    # If PACKAGE_NAME is not specified, fall back to CRATE_NAME.
    if(NOT ARG_PACKAGE_NAME)
        set(ARG_PACKAGE_NAME "${ARG_CRATE_NAME}")
    endif()

    # If cargo is not available, create a stub INTERFACE target so downstream
    # targets can still link against it (no actual Rust library will be produced).
    if(NOT CARGO_EXECUTABLE)
        add_library(${ARG_TARGET} INTERFACE)
        message(STATUS "Temporalio: Rust bridge target '${ARG_TARGET}' -> STUB (cargo not found)")
        return()
    endif()

    # Determine Rust build profile.
    # For multi-config generators (Visual Studio, Ninja Multi-Config) there is
    # no single CMAKE_BUILD_TYPE at configure time.  We default to debug and
    # provide per-config overrides via IMPORTED_LOCATION_<CONFIG>.
    set(RUST_PROFILE_debug   "debug")
    set(RUST_PROFILE_release "release")
    set(CARGO_FLAGS_debug    "")
    set(CARGO_FLAGS_release  "--release")

    # Single-config generator path (CMAKE_BUILD_TYPE is set)
    if(CMAKE_BUILD_TYPE)
        string(TOUPPER "${CMAKE_BUILD_TYPE}" _build_upper)
        if(_build_upper STREQUAL "RELEASE" OR _build_upper STREQUAL "RELWITHDEBINFO" OR _build_upper STREQUAL "MINSIZEREL")
            set(_rust_profile "release")
            set(_cargo_flags  "--release")
        else()
            set(_rust_profile "debug")
            set(_cargo_flags  "")
        endif()
    else()
        # Multi-config: build both debug and release, set per-config locations below
        set(_rust_profile "debug")
        set(_cargo_flags  "")
    endif()

    set(RUST_OUTPUT_DIR "${ARG_CARGO_DIR}/target/${_rust_profile}")
    # The Rust crate is a cdylib. On Windows, cargo produces a .dll and a
    # .dll.lib import library.  On Unix it produces a .so / .dylib.
    # We link the import library on Windows; on Unix we link the shared lib.
    if(WIN32)
        set(RUST_LIB_NAME "${ARG_CRATE_NAME}.dll.lib")
    elseif(APPLE)
        set(RUST_LIB_NAME "${TEMPORALIO_RUST_LIB_PREFIX}${ARG_CRATE_NAME}${TEMPORALIO_RUST_SHARED_SUFFIX}")
    else()
        set(RUST_LIB_NAME "${TEMPORALIO_RUST_LIB_PREFIX}${ARG_CRATE_NAME}${TEMPORALIO_RUST_SHARED_SUFFIX}")
    endif()
    set(RUST_LIB_PATH "${RUST_OUTPUT_DIR}/${RUST_LIB_NAME}")

    # Resolve protoc for Rust bridge (prost/tonic need PROTOC env var).
    set(_cargo_env_prefix "")
    if(TARGET protobuf::protoc)
        get_target_property(_bridge_protoc protobuf::protoc IMPORTED_LOCATION)
        if(NOT _bridge_protoc)
            get_target_property(_bridge_protoc protobuf::protoc IMPORTED_LOCATION_RELEASE)
        endif()
    endif()
    if(NOT _bridge_protoc)
        find_program(_bridge_protoc protoc)
    endif()
    if(_bridge_protoc)
        set(_cargo_env_prefix ${CMAKE_COMMAND} -E env "PROTOC=${_bridge_protoc}")
        # Also set PROTOC_INCLUDE for well-known type .proto imports
        get_filename_component(_protoc_dir "${_bridge_protoc}" DIRECTORY)
        get_filename_component(_protoc_prefix "${_protoc_dir}" DIRECTORY)
        if(EXISTS "${_protoc_prefix}/include/google/protobuf/any.proto")
            list(APPEND _cargo_env_prefix "PROTOC_INCLUDE=${_protoc_prefix}/include")
        endif()
    endif()

    # Custom command to build the Rust crate
    add_custom_command(
        OUTPUT "${RUST_LIB_PATH}"
        COMMAND ${_cargo_env_prefix} ${CARGO_EXECUTABLE} build ${_cargo_flags} -p ${ARG_PACKAGE_NAME}
        WORKING_DIRECTORY "${ARG_CARGO_DIR}"
        COMMENT "Building Rust crate: ${ARG_CRATE_NAME} (${_rust_profile})"
        VERBATIM
    )

    add_custom_target(${ARG_TARGET}_build
        DEPENDS "${RUST_LIB_PATH}"
    )

    if(WIN32)
        # On Windows, cdylib produces a .dll + .dll.lib import library.
        # Use SHARED IMPORTED with IMPORTED_IMPLIB for the import library.
        set(RUST_DLL_NAME "${ARG_CRATE_NAME}.dll")
        set(RUST_DLL_PATH "${RUST_OUTPUT_DIR}/${RUST_DLL_NAME}")

        add_library(${ARG_TARGET} SHARED IMPORTED GLOBAL)
        set_target_properties(${ARG_TARGET} PROPERTIES
            IMPORTED_LOCATION "${RUST_DLL_PATH}"
            IMPORTED_IMPLIB   "${RUST_LIB_PATH}"
        )
    else()
        # On Unix, cdylib produces a .so / .dylib.
        add_library(${ARG_TARGET} SHARED IMPORTED GLOBAL)
        set_target_properties(${ARG_TARGET} PROPERTIES
            IMPORTED_LOCATION "${RUST_LIB_PATH}"
        )
    endif()
    add_dependencies(${ARG_TARGET} ${ARG_TARGET}_build)

    # For multi-config generators, set per-configuration import locations so
    # that Debug configs use the debug Rust build and Release configs use release.
    get_property(_is_multi_config GLOBAL PROPERTY GENERATOR_IS_MULTI_CONFIG)
    if(_is_multi_config)
        set(_release_dir "${ARG_CARGO_DIR}/target/release")
        set(_debug_dir   "${ARG_CARGO_DIR}/target/debug")
        if(WIN32)
            set_target_properties(${ARG_TARGET} PROPERTIES
                IMPORTED_LOCATION_DEBUG           "${_debug_dir}/${RUST_DLL_NAME}"
                IMPORTED_IMPLIB_DEBUG             "${_debug_dir}/${RUST_LIB_NAME}"
                IMPORTED_LOCATION_RELEASE         "${_release_dir}/${RUST_DLL_NAME}"
                IMPORTED_IMPLIB_RELEASE           "${_release_dir}/${RUST_LIB_NAME}"
                IMPORTED_LOCATION_RELWITHDEBINFO  "${_release_dir}/${RUST_DLL_NAME}"
                IMPORTED_IMPLIB_RELWITHDEBINFO    "${_release_dir}/${RUST_LIB_NAME}"
                IMPORTED_LOCATION_MINSIZEREL      "${_release_dir}/${RUST_DLL_NAME}"
                IMPORTED_IMPLIB_MINSIZEREL        "${_release_dir}/${RUST_LIB_NAME}"
            )
        else()
            set_target_properties(${ARG_TARGET} PROPERTIES
                IMPORTED_LOCATION_DEBUG   "${_debug_dir}/${RUST_LIB_NAME}"
                IMPORTED_LOCATION_RELEASE "${_release_dir}/${RUST_LIB_NAME}"
                IMPORTED_LOCATION_RELWITHDEBINFO "${_release_dir}/${RUST_LIB_NAME}"
                IMPORTED_LOCATION_MINSIZEREL     "${_release_dir}/${RUST_LIB_NAME}"
            )
        endif()
    endif()

    # Platform-specific link dependencies for the Rust runtime
    if(WIN32)
        set_property(TARGET ${ARG_TARGET} APPEND PROPERTY
            INTERFACE_LINK_LIBRARIES ws2_32 userenv ntdll bcrypt advapi32
        )
    elseif(APPLE)
        set_property(TARGET ${ARG_TARGET} APPEND PROPERTY
            INTERFACE_LINK_LIBRARIES "-framework Security" "-framework CoreFoundation" pthread dl m
        )
    else()
        set_property(TARGET ${ARG_TARGET} APPEND PROPERTY
            INTERFACE_LINK_LIBRARIES pthread dl m rt
        )
    endif()

    message(STATUS "Temporalio: Rust bridge target '${ARG_TARGET}' -> ${RUST_LIB_PATH}")
endfunction()
