# ProtobufGenerate.cmake - Protobuf C++ code generation for Temporal API protos
#
# Provides the function:
#   temporalio_generate_protos(
#       TARGET <library-target-name>
#       PROTO_ROOT <path-to-sdk-core-protos-dir>
#   )
#
# This creates a STATIC library target containing all generated .pb.cc files
# and exposes the generated headers via its PUBLIC include path.

# Note: Protobuf is found in the main CMakeLists.txt before this file is included.
# We do not call find_package here to avoid conflicting with FetchContent fallback.

# Determine protoc executable. There are three scenarios:
# 1. System/vcpkg protobuf: protobuf::protoc is an IMPORTED target with IMPORTED_LOCATION
# 2. FetchContent protobuf: protoc is a build target, use $<TARGET_FILE:protoc>
# 3. No protobuf found: fall back to find_program(protoc)
set(_TEMPORALIO_PROTOC_FOUND FALSE)

if(TARGET protobuf::protoc)
    # Check if this is an imported target (system/vcpkg install)
    get_target_property(_protoc_imported protobuf::protoc IMPORTED)
    if(_protoc_imported)
        get_target_property(PROTOC_EXECUTABLE protobuf::protoc IMPORTED_LOCATION)
        if(NOT PROTOC_EXECUTABLE)
            get_target_property(PROTOC_EXECUTABLE protobuf::protoc IMPORTED_LOCATION_RELEASE)
        endif()
        if(NOT PROTOC_EXECUTABLE)
            get_target_property(PROTOC_EXECUTABLE protobuf::protoc IMPORTED_LOCATION_DEBUG)
        endif()
        if(PROTOC_EXECUTABLE)
            set(_TEMPORALIO_PROTOC_FOUND TRUE)
            set(_TEMPORALIO_PROTOC_IS_TARGET FALSE)
            message(STATUS "Temporalio: Found protoc (imported): ${PROTOC_EXECUTABLE}")
        endif()
    else()
        # FetchContent: protobuf::protoc is an ALIAS for the 'protoc' build target.
        # We cannot get IMPORTED_LOCATION from it; use TARGET_FILE generator expression.
        set(_TEMPORALIO_PROTOC_FOUND TRUE)
        set(_TEMPORALIO_PROTOC_IS_TARGET TRUE)
        # For custom_command COMMAND, use the target name directly (CMake resolves it)
        set(PROTOC_EXECUTABLE $<TARGET_FILE:protoc>)
        message(STATUS "Temporalio: Using FetchContent-built protoc (build target)")
    endif()
endif()

if(NOT _TEMPORALIO_PROTOC_FOUND)
    find_program(PROTOC_EXECUTABLE protoc)
    if(PROTOC_EXECUTABLE)
        set(_TEMPORALIO_PROTOC_FOUND TRUE)
        set(_TEMPORALIO_PROTOC_IS_TARGET FALSE)
        message(STATUS "Temporalio: Found protoc (system): ${PROTOC_EXECUTABLE}")
    else()
        message(STATUS "Temporalio: protoc not found - proto generation will create placeholder targets")
    endif()
endif()

