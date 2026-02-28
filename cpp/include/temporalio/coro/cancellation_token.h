#pragma once

/// @file Cancellation token wrapping C++20 std::stop_token.

#include <stop_token>

namespace temporalio::coro {

/// Wraps std::stop_source / std::stop_token to provide a familiar
/// CancellationTokenSource API matching the C# pattern.
///
/// Thread-safe: cancel() can be called from any thread, and
/// is_cancellation_requested() / token() are safe to call concurrently.
class CancellationTokenSource {
public:
    CancellationTokenSource() = default;

    /// Returns the stop_token associated with this source.
    /// Use this to check cancellation or register callbacks.
    std::stop_token token() const noexcept { return source_.get_token(); }

    /// Request cancellation. Thread-safe, idempotent.
    void cancel() noexcept { source_.request_stop(); }

    /// Check if cancellation has been requested.
    bool is_cancellation_requested() const noexcept {
        return source_.stop_requested();
    }

    // Non-copyable, movable
    CancellationTokenSource(const CancellationTokenSource&) = delete;
    CancellationTokenSource& operator=(const CancellationTokenSource&) =
        delete;
    CancellationTokenSource(CancellationTokenSource&&) noexcept = default;
    CancellationTokenSource& operator=(CancellationTokenSource&&) noexcept =
        default;

private:
    std::stop_source source_;
};

}  // namespace temporalio::coro

