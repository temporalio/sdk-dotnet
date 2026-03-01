# Overlay port for local development.
# Usage: vcpkg install temporalio --overlay-ports=./vcpkg-overlay

# Use the local source checkout instead of fetching from GitHub.
# The overlay port directory is at <repo>/vcpkg-overlay/temporalio/,
# so the repo root is two levels up.
get_filename_component(SOURCE_PATH "${CMAKE_CURRENT_LIST_DIR}/../.." ABSOLUTE)

# Cargo must be available on the build machine for the Rust sdk-core bridge.
# vcpkg does not provide a CARGO helper, so we rely on the system installation.
find_program(CARGO cargo REQUIRED)
message(STATUS "Found cargo: ${CARGO}")

# Set PROTOC for the Rust build (protobuf compiler needed by prost/tonic).
find_program(PROTOC_PROGRAM protoc PATHS "${CURRENT_HOST_INSTALLED_DIR}/tools/protobuf" NO_DEFAULT_PATH)
if(PROTOC_PROGRAM)
    set(ENV{PROTOC} "${PROTOC_PROGRAM}")
endif()

# Set PROTOC_INCLUDE for well-known type .proto imports.
set(_protoc_include "${CURRENT_HOST_INSTALLED_DIR}/include")
if(EXISTS "${_protoc_include}/google/protobuf/any.proto")
    set(ENV{PROTOC_INCLUDE} "${_protoc_include}")
endif()

# Determine extension build options based on features.
set(EXTENSIONS_OPTION OFF)
if("opentelemetry" IN_LIST FEATURES OR "diagnostics" IN_LIST FEATURES)
    set(EXTENSIONS_OPTION ON)
endif()

# The CMake build system handles the Rust bridge build automatically via
# temporalio_build_rust_bridge() in Platform.cmake, which invokes cargo as
# a custom command. We pass CARGO as CARGO_EXECUTABLE so CMake can find it.
vcpkg_cmake_configure(
    SOURCE_PATH "${SOURCE_PATH}/cpp"
    OPTIONS
        -DTEMPORALIO_BUILD_TESTS=OFF
        -DTEMPORALIO_BUILD_EXAMPLES=OFF
        -DTEMPORALIO_BUILD_EXTENSIONS=${EXTENSIONS_OPTION}
        -DCARGO_EXECUTABLE=${CARGO}
)

vcpkg_cmake_install()
vcpkg_cmake_config_fixup(PACKAGE_NAME temporalio CONFIG_PATH lib/cmake/temporalio)
vcpkg_fixup_pkgconfig()
vcpkg_copy_pdbs()

# Remove duplicate include directory from debug install.
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/include")
file(REMOVE_RECURSE "${CURRENT_PACKAGES_DIR}/debug/share")

vcpkg_install_copyright(FILE_LIST "${SOURCE_PATH}/LICENSE")