function(temporalio_generate_protos)
    cmake_parse_arguments(PARSE_ARGV 0 ARG "" "TARGET;PROTO_ROOT" "")

    if(NOT ARG_TARGET OR NOT ARG_PROTO_ROOT)
        message(FATAL_ERROR "temporalio_generate_protos requires TARGET and PROTO_ROOT")
    endif()

    set(GEN_DIR "${CMAKE_CURRENT_BINARY_DIR}/proto_gen")
    file(MAKE_DIRECTORY "${GEN_DIR}")

    # If protoc is not available, create a placeholder library
    if(NOT _TEMPORALIO_PROTOC_FOUND)
        message(STATUS "Temporalio: protoc not available - creating placeholder proto target '${ARG_TARGET}'")
        file(WRITE "${GEN_DIR}/empty_proto.cpp"
            "// Placeholder - protoc not available for code generation\n")
        add_library(${ARG_TARGET} STATIC "${GEN_DIR}/empty_proto.cpp")
        target_include_directories(${ARG_TARGET} PUBLIC $<BUILD_INTERFACE:${GEN_DIR}>)
        if(TARGET protobuf::libprotobuf)
            target_link_libraries(${ARG_TARGET} PUBLIC protobuf::libprotobuf)
        endif()
        if(MSVC)
            target_compile_options(${ARG_TARGET} PRIVATE /W0)
        else()
            target_compile_options(${ARG_TARGET} PRIVATE -w)
        endif()
        target_compile_features(${ARG_TARGET} PUBLIC cxx_std_17)
        set(TEMPORALIO_PROTO_GEN_DIR "${GEN_DIR}" PARENT_SCOPE)
        return()
    endif()

    set(PROTO_ROOT "${ARG_PROTO_ROOT}")

    # Proto import directories - order matters for resolution
    set(API_UPSTREAM_DIR "${PROTO_ROOT}/api_upstream")
    set(API_CLOUD_DIR "${PROTO_ROOT}/api_cloud_upstream")
    set(LOCAL_DIR "${PROTO_ROOT}/local")
    set(TESTSRV_DIR "${PROTO_ROOT}/testsrv_upstream")
    set(GOOGLE_RPC_DIR "${PROTO_ROOT}")
    set(GRPC_DIR "${PROTO_ROOT}")

    # Collect all .proto files by category
    # 1. API upstream (temporal/api/...) - excluding google well-known types
    #    (we use the protobuf library's built-in well-known types instead)
    file(GLOB_RECURSE API_PROTOS "${API_UPSTREAM_DIR}/temporal/*.proto")

    # 2. Google API protos (google/api/annotations.proto, google/api/http.proto)
    file(GLOB_RECURSE GOOGLE_API_PROTOS "${API_UPSTREAM_DIR}/google/api/*.proto")

    # 3. API Cloud upstream
    file(GLOB_RECURSE API_CLOUD_PROTOS "${API_CLOUD_DIR}/*.proto")

    # 4. Local/coresdk protos
    file(GLOB_RECURSE LOCAL_PROTOS "${LOCAL_DIR}/*.proto")

    # 5. Test service protos
    file(GLOB_RECURSE TESTSRV_PROTOS "${TESTSRV_DIR}/*.proto")

    # 6. Google RPC status proto
    file(GLOB_RECURSE GOOGLE_RPC_PROTOS "${GOOGLE_RPC_DIR}/google/rpc/*.proto")

    # 7. gRPC health proto
    file(GLOB_RECURSE GRPC_HEALTH_PROTOS "${GRPC_DIR}/grpc/*.proto")

    # Combine all proto files (skip google/protobuf/* - use system ones)
    set(ALL_PROTOS
        ${GOOGLE_API_PROTOS}
        ${GOOGLE_RPC_PROTOS}
        ${GRPC_HEALTH_PROTOS}
        ${API_PROTOS}
        ${API_CLOUD_PROTOS}
        ${LOCAL_PROTOS}
        ${TESTSRV_PROTOS}
    )

    if(NOT ALL_PROTOS)
        message(WARNING "Temporalio: No .proto files found under ${PROTO_ROOT}")
        # Create an empty placeholder library
        file(WRITE "${GEN_DIR}/empty_proto.cpp"
            "// No proto files found - placeholder\n")
        add_library(${ARG_TARGET} STATIC "${GEN_DIR}/empty_proto.cpp")
        return()
    endif()

    list(LENGTH ALL_PROTOS _proto_count)
    message(STATUS "Temporalio: Found ${_proto_count} .proto files to generate")

    # Build the protoc include path list
    # Each directory is an import root for its contained proto files.
    # - api_upstream: temporal/api/* and google/api/* imports
    # - api_cloud_upstream: temporal/api/cloud/* imports
    # - local: temporal/sdk/core/* imports
    # - testsrv_upstream: temporal/api/testservice/* imports
    # - PROTO_ROOT: google/rpc/* and grpc/* imports
    set(PROTO_INCLUDE_DIRS
        "${API_UPSTREAM_DIR}"
        "${API_CLOUD_DIR}"
        "${LOCAL_DIR}"
        "${TESTSRV_DIR}"
        "${GOOGLE_RPC_DIR}"
        "${GRPC_DIR}"
    )

    # Add protobuf well-known types include path (google/protobuf/*.proto)
    # When using FetchContent, these are under _deps/protobuf-src/src/
    if(TARGET protobuf::libprotobuf)
        get_target_property(_proto_import_dirs protobuf::libprotobuf INTERFACE_INCLUDE_DIRECTORIES)
        if(_proto_import_dirs)
            foreach(_dir IN LISTS _proto_import_dirs)
                # The protobuf src directory contains google/protobuf/*.proto
                if(EXISTS "${_dir}/google/protobuf/any.proto")
                    list(APPEND PROTO_INCLUDE_DIRS "${_dir}")
                endif()
            endforeach()
        endif()
    endif()
    # Fallback: check FetchContent protobuf source directory directly
    if(FETCHCONTENT_BASE_DIR)
        set(_pb_src "${FETCHCONTENT_BASE_DIR}/protobuf-src/src")
    else()
        set(_pb_src "${CMAKE_BINARY_DIR}/_deps/protobuf-src/src")
    endif()
    if(EXISTS "${_pb_src}/google/protobuf/any.proto")
        list(APPEND PROTO_INCLUDE_DIRS "${_pb_src}")
    endif()

    # Additional fallback: use protobuf_SOURCE_DIR (set by FetchContent)
    if(protobuf_SOURCE_DIR)
        set(_pb_fc_src "${protobuf_SOURCE_DIR}/src")
        if(EXISTS "${_pb_fc_src}/google/protobuf/any.proto")
            list(APPEND PROTO_INCLUDE_DIRS "${_pb_fc_src}")
        endif()
    endif()

    list(REMOVE_DUPLICATES PROTO_INCLUDE_DIRS)
    message(STATUS "Temporalio: Proto include dirs: ${PROTO_INCLUDE_DIRS}")

    # Build protoc -I flags
    set(PROTO_INCLUDE_FLAGS)
    foreach(_inc IN LISTS PROTO_INCLUDE_DIRS)
        list(APPEND PROTO_INCLUDE_FLAGS "-I${_inc}")
    endforeach()

    # For each proto file, determine the output .pb.h and .pb.cc paths
    set(GEN_SOURCES)
    set(GEN_HEADERS)

    # Build dependency list for custom commands
    set(_protoc_depends)
    if(_TEMPORALIO_PROTOC_IS_TARGET)
        # When protoc is a build target, custom commands must depend on it
        # so that it is built before proto generation runs
        list(APPEND _protoc_depends protoc)
    endif()

    foreach(_proto IN LISTS ALL_PROTOS)
        # Determine which include root this proto belongs to
        set(_found_root "")
        foreach(_inc IN LISTS PROTO_INCLUDE_DIRS)
            file(RELATIVE_PATH _rel "${_inc}" "${_proto}")
            string(SUBSTRING "${_rel}" 0 2 _prefix)
            if(NOT _prefix STREQUAL "..")
                set(_found_root "${_inc}")
                break()
            endif()
        endforeach()

        if(NOT _found_root)
            message(WARNING "Temporalio: Could not determine include root for ${_proto}")
            continue()
        endif()

        file(RELATIVE_PATH _rel_path "${_found_root}" "${_proto}")
        # Convert .proto -> .pb.cc and .pb.h
        string(REGEX REPLACE "\\.proto$" ".pb.cc" _gen_cc "${_rel_path}")
        string(REGEX REPLACE "\\.proto$" ".pb.h" _gen_h "${_rel_path}")

        set(_out_cc "${GEN_DIR}/${_gen_cc}")
        set(_out_h "${GEN_DIR}/${_gen_h}")

        list(APPEND GEN_SOURCES "${_out_cc}")
        list(APPEND GEN_HEADERS "${_out_h}")

        # Add a custom command to generate this proto
        get_filename_component(_out_dir "${_out_cc}" DIRECTORY)
        add_custom_command(
            OUTPUT "${_out_cc}" "${_out_h}"
            COMMAND ${CMAKE_COMMAND} -E make_directory "${_out_dir}"
            COMMAND ${PROTOC_EXECUTABLE}
                ${PROTO_INCLUDE_FLAGS}
                "--cpp_out=${GEN_DIR}"
                "${_proto}"
            DEPENDS "${_proto}" ${_protoc_depends}
            COMMENT "Generating C++ protobuf for ${_rel_path}"
            VERBATIM
        )
    endforeach()

    # Create the library target with all generated sources
    add_library(${ARG_TARGET} STATIC ${GEN_SOURCES})

    target_include_directories(${ARG_TARGET}
        SYSTEM PUBLIC
            $<BUILD_INTERFACE:${GEN_DIR}>
            $<INSTALL_INTERFACE:include>
    )

    if(TEMPORALIO_PROTOBUF_TARGET)
        target_link_libraries(${ARG_TARGET}
            PUBLIC
                ${TEMPORALIO_PROTOBUF_TARGET}
        )
    elseif(TARGET protobuf::libprotobuf)
        target_link_libraries(${ARG_TARGET}
            PUBLIC
                protobuf::libprotobuf
        )
    endif()

    # Suppress warnings in generated code
    if(MSVC)
        target_compile_options(${ARG_TARGET} PRIVATE /W0)
    else()
        target_compile_options(${ARG_TARGET} PRIVATE -w)
    endif()

    target_compile_features(${ARG_TARGET} PUBLIC cxx_std_17)

    # Export the generated directory for other targets to find headers
    set(TEMPORALIO_PROTO_GEN_DIR "${GEN_DIR}" PARENT_SCOPE)

    message(STATUS "Temporalio: Proto generation target '${ARG_TARGET}' configured with ${_proto_count} protos")
    message(STATUS "Temporalio: Generated files will be placed in ${GEN_DIR}")
endfunction()
