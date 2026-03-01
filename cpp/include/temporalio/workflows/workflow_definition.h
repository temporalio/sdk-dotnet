#pragma once

/// @file Type-safe workflow definition builder and registration types.

#include <any>
#include <functional>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <tuple>
#include <type_traits>
#include <unordered_map>
#include <utility>
#include <vector>

#include <temporalio/coro/task.h>
#include <temporalio/workflows/workflow.h>

namespace temporalio::workflows {

namespace detail {

/// Extract arguments from a vector<std::any> into a tuple via index_sequence.
/// Uses Workflow::decode_value to handle converters::Payload decoding.
template <typename Tuple, std::size_t... I>
Tuple extract_args(const std::vector<std::any>& args,
                   std::index_sequence<I...>) {
    return Tuple{
        Workflow::decode_value<std::tuple_element_t<I, Tuple>>(args.at(I))...};
}

/// Call a member function with arguments extracted from vector<std::any>.
/// Returns Task<std::any> wrapping the result.
/// Uses Workflow::decode_value to properly decode converters::Payload args.
template <typename T, typename R, typename... Args, std::size_t... I>
coro::Task<std::any> invoke_run(
    coro::Task<R> (T::*method)(Args...),
    T* self,
    const std::vector<std::any>& args,
    std::index_sequence<I...>) {
    if constexpr (std::is_void_v<R>) {
        co_await (self->*method)(
            Workflow::decode_value<std::decay_t<Args>>(args.at(I))...);
        co_return std::any{};
    } else {
        auto result = co_await (self->*method)(
            Workflow::decode_value<std::decay_t<Args>>(args.at(I))...);
        co_return std::any(std::move(result));
    }
}

/// Call a void-returning async member function with extracted args.
template <typename T, typename... Args, std::size_t... I>
coro::Task<void> invoke_signal(
    coro::Task<void> (T::*method)(Args...),
    T* self,
    const std::vector<std::any>& args,
    std::index_sequence<I...>) {
    co_await (self->*method)(
        Workflow::decode_value<std::decay_t<Args>>(args.at(I))...);
}

/// Call a synchronous member function (const) with extracted args.
template <typename T, typename R, typename... Args, std::size_t... I>
std::any invoke_query(
    R (T::*method)(Args...) const,
    const T* self,
    const std::vector<std::any>& args,
    std::index_sequence<I...>) {
    return std::any((self->*method)(
        Workflow::decode_value<std::decay_t<Args>>(args.at(I))...));
}

/// Call a synchronous member function (non-const) with extracted args.
template <typename T, typename R, typename... Args, std::size_t... I>
std::any invoke_query_nc(
    R (T::*method)(Args...),
    T* self,
    const std::vector<std::any>& args,
    std::index_sequence<I...>) {
    return std::any((self->*method)(
        Workflow::decode_value<std::decay_t<Args>>(args.at(I))...));
}

/// Call a synchronous void member function (validator) with extracted args.
template <typename T, typename... Args, std::size_t... I>
void invoke_validator(
    void (T::*method)(Args...),
    T* self,
    const std::vector<std::any>& args,
    std::index_sequence<I...>) {
    (self->*method)(
        Workflow::decode_value<std::decay_t<Args>>(args.at(I))...);
}

}  // namespace detail

/// Policy for handling unfinished signal/update handlers.
enum class HandlerUnfinishedPolicy {
    kWarnAndAbandon = 0,
    kAbandon = 1,
};

/// Definition of a workflow signal handler.
struct WorkflowSignalDefinition {
    /// Signal name. Empty string means dynamic signal.
    std::string name;
    /// Optional description.
    std::string description;
    /// Handler function: (instance, args_payload) -> Task<void>
    std::function<coro::Task<void>(void*, std::vector<std::any>)> handler;
    /// Policy for unfinished handlers.
    HandlerUnfinishedPolicy unfinished_policy =
        HandlerUnfinishedPolicy::kWarnAndAbandon;
};

/// Definition of a workflow query handler.
struct WorkflowQueryDefinition {
    /// Query name. Empty string means dynamic query.
    std::string name;
    /// Optional description.
    std::string description;
    /// Handler function: (instance, args_payload) -> result as any
    std::function<std::any(void*, std::vector<std::any>)> handler;
};

/// Definition of a workflow update handler.
struct WorkflowUpdateDefinition {
    /// Update name. Empty string means dynamic update.
    std::string name;
    /// Optional description.
    std::string description;
    /// Handler function: (instance, args_payload) -> Task<any>
    std::function<coro::Task<std::any>(void*, std::vector<std::any>)>
        handler;
    /// Optional validator: (instance, args_payload) -> void (throws on
    /// invalid)
    std::function<void(void*, std::vector<std::any>)> validator;
    /// Policy for unfinished handlers.
    HandlerUnfinishedPolicy unfinished_policy =
        HandlerUnfinishedPolicy::kWarnAndAbandon;
};

/// Definition of a workflow type, including its run function, signal/query/
/// update handlers, and factory.
///
/// Created via the builder API:
///   auto def = WorkflowDefinition::create<MyWorkflow>("MyWorkflow")
///       .run(&MyWorkflow::run)
///       .signal("name", &MyWorkflow::on_signal)
///       .query("status", &MyWorkflow::get_status)
///       .build();
class WorkflowDefinition {
public:
    /// Builder for constructing WorkflowDefinition instances.
    template <typename T>
    class Builder;

