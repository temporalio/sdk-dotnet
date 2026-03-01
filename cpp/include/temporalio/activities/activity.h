#pragma once

/// @file Activity definition and registration types.

#include <any>
#include <functional>
#include <memory>
#include <string>
#include <tuple>
#include <type_traits>
#include <utility>
#include <vector>

#include <temporalio/converters/data_converter.h>
#include <temporalio/coro/task.h>

namespace temporalio::activities {

namespace detail {

/// Helper: invoke a callable with arguments extracted from a vector<std::any>.
/// Uses decode_payload_value to properly decode converters::Payload args.
template <typename Tuple, std::size_t... I>
Tuple extract_args(const std::vector<std::any>& args,
                   std::index_sequence<I...>) {
    return Tuple{
        converters::decode_payload_value<std::tuple_element_t<I, Tuple>>(
            args.at(I))...};
}

/// Call a free function with arguments extracted from vector<std::any>.
template <typename R, typename... Args>
coro::Task<std::any> invoke_free(
    coro::Task<R> (*func)(Args...),
    std::vector<std::any> args) {
    using ArgTuple = std::tuple<std::decay_t<Args>...>;
    if constexpr (sizeof...(Args) == 0) {
        if constexpr (std::is_void_v<R>) {
            co_await func();
            co_return std::any{};
        } else {
            auto result = co_await func();
            co_return std::any(std::move(result));
        }
    } else {
        auto arg_tuple = extract_args<ArgTuple>(
            args, std::index_sequence_for<Args...>{});
        if constexpr (std::is_void_v<R>) {
            co_await std::apply(func, std::move(arg_tuple));
            co_return std::any{};
        } else {
            auto result = co_await std::apply(func, std::move(arg_tuple));
            co_return std::any(std::move(result));
        }
    }
}

/// Call a member function with arguments extracted from vector<std::any>.
template <typename T, typename R, typename... Args>
coro::Task<std::any> invoke_member(
    coro::Task<R> (T::*method)(Args...),
    T* instance,
    std::vector<std::any> args) {
    using ArgTuple = std::tuple<std::decay_t<Args>...>;
    if constexpr (sizeof...(Args) == 0) {
        if constexpr (std::is_void_v<R>) {
            co_await (instance->*method)();
            co_return std::any{};
        } else {
            auto result = co_await (instance->*method)();
            co_return std::any(std::move(result));
        }
    } else {
        auto arg_tuple = extract_args<ArgTuple>(
            args, std::index_sequence_for<Args...>{});
        auto bound = [method, instance](auto&&... a)
            -> coro::Task<R> {
            return (instance->*method)(
                std::forward<decltype(a)>(a)...);
        };
        if constexpr (std::is_void_v<R>) {
            co_await std::apply(bound, std::move(arg_tuple));
            co_return std::any{};
        } else {
            auto result =
                co_await std::apply(bound, std::move(arg_tuple));
            co_return std::any(std::move(result));
        }
    }
}

/// Trait: extract the value type T from coro::Task<T>.
template <typename TaskType>
struct task_value_type;

template <typename T>
struct task_value_type<coro::Task<T>> {
    using type = T;
};

template <typename TaskType>
using task_value_type_t = typename task_value_type<TaskType>::type;

/// Trait: deduce the call operator signature of a callable type.
/// Primary template (SFINAE fallback).
template <typename T, typename = void>
struct callable_traits;

/// Specialization for types with a non-overloaded operator().
template <typename T>
struct callable_traits<T, std::void_t<decltype(&T::operator())>>
    : callable_traits<decltype(&T::operator())> {};

/// Specialization for pointer-to-member-function (non-const).
template <typename C, typename R, typename... Args>
struct callable_traits<R (C::*)(Args...)> {
    using return_type = R;
    using arg_tuple = std::tuple<std::decay_t<Args>...>;
    static constexpr std::size_t arity = sizeof...(Args);
};

/// Specialization for pointer-to-member-function (const).
template <typename C, typename R, typename... Args>
struct callable_traits<R (C::*)(Args...) const> {
    using return_type = R;
    using arg_tuple = std::tuple<std::decay_t<Args>...>;
    static constexpr std::size_t arity = sizeof...(Args);
};

/// Call a callable with arguments extracted from vector<std::any>.
/// Uses decode_payload_value to properly decode converters::Payload args.
template <typename Callable, typename ArgTuple, std::size_t... I>
auto apply_callable(Callable& callable, const std::vector<std::any>& args,
                    std::index_sequence<I...>) {
    return callable(
        converters::decode_payload_value<std::tuple_element_t<I, ArgTuple>>(
            args.at(I))...);
}

}  // namespace detail

/// Definition of a registered activity including its name and executor.
///
/// Registration examples:
///   // Free function (0, 1, or multiple args)
///   auto def = ActivityDefinition::create("greet", &greet_function);
///   // Lambda (0, 1, or multiple args)
///   auto def = ActivityDefinition::create("add", [](int a, int b) -> Task<int> {
///       co_return a + b; });
///   // Member function + instance
///   auto def = ActivityDefinition::create("method", &MyClass::method, &obj);
class ActivityDefinition {
public:
    /// Create from a free function or static method returning Task<R>.
    /// Supports zero, one, or multiple arguments.
    template <typename R, typename... Args>
    static std::shared_ptr<ActivityDefinition> create(
        const std::string& name,
        coro::Task<R> (*func)(Args...)) {
        auto def = std::make_shared<ActivityDefinition>();
        def->name_ = name;
        def->executor_ = [func](std::vector<std::any> args)
            -> coro::Task<std::any> {
            co_return co_await detail::invoke_free(func, std::move(args));
        };
        return def;
    }

