#pragma once

/// @file version.h
/// @brief Library version information.

#include <temporalio/export.h>

#define TEMPORALIO_VERSION_MAJOR 0
#define TEMPORALIO_VERSION_MINOR 1
#define TEMPORALIO_VERSION_PATCH 0
#define TEMPORALIO_VERSION_STRING "0.1.0"

namespace temporalio {

/// Returns the library version string.
TEMPORALIO_EXPORT const char* version() noexcept;

} // namespace temporalio
