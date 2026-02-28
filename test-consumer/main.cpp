// Minimal consumer integration test for the Temporal C++ SDK.
// Verifies that find_package(temporalio) works and headers are includable.
//
// Build:
//   cmake -B build -DCMAKE_PREFIX_PATH=<install-prefix>
//   cmake --build build
//   ./build/consumer_test

#include <temporalio/version.h>

#include <cstdlib>
#include <cstring>
#include <iostream>

int main() {
    // Verify the version string is available and non-empty.
    const char* ver = temporalio::version();
    if (ver == nullptr || std::strlen(ver) == 0) {
        std::cerr << "FAIL: temporalio::version() returned null or empty\n";
        return EXIT_FAILURE;
    }

    // Verify it matches the expected version.
    if (std::strcmp(ver, TEMPORALIO_VERSION_STRING) != 0) {
        std::cerr << "FAIL: version mismatch: got \"" << ver
                  << "\", expected \"" << TEMPORALIO_VERSION_STRING << "\"\n";
        return EXIT_FAILURE;
    }

    std::cout << "OK: Temporal C++ SDK version " << ver << "\n";
    return EXIT_SUCCESS;
}