    /// Start building a definition for workflow type T with the given name.
    template <typename T>
    static Builder<T> create(const std::string& name);

    /// The workflow type name (empty for dynamic workflows).
    const std::string& name() const noexcept { return name_; }

    /// Whether this is a dynamic workflow (name is empty).
    bool is_dynamic() const noexcept { return name_.empty(); }

    /// Factory to create a new workflow instance. Returns a void* owning
    /// pointer managed by the shared_ptr destructor.
    std::shared_ptr<void> create_instance() const {
        if (factory_) {
            return factory_();
        }
        return nullptr;
    }

    /// The run function: (instance, args) -> Task<any>.
    const std::function<coro::Task<std::any>(void*, std::vector<std::any>)>&
    run_func() const noexcept {
        return run_func_;
    }

    /// Signal handlers by name.
    const std::unordered_map<std::string, WorkflowSignalDefinition>& signals()
        const noexcept {
        return signals_;
    }

    /// Dynamic signal handler, if any.
    const std::optional<WorkflowSignalDefinition>& dynamic_signal()
        const noexcept {
        return dynamic_signal_;
    }

    /// Query handlers by name.
    const std::unordered_map<std::string, WorkflowQueryDefinition>& queries()
        const noexcept {
        return queries_;
    }

    /// Dynamic query handler, if any.
    const std::optional<WorkflowQueryDefinition>& dynamic_query()
        const noexcept {
        return dynamic_query_;
    }

    /// Update handlers by name.
    const std::unordered_map<std::string, WorkflowUpdateDefinition>& updates()
        const noexcept {
        return updates_;
    }

    /// Dynamic update handler, if any.
    const std::optional<WorkflowUpdateDefinition>& dynamic_update()
        const noexcept {
        return dynamic_update_;
    }

private:
    WorkflowDefinition() = default;

    std::string name_;
    std::function<std::shared_ptr<void>()> factory_;
    std::function<coro::Task<std::any>(void*, std::vector<std::any>)>
        run_func_;
    std::unordered_map<std::string, WorkflowSignalDefinition> signals_;
    std::optional<WorkflowSignalDefinition> dynamic_signal_;
    std::unordered_map<std::string, WorkflowQueryDefinition> queries_;
    std::optional<WorkflowQueryDefinition> dynamic_query_;
    std::unordered_map<std::string, WorkflowUpdateDefinition> updates_;
    std::optional<WorkflowUpdateDefinition> dynamic_update_;
};

/// Builder for constructing a WorkflowDefinition from a user's workflow class.
template <typename T>
class WorkflowDefinition::Builder {
public:
    explicit Builder(const std::string& name) {
        def_.name_ = name;
        // Default factory: construct T with default constructor
        def_.factory_ = []() -> std::shared_ptr<void> {
            return std::shared_ptr<void>(new T(),
                                         [](void* p) { delete static_cast<T*>(p); });
        };
    }

    /// Set the run method (member function returning Task<R>).
    /// Supports zero, one, or multiple arguments.
    template <typename R, typename... Args>
    Builder& run(coro::Task<R> (T::*method)(Args...)) {
        def_.run_func_ = [method](void* instance,
                                  std::vector<std::any> args)
            -> coro::Task<std::any> {
            auto* self = static_cast<T*>(instance);
            if constexpr (sizeof...(Args) == 0) {
                if constexpr (std::is_void_v<R>) {
                    co_await (self->*method)();
                    co_return std::any{};
                } else {
                    auto result = co_await (self->*method)();
                    co_return std::any(std::move(result));
                }
            } else if constexpr (sizeof...(Args) == 1) {
                using Arg0 = std::tuple_element_t<0, std::tuple<Args...>>;
                if constexpr (std::is_same_v<Arg0, std::vector<std::any>>) {
                    // When the run method takes std::vector<std::any>,
                    // pass the entire args vector directly (raw access).
                    if constexpr (std::is_void_v<R>) {
                        co_await (self->*method)(std::move(args));
                        co_return std::any{};
                    } else {
                        auto result =
                            co_await (self->*method)(std::move(args));
                        co_return std::any(std::move(result));
                    }
                } else {
                    co_return co_await detail::invoke_run(
                        method, self, args,
                        std::index_sequence_for<Args...>{});
                }
            } else {
                co_return co_await detail::invoke_run(
                    method, self, args,
                    std::index_sequence_for<Args...>{});
            }
        };
        return *this;
    }