    /// Create from a callable (lambda, std::function, functor).
    /// Supports zero, one, or multiple arguments.
    template <typename Callable>
    static std::shared_ptr<ActivityDefinition> create(
        const std::string& name, Callable&& callable) {
        using Traits = detail::callable_traits<std::decay_t<Callable>>;
        using ArgTuple = typename Traits::arg_tuple;
        using ReturnTask = typename Traits::return_type;
        using ResultType = detail::task_value_type_t<ReturnTask>;

        auto def = std::make_shared<ActivityDefinition>();
        def->name_ = name;
        auto stored = std::make_shared<std::decay_t<Callable>>(
            std::forward<Callable>(callable));

        def->executor_ = [stored](std::vector<std::any> args)
            -> coro::Task<std::any> {
            constexpr auto arity = Traits::arity;
            if constexpr (arity == 0) {
                if constexpr (std::is_void_v<ResultType>) {
                    co_await (*stored)();
                    co_return std::any{};
                } else {
                    auto result = co_await (*stored)();
                    co_return std::any(std::move(result));
                }
            } else {
                if constexpr (std::is_void_v<ResultType>) {
                    co_await detail::apply_callable<
                        std::decay_t<Callable>, ArgTuple>(
                        *stored, args,
                        std::make_index_sequence<arity>{});
                    co_return std::any{};
                } else {
                    auto result = co_await detail::apply_callable<
                        std::decay_t<Callable>, ArgTuple>(
                        *stored, args,
                        std::make_index_sequence<arity>{});
                    co_return std::any(std::move(result));
                }
            }
        };
        return def;
    }

    /// Create from a member function + instance pointer.
    /// IMPORTANT: The caller must ensure that `instance` outlives the
    /// returned ActivityDefinition and any tasks it produces. The definition
    /// captures a raw pointer; destroying the instance while activity tasks
    /// are in flight is undefined behavior.
    /// Supports zero, one, or multiple arguments.
    template <typename T, typename R, typename... Args>
    static std::shared_ptr<ActivityDefinition> create(
        const std::string& name,
        coro::Task<R> (T::*method)(Args...),
        T* instance) {
        auto def = std::make_shared<ActivityDefinition>();
        def->name_ = name;
        def->executor_ = [method, instance](std::vector<std::any> args)
            -> coro::Task<std::any> {
            co_return co_await detail::invoke_member(
                method, instance, std::move(args));
        };
        return def;
    }

    /// The activity name (empty for dynamic activities).
    const std::string& name() const noexcept { return name_; }

    /// Whether this is a dynamic activity.
    bool is_dynamic() const noexcept { return name_.empty(); }

    /// Execute the activity with the given arguments.
    coro::Task<std::any> execute(std::vector<std::any> args) const {
        return executor_(std::move(args));
    }

private:
    std::string name_;
    std::function<coro::Task<std::any>(std::vector<std::any>)> executor_;
};

}  // namespace temporalio::activities
