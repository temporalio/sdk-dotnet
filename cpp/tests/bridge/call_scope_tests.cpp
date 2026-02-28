#include <gtest/gtest.h>

#include <cstdint>
#include <cstring>
#include <span>
#include <string>
#include <string_view>
#include <vector>

// Include the interop types (struct definitions only, no FFI calls needed)
#include "temporalio/bridge/interop.h"
#include "temporalio/bridge/byte_array.h"
#include "temporalio/bridge/call_scope.h"

using namespace temporalio::bridge;

// ===========================================================================
// CallScope byte_array from string_view
// ===========================================================================

TEST(CallScopeTest, ByteArrayFromStringView) {
    CallScope scope;
    auto ref = scope.byte_array("hello");

    ASSERT_NE(ref.data, nullptr);
    EXPECT_EQ(ref.size, 5u);
    EXPECT_EQ(std::string_view(reinterpret_cast<const char*>(ref.data), ref.size),
              "hello");
}

TEST(CallScopeTest, ByteArrayFromEmptyStringView) {
    CallScope scope;
    auto ref = scope.byte_array("");

    // empty_byte_array_ref() returns a non-null sentinel pointer
    // because Rust's slice::from_raw_parts requires non-null even for size 0
    EXPECT_NE(ref.data, nullptr);
    EXPECT_EQ(ref.size, 0u);
}

TEST(CallScopeTest, ByteArrayOwnsData) {
    CallScope scope;
    std::string temp = "temporary";
    auto ref = scope.byte_array(std::string_view(temp));

    // Modify the original - the ref should still point to the scope's copy
    temp = "modified!";

    EXPECT_EQ(std::string_view(reinterpret_cast<const char*>(ref.data), ref.size),
              "temporary");
}

TEST(CallScopeTest, MultipleByteArrays) {
    CallScope scope;
    auto ref1 = scope.byte_array("first");
    auto ref2 = scope.byte_array("second");
    auto ref3 = scope.byte_array("third");

    EXPECT_EQ(std::string_view(reinterpret_cast<const char*>(ref1.data), ref1.size),
              "first");
    EXPECT_EQ(std::string_view(reinterpret_cast<const char*>(ref2.data), ref2.size),
              "second");
    EXPECT_EQ(std::string_view(reinterpret_cast<const char*>(ref3.data), ref3.size),
              "third");
}

// ===========================================================================
// CallScope byte_array from byte span
// ===========================================================================

TEST(CallScopeTest, ByteArrayFromByteSpan) {
    CallScope scope;
    std::vector<uint8_t> bytes = {0x01, 0x02, 0x03, 0x04};
    auto ref = scope.byte_array(std::span<const uint8_t>(bytes));

    ASSERT_NE(ref.data, nullptr);
    EXPECT_EQ(ref.size, 4u);
    EXPECT_EQ(ref.data[0], 0x01);
    EXPECT_EQ(ref.data[1], 0x02);
    EXPECT_EQ(ref.data[2], 0x03);
    EXPECT_EQ(ref.data[3], 0x04);
}

TEST(CallScopeTest, ByteArrayFromEmptyByteSpan) {
    CallScope scope;
    std::span<const uint8_t> empty;
    auto ref = scope.byte_array(empty);

    EXPECT_NE(ref.data, nullptr);
    EXPECT_EQ(ref.size, 0u);
}

// ===========================================================================
// CallScope byte_array from vector
// ===========================================================================

TEST(CallScopeTest, ByteArrayFromByteVector) {
    CallScope scope;
    std::vector<uint8_t> bytes = {0xAA, 0xBB, 0xCC};
    auto ref = scope.byte_array(bytes);

    ASSERT_NE(ref.data, nullptr);
    EXPECT_EQ(ref.size, 3u);
    EXPECT_EQ(ref.data[0], 0xAA);
}

// ===========================================================================
// CallScope byte_array_kv
// ===========================================================================

TEST(CallScopeTest, ByteArrayKV) {
    CallScope scope;
    auto ref = scope.byte_array_kv("key", "value");

    ASSERT_NE(ref.data, nullptr);
    auto sv =
        std::string_view(reinterpret_cast<const char*>(ref.data), ref.size);
    EXPECT_EQ(sv, "key\nvalue");
}

TEST(CallScopeTest, ByteArrayKVEmptyValue) {
    CallScope scope;
    auto ref = scope.byte_array_kv("key", "");

    auto sv =
        std::string_view(reinterpret_cast<const char*>(ref.data), ref.size);
    EXPECT_EQ(sv, "key\n");
}

// ===========================================================================
// CallScope newline_delimited
// ===========================================================================

TEST(CallScopeTest, NewlineDelimited) {
    CallScope scope;
    auto ref =
        scope.newline_delimited({"alpha", "beta", "gamma"});

    ASSERT_NE(ref.data, nullptr);
    auto sv =
        std::string_view(reinterpret_cast<const char*>(ref.data), ref.size);
    EXPECT_EQ(sv, "alpha\nbeta\ngamma");
}

TEST(CallScopeTest, NewlineDelimitedSingleElement) {
    CallScope scope;
    auto ref = scope.newline_delimited({"only"});

    auto sv =
        std::string_view(reinterpret_cast<const char*>(ref.data), ref.size);
    EXPECT_EQ(sv, "only");
}

