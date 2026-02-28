#include <gtest/gtest.h>

#include <atomic>
#include <stop_token>
#include <thread>
#include <vector>

#include "temporalio/coro/cancellation_token.h"

using namespace temporalio::coro;

// ===========================================================================
// Basic CancellationTokenSource functionality
// ===========================================================================

TEST(CancellationTokenSourceTest, InitiallyNotCancelled) {
    CancellationTokenSource cts;
    EXPECT_FALSE(cts.is_cancellation_requested());
}

TEST(CancellationTokenSourceTest, CancelSetsFlag) {
    CancellationTokenSource cts;
    cts.cancel();
    EXPECT_TRUE(cts.is_cancellation_requested());
}

TEST(CancellationTokenSourceTest, CancelIsIdempotent) {
    CancellationTokenSource cts;
    cts.cancel();
    cts.cancel();  // Second call should be fine
    EXPECT_TRUE(cts.is_cancellation_requested());
}

// ===========================================================================
// Token usage
// ===========================================================================

TEST(CancellationTokenSourceTest, TokenReflectsCancellation) {
    CancellationTokenSource cts;
    auto token = cts.token();

    EXPECT_FALSE(token.stop_requested());
    cts.cancel();
    EXPECT_TRUE(token.stop_requested());
}

TEST(CancellationTokenSourceTest, TokenIsStoppable) {
    CancellationTokenSource cts;
    auto token = cts.token();
    EXPECT_TRUE(token.stop_possible());
}

TEST(CancellationTokenSourceTest, MultipleTokensFromSameSource) {
    CancellationTokenSource cts;
    auto token1 = cts.token();
    auto token2 = cts.token();

    EXPECT_FALSE(token1.stop_requested());
    EXPECT_FALSE(token2.stop_requested());

    cts.cancel();

    EXPECT_TRUE(token1.stop_requested());
    EXPECT_TRUE(token2.stop_requested());
}

// ===========================================================================
// stop_callback integration
// ===========================================================================

TEST(CancellationTokenSourceTest, StopCallbackInvoked) {
    CancellationTokenSource cts;
    auto token = cts.token();

    bool callback_invoked = false;
    std::stop_callback callback(token, [&]() { callback_invoked = true; });

    EXPECT_FALSE(callback_invoked);
    cts.cancel();
    EXPECT_TRUE(callback_invoked);
}

TEST(CancellationTokenSourceTest, StopCallbackRegisteredAfterCancel) {
    CancellationTokenSource cts;
    cts.cancel();

    auto token = cts.token();
    bool callback_invoked = false;
    std::stop_callback callback(token, [&]() { callback_invoked = true; });

    // Callback should be invoked immediately since already cancelled
    EXPECT_TRUE(callback_invoked);
}

TEST(CancellationTokenSourceTest, MultipleCallbacks) {
    CancellationTokenSource cts;
    auto token = cts.token();

    int count = 0;
    std::stop_callback cb1(token, [&]() { ++count; });
    std::stop_callback cb2(token, [&]() { ++count; });
    std::stop_callback cb3(token, [&]() { ++count; });

    cts.cancel();
    EXPECT_EQ(count, 3);
}

// ===========================================================================
// Move semantics
// ===========================================================================

TEST(CancellationTokenSourceTest, MoveConstruction) {
    CancellationTokenSource cts1;
    auto token = cts1.token();

    CancellationTokenSource cts2 = std::move(cts1);

    // The moved-to source should control the original token
    cts2.cancel();
    EXPECT_TRUE(token.stop_requested());
}

TEST(CancellationTokenSourceTest, MoveAssignment) {
    CancellationTokenSource cts1;
    auto token1 = cts1.token();

    CancellationTokenSource cts2;

    cts2 = std::move(cts1);
    cts2.cancel();
    EXPECT_TRUE(token1.stop_requested());
}

// ===========================================================================
// Thread safety
// ===========================================================================

TEST(CancellationTokenSourceTest, ConcurrentCancelAndCheck) {
    CancellationTokenSource cts;
    auto token = cts.token();

    std::atomic<bool> ready{false};
    std::atomic<int> observed_cancellation{0};

    constexpr int num_threads = 8;
    std::vector<std::thread> threads;
    threads.reserve(num_threads);

    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([&]() {
            while (!ready.load(std::memory_order_acquire)) {
                // spin
            }
            if (token.stop_requested()) {
                observed_cancellation.fetch_add(1, std::memory_order_relaxed);
            }
        });
    }

    cts.cancel();
    ready.store(true, std::memory_order_release);

    for (auto& t : threads) {
        t.join();
    }

    // All threads should observe cancellation
    EXPECT_EQ(observed_cancellation.load(), num_threads);
}

TEST(CancellationTokenSourceTest, ConcurrentCancelFromMultipleThreads) {
    CancellationTokenSource cts;
    auto token = cts.token();

    constexpr int num_threads = 8;
    std::vector<std::thread> threads;
    threads.reserve(num_threads);

    std::atomic<bool> go{false};

    for (int i = 0; i < num_threads; ++i) {
        threads.emplace_back([&]() {
            while (!go.load(std::memory_order_acquire)) {
                // spin
            }
            cts.cancel();  // Multiple threads calling cancel concurrently
        });
    }

    go.store(true, std::memory_order_release);

    for (auto& t : threads) {
        t.join();
    }

    EXPECT_TRUE(cts.is_cancellation_requested());
    EXPECT_TRUE(token.stop_requested());
}

// ===========================================================================
// Non-copyable
// ===========================================================================

TEST(CancellationTokenSourceTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<CancellationTokenSource>);
    EXPECT_FALSE(std::is_copy_assignable_v<CancellationTokenSource>);
}

TEST(CancellationTokenSourceTest, IsMovable) {
    EXPECT_TRUE(std::is_move_constructible_v<CancellationTokenSource>);
    EXPECT_TRUE(std::is_move_assignable_v<CancellationTokenSource>);
}