    /// Add a signal handler (member function returning Task<void>).
    /// Supports zero, one, or multiple arguments.
    template <typename... Args>
    Builder& signal(const std::string& name,
                    coro::Task<void> (T::*method)(Args...),
                    HandlerUnfinishedPolicy policy =
                        HandlerUnfinishedPolicy::kWarnAndAbandon) {
        WorkflowSignalDefinition sig;
        sig.name = name;
        sig.unfinished_policy = policy;
        sig.handler = [method](void* instance,
                               std::vector<std::any> args)
            -> coro::Task<void> {
            auto* self = static_cast<T*>(instance);
            if constexpr (sizeof...(Args) == 0) {
                co_await (self->*method)();
            } else {
                co_await detail::invoke_signal(
                    method, self, args,
                    std::index_sequence_for<Args...>{});
            }
        };
        def_.signals_[name] = std::move(sig);
        return *this;
    }

    /// Add a query handler (member function returning a value).
    /// Supports zero, one, or multiple arguments.
    template <typename R, typename... Args>
    Builder& query(const std::string& name, R (T::*method)(Args...) const) {
        WorkflowQueryDefinition qry;
        qry.name = name;
        qry.handler = [method](void* instance,
                               [[maybe_unused]] std::vector<std::any> args) -> std::any {
            auto* self = static_cast<const T*>(instance);
            if constexpr (sizeof...(Args) == 0) {
                return std::any((self->*method)());
            } else {
                return detail::invoke_query(
                    method, self, args,
                    std::index_sequence_for<Args...>{});
            }
        };
        def_.queries_[name] = std::move(qry);
        return *this;
    }

    /// Add a query handler (non-const member function).
    /// Supports zero, one, or multiple arguments.
    template <typename R, typename... Args>
    Builder& query(const std::string& name, R (T::*method)(Args...)) {
        WorkflowQueryDefinition qry;
        qry.name = name;
        qry.handler = [method](void* instance,
                               [[maybe_unused]] std::vector<std::any> args) -> std::any {
            auto* self = static_cast<T*>(instance);
            if constexpr (sizeof...(Args) == 0) {
                return std::any((self->*method)());
            } else {
                return detail::invoke_query_nc(
                    method, self, args,
                    std::index_sequence_for<Args...>{});
            }
        };
        def_.queries_[name] = std::move(qry);
        return *this;
    }

    /// Add an update handler.
    /// Supports zero, one, or multiple arguments.
    template <typename R, typename... Args>
    Builder& update(const std::string& name,
                    coro::Task<R> (T::*method)(Args...),
                    void (T::*validator)(Args...) = nullptr,
                    HandlerUnfinishedPolicy policy =
                        HandlerUnfinishedPolicy::kWarnAndAbandon) {
        WorkflowUpdateDefinition upd;
        upd.name = name;
        upd.unfinished_policy = policy;
        upd.handler = [method](void* instance,
                               std::vector<std::any> args)
            -> coro::Task<std::any> {
            auto* self = static_cast<T*>(instance);
            if constexpr (sizeof...(Args) == 0) {
                if constexpr (std::is_void_v<R>) {
                    co_await (self->*method)();
                    co_return std::any{};
                } else {
                    auto result = co_await (self->*method)();
                    co_return std::any(std::move(result));
                }
            } else {
                co_return co_await detail::invoke_run(
                    method, self, args,
                    std::index_sequence_for<Args...>{});
            }
        };
        if (validator) {
            upd.validator = [validator](void* instance,
                                        std::vector<std::any> args) {
                auto* self = static_cast<T*>(instance);
                if constexpr (sizeof...(Args) == 0) {
                    (self->*validator)();
                } else {
                    detail::invoke_validator(
                        validator, self, args,
                        std::index_sequence_for<Args...>{});
                }
            };
        }
        def_.updates_[name] = std::move(upd);
        return *this;
    }

    /// Set a custom factory function.
    Builder& factory(std::function<std::shared_ptr<void>()> f) {
        def_.factory_ = std::move(f);
        return *this;
    }

    /// Build and return the final WorkflowDefinition.
    /// @throws std::logic_error if build() has already been called.
    std::shared_ptr<WorkflowDefinition> build() {
        if (built_) {
            throw std::logic_error(
                "WorkflowDefinition::Builder::build() called more than once");
        }
        if (!def_.run_func_) {
            throw std::logic_error(
                "WorkflowDefinition::Builder::build() called without setting "
                "a run function (use .run() or .run_method())");
        }
        built_ = true;
        // Cannot use std::make_shared here because WorkflowDefinition's
        // constructors are private. Builder has access as a nested class,
        // but std::make_shared constructs via an allocator that does not.
        auto result = std::shared_ptr<WorkflowDefinition>(
            new WorkflowDefinition(std::move(def_)));
        return result;
    }

private:
    WorkflowDefinition def_;
    bool built_{false};
};

template <typename T>
WorkflowDefinition::Builder<T> WorkflowDefinition::create(
    const std::string& name) {
    return Builder<T>(name);
}

}  // namespace temporalio::workflows