TEST(CallScopeTest, NewlineDelimitedEmpty) {
    CallScope scope;
    auto ref = scope.newline_delimited({});

    EXPECT_NE(ref.data, nullptr);
    EXPECT_EQ(ref.size, 0u);
}

// ===========================================================================
// CallScope byte_array_array
// ===========================================================================

TEST(CallScopeTest, ByteArrayArray) {
    CallScope scope;
    auto arr = scope.byte_array_array({"one", "two", "three"});

    ASSERT_NE(arr.data, nullptr);
    EXPECT_EQ(arr.size, 3u);

    auto sv0 = std::string_view(
        reinterpret_cast<const char*>(arr.data[0].data), arr.data[0].size);
    auto sv1 = std::string_view(
        reinterpret_cast<const char*>(arr.data[1].data), arr.data[1].size);
    auto sv2 = std::string_view(
        reinterpret_cast<const char*>(arr.data[2].data), arr.data[2].size);

    EXPECT_EQ(sv0, "one");
    EXPECT_EQ(sv1, "two");
    EXPECT_EQ(sv2, "three");
}

TEST(CallScopeTest, ByteArrayArrayEmpty) {
    CallScope scope;
    auto arr = scope.byte_array_array({});

    EXPECT_NE(arr.data, nullptr);
    EXPECT_EQ(arr.size, 0u);
}

// ===========================================================================
// CallScope byte_array_array_kv
// ===========================================================================

TEST(CallScopeTest, ByteArrayArrayKV) {
    CallScope scope;
    auto arr = scope.byte_array_array_kv({{"k1", "v1"}, {"k2", "v2"}});

    ASSERT_NE(arr.data, nullptr);
    EXPECT_EQ(arr.size, 2u);

    auto sv0 = std::string_view(
        reinterpret_cast<const char*>(arr.data[0].data), arr.data[0].size);
    auto sv1 = std::string_view(
        reinterpret_cast<const char*>(arr.data[1].data), arr.data[1].size);

    EXPECT_EQ(sv0, "k1\nv1");
    EXPECT_EQ(sv1, "k2\nv2");
}

// ===========================================================================
// CallScope alloc
// ===========================================================================

TEST(CallScopeTest, AllocInt) {
    CallScope scope;
    int* p = scope.alloc(42);

    ASSERT_NE(p, nullptr);
    EXPECT_EQ(*p, 42);
    // Memory is freed when scope goes out of scope
}

TEST(CallScopeTest, AllocStruct) {
    struct TestStruct {
        int x;
        double y;
    };

    CallScope scope;
    auto* p = scope.alloc(TestStruct{10, 3.14});

    ASSERT_NE(p, nullptr);
    EXPECT_EQ(p->x, 10);
    EXPECT_DOUBLE_EQ(p->y, 3.14);
}

TEST(CallScopeTest, AllocMultiple) {
    CallScope scope;
    int* p1 = scope.alloc(1);
    int* p2 = scope.alloc(2);
    int* p3 = scope.alloc(3);

    EXPECT_EQ(*p1, 1);
    EXPECT_EQ(*p2, 2);
    EXPECT_EQ(*p3, 3);
}

// ===========================================================================
// Static helpers
// ===========================================================================

TEST(CallScopeTest, EmptyByteArrayRef) {
    auto ref = CallScope::empty_byte_array_ref();
    // Sentinel non-null pointer required by Rust's slice::from_raw_parts
    EXPECT_NE(ref.data, nullptr);
    EXPECT_EQ(ref.size, 0u);
}

TEST(CallScopeTest, EmptyByteArrayRefArray) {
    auto arr = CallScope::empty_byte_array_ref_array();
    EXPECT_NE(arr.data, nullptr);
    EXPECT_EQ(arr.size, 0u);
}

// ===========================================================================
// Non-copyable, non-movable
// ===========================================================================

TEST(CallScopeTest, IsNonCopyable) {
    EXPECT_FALSE(std::is_copy_constructible_v<CallScope>);
    EXPECT_FALSE(std::is_copy_assignable_v<CallScope>);
}

TEST(CallScopeTest, IsNonMovable) {
    EXPECT_FALSE(std::is_move_constructible_v<CallScope>);
    EXPECT_FALSE(std::is_move_assignable_v<CallScope>);
}

// ===========================================================================
// Byte array helper functions
// ===========================================================================

TEST(ByteArrayHelpersTest, ByteArrayRefToStringView) {
    const char* data = "test string";
    TemporalCoreByteArrayRef ref{
        reinterpret_cast<const uint8_t*>(data),
        std::strlen(data),
    };

    auto sv = byte_array_ref_to_string_view(ref);
    EXPECT_EQ(sv, "test string");
}

TEST(ByteArrayHelpersTest, ByteArrayRefToStringViewNull) {
    TemporalCoreByteArrayRef ref{nullptr, 0};
    auto sv = byte_array_ref_to_string_view(ref);
    EXPECT_TRUE(sv.empty());
}

TEST(ByteArrayHelpersTest, ByteArrayRefToString) {
    const char* data = "hello";
    TemporalCoreByteArrayRef ref{
        reinterpret_cast<const uint8_t*>(data),
        std::strlen(data),
    };

    auto str = byte_array_ref_to_string(ref);
    EXPECT_EQ(str, "hello");
}
