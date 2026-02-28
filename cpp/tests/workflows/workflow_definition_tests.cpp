#include <gtest/gtest.h>

#include <any>
#include <string>
#include <vector>

#include "temporalio/coro/task.h"
#include "temporalio/workflows/workflow_definition.h"

using namespace temporalio::coro;
using namespace temporalio::workflows;

// ===========================================================================
// Sample workflow for testing
// ===========================================================================
namespace {

class GreetingWorkflow {
public:
    Task<std::string> run(std::string name) {
        greeting_ = "Hello, " + name;
        co_return greeting_;
    }

    Task<void> set_greeting(std::string greeting) {
        greeting_ = std::move(greeting);
        co_return;
    }

    std::string get_greeting() const { return greeting_; }

    Task<std::string> update_greeting(std::string new_greeting) {
        auto old = greeting_;
        greeting_ = std::move(new_greeting);
        co_return old;
    }

    void validate_greeting(std::string greeting) {
        if (greeting.empty()) {
            throw std::invalid_argument("greeting cannot be empty");
        }
    }

private:
    std::string greeting_{"default"};
};

class SimpleWorkflow {
public:
    Task<int> run() { co_return 42; }
};

// Multi-argument workflow for testing variadic template support
class MultiArgWorkflow {
public:
    Task<int> run(int a, int b) {
        result_ = a + b;
        co_return result_;
    }

    Task<void> multi_signal(std::string name, int value) {
        signals_.push_back(name + "=" + std::to_string(value));
        co_return;
    }

    std::string multi_query(std::string prefix, int count) const {
        return prefix + ":" + std::to_string(count);
    }

    // Non-const multi-arg query
    int sum_query(int a, int b) {
        return a + b;
    }

    Task<std::string> multi_update(std::string key, int val) {
        auto old = last_update_;
        last_update_ = key + "=" + std::to_string(val);
        co_return old;
    }

    void validate_update(std::string key, int val) {
        if (key.empty()) {
            throw std::invalid_argument("key cannot be empty");
        }
        if (val < 0) {
            throw std::invalid_argument("val must be non-negative");
        }
    }

    // Three-argument methods to test > 2 args
    Task<std::string> run_three(std::string a, int b, double c) {
        co_return a + ":" + std::to_string(b) + ":" + std::to_string(c);
    }

    Task<void> signal_three(std::string a, int b, bool c) {
        signals_.push_back(a + ":" + std::to_string(b) + ":" +
                           (c ? "true" : "false"));
        co_return;
    }

    int get_result() const { return result_; }
    const std::vector<std::string>& get_signals() const { return signals_; }

private:
    int result_{0};
    std::string last_update_{"none"};
    std::vector<std::string> signals_;
};

}  // namespace

// ===========================================================================
// Builder basic tests
// ===========================================================================

TEST(WorkflowDefinitionTest, CreateWithName) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .build();

    EXPECT_EQ(def->name(), "GreetingWorkflow");
    EXPECT_FALSE(def->is_dynamic());
}

TEST(WorkflowDefinitionTest, CreateDynamic) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("")
                   .run(&GreetingWorkflow::run)
                   .build();

    EXPECT_TRUE(def->is_dynamic());
    EXPECT_TRUE(def->name().empty());
}

TEST(WorkflowDefinitionTest, CreateInstance) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .build();

    auto instance = def->create_instance();
    EXPECT_NE(instance, nullptr);

    // Verify the instance is the right type
    auto* wf = static_cast<GreetingWorkflow*>(instance.get());
    EXPECT_EQ(wf->get_greeting(), "default");
}

// ===========================================================================
// Signal handler
// ===========================================================================

TEST(WorkflowDefinitionTest, SignalHandler) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .signal("set_greeting", &GreetingWorkflow::set_greeting)
                   .build();

    EXPECT_EQ(def->signals().size(), 1u);
    EXPECT_TRUE(def->signals().count("set_greeting") > 0);

    auto& sig = def->signals().at("set_greeting");
    EXPECT_EQ(sig.name, "set_greeting");
    EXPECT_EQ(sig.unfinished_policy, HandlerUnfinishedPolicy::kWarnAndAbandon);
}

TEST(WorkflowDefinitionTest, SignalHandlerWithPolicy) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .signal("set_greeting", &GreetingWorkflow::set_greeting,
                           HandlerUnfinishedPolicy::kAbandon)
                   .build();

    auto& sig = def->signals().at("set_greeting");
    EXPECT_EQ(sig.unfinished_policy, HandlerUnfinishedPolicy::kAbandon);
}

// ===========================================================================
// Query handler
// ===========================================================================

TEST(WorkflowDefinitionTest, QueryHandler) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .query("get_greeting", &GreetingWorkflow::get_greeting)
                   .build();

    EXPECT_EQ(def->queries().size(), 1u);
    EXPECT_TRUE(def->queries().count("get_greeting") > 0);

    auto& qry = def->queries().at("get_greeting");
    EXPECT_EQ(qry.name, "get_greeting");
}

TEST(WorkflowDefinitionTest, QueryHandlerExecution) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .query("get_greeting", &GreetingWorkflow::get_greeting)
                   .build();

    auto instance = def->create_instance();
    auto* wf = static_cast<GreetingWorkflow*>(instance.get());

    auto& qry = def->queries().at("get_greeting");
    auto result = qry.handler(wf, {});
    EXPECT_EQ(std::any_cast<std::string>(result), "default");
}

// ===========================================================================
// Update handler
// ===========================================================================

TEST(WorkflowDefinitionTest, UpdateHandler) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .update("update_greeting",
                           &GreetingWorkflow::update_greeting)
                   .build();

    EXPECT_EQ(def->updates().size(), 1u);
    EXPECT_TRUE(def->updates().count("update_greeting") > 0);

    auto& upd = def->updates().at("update_greeting");
    EXPECT_EQ(upd.name, "update_greeting");
    EXPECT_FALSE(upd.validator);  // No validator set
}

TEST(WorkflowDefinitionTest, UpdateHandlerWithValidator) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .update("update_greeting",
                           &GreetingWorkflow::update_greeting,
                           &GreetingWorkflow::validate_greeting)
                   .build();

    auto& upd = def->updates().at("update_greeting");
    EXPECT_TRUE(upd.validator != nullptr);

    // Test validator with valid input
    auto instance = def->create_instance();
    auto* wf = static_cast<GreetingWorkflow*>(instance.get());
    EXPECT_NO_THROW(
        upd.validator(wf, {std::any(std::string("valid"))}));

    // Test validator with invalid input
    EXPECT_THROW(
        upd.validator(wf, {std::any(std::string(""))}),
        std::invalid_argument);
}

// ===========================================================================
// Full definition with all handler types
// ===========================================================================

TEST(WorkflowDefinitionTest, FullDefinition) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("GreetingWorkflow")
                   .run(&GreetingWorkflow::run)
                   .signal("set_greeting", &GreetingWorkflow::set_greeting)
                   .query("get_greeting", &GreetingWorkflow::get_greeting)
                   .update("update_greeting",
                           &GreetingWorkflow::update_greeting,
                           &GreetingWorkflow::validate_greeting)
                   .build();

    EXPECT_EQ(def->name(), "GreetingWorkflow");
    EXPECT_EQ(def->signals().size(), 1u);
    EXPECT_EQ(def->queries().size(), 1u);
    EXPECT_EQ(def->updates().size(), 1u);
    EXPECT_NE(def->create_instance(), nullptr);
}

// ===========================================================================
// No-arg workflow
// ===========================================================================

TEST(WorkflowDefinitionTest, NoArgWorkflow) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("SimpleWorkflow")
                   .run(&SimpleWorkflow::run)
                   .build();

    EXPECT_EQ(def->name(), "SimpleWorkflow");
    EXPECT_NE(def->create_instance(), nullptr);
}

// ===========================================================================
// Empty signals/queries/updates
// ===========================================================================

TEST(WorkflowDefinitionTest, NoSignals) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("Test")
                   .run(&GreetingWorkflow::run)
                   .build();

    EXPECT_TRUE(def->signals().empty());
    EXPECT_FALSE(def->dynamic_signal().has_value());
}

TEST(WorkflowDefinitionTest, NoQueries) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("Test")
                   .run(&GreetingWorkflow::run)
                   .build();

    EXPECT_TRUE(def->queries().empty());
    EXPECT_FALSE(def->dynamic_query().has_value());
}

TEST(WorkflowDefinitionTest, NoUpdates) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("Test")
                   .run(&GreetingWorkflow::run)
                   .build();

    EXPECT_TRUE(def->updates().empty());
    EXPECT_FALSE(def->dynamic_update().has_value());
}

// ===========================================================================
// Custom factory
// ===========================================================================

TEST(WorkflowDefinitionTest, CustomFactory) {
    int create_count = 0;
    auto def = WorkflowDefinition::create<GreetingWorkflow>("Test")
                   .run(&GreetingWorkflow::run)
                   .factory([&create_count]() -> std::shared_ptr<void> {
                       ++create_count;
                       return std::shared_ptr<void>(
                           new GreetingWorkflow(),
                           [](void* p) { delete static_cast<GreetingWorkflow*>(p); });
                   })
                   .build();

    auto inst1 = def->create_instance();
    auto inst2 = def->create_instance();

    EXPECT_EQ(create_count, 2);
    EXPECT_NE(inst1.get(), inst2.get());  // Different instances
}

// ===========================================================================
// Multiple signal/query/update handlers
// ===========================================================================

TEST(WorkflowDefinitionTest, MultipleHandlers) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("Multi")
                   .run(&GreetingWorkflow::run)
                   .signal("set_greeting", &GreetingWorkflow::set_greeting)
                   .query("get_greeting", &GreetingWorkflow::get_greeting)
                   .update("update_greeting",
                           &GreetingWorkflow::update_greeting)
                   .build();

    EXPECT_EQ(def->signals().size(), 1u);
    EXPECT_EQ(def->queries().size(), 1u);
    EXPECT_EQ(def->updates().size(), 1u);
}

// ===========================================================================
// Multi-argument support tests
// ===========================================================================

TEST(WorkflowDefinitionTest, MultiArgRunRegisters) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("MultiArg")
                   .run(&MultiArgWorkflow::run)
                   .build();

    EXPECT_EQ(def->name(), "MultiArg");
    EXPECT_NE(def->create_instance(), nullptr);
    EXPECT_TRUE(def->run_func() != nullptr);
}

TEST(WorkflowDefinitionTest, MultiArgSignalRegisters) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("MultiArg")
                   .run(&MultiArgWorkflow::run)
                   .signal("multi_signal", &MultiArgWorkflow::multi_signal)
                   .build();

    EXPECT_EQ(def->signals().size(), 1u);
    EXPECT_TRUE(def->signals().count("multi_signal") > 0);
}

TEST(WorkflowDefinitionTest, MultiArgConstQueryExecution) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("MultiArg")
                   .run(&MultiArgWorkflow::run)
                   .query("multi_query", &MultiArgWorkflow::multi_query)
                   .build();

    auto instance = def->create_instance();
    auto* wf = static_cast<MultiArgWorkflow*>(instance.get());

    auto& qry = def->queries().at("multi_query");
    auto result = qry.handler(wf, {std::any(std::string("prefix")),
                                    std::any(42)});
    EXPECT_EQ(std::any_cast<std::string>(result), "prefix:42");
}

TEST(WorkflowDefinitionTest, MultiArgNonConstQueryExecution) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("MultiArg")
                   .run(&MultiArgWorkflow::run)
                   .query("sum", &MultiArgWorkflow::sum_query)
                   .build();

    auto instance = def->create_instance();
    auto* wf = static_cast<MultiArgWorkflow*>(instance.get());

    auto& qry = def->queries().at("sum");
    auto result = qry.handler(wf, {std::any(10), std::any(32)});
    EXPECT_EQ(std::any_cast<int>(result), 42);
}

TEST(WorkflowDefinitionTest, MultiArgUpdateRegisters) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("MultiArg")
                   .run(&MultiArgWorkflow::run)
                   .update("multi_update",
                           &MultiArgWorkflow::multi_update)
                   .build();

    EXPECT_EQ(def->updates().size(), 1u);
    EXPECT_TRUE(def->updates().count("multi_update") > 0);
}

TEST(WorkflowDefinitionTest, MultiArgUpdateWithValidatorExecution) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("MultiArg")
                   .run(&MultiArgWorkflow::run)
                   .update("multi_update",
                           &MultiArgWorkflow::multi_update,
                           &MultiArgWorkflow::validate_update)
                   .build();

    auto instance = def->create_instance();
    auto* wf = static_cast<MultiArgWorkflow*>(instance.get());

    auto& upd = def->updates().at("multi_update");
    EXPECT_TRUE(upd.validator != nullptr);

    // Valid input
    EXPECT_NO_THROW(
        upd.validator(wf, {std::any(std::string("key")), std::any(1)}));

    // Empty key
    EXPECT_THROW(
        upd.validator(wf, {std::any(std::string("")), std::any(1)}),
        std::invalid_argument);

    // Negative value
    EXPECT_THROW(
        upd.validator(wf, {std::any(std::string("key")), std::any(-1)}),
        std::invalid_argument);
}

TEST(WorkflowDefinitionTest, ThreeArgRunRegisters) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("ThreeArg")
                   .run(&MultiArgWorkflow::run_three)
                   .build();

    EXPECT_EQ(def->name(), "ThreeArg");
    EXPECT_TRUE(def->run_func() != nullptr);
}

TEST(WorkflowDefinitionTest, ThreeArgSignalRegisters) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("ThreeArg")
                   .run(&MultiArgWorkflow::run)
                   .signal("signal_three", &MultiArgWorkflow::signal_three)
                   .build();

    EXPECT_EQ(def->signals().size(), 1u);
    EXPECT_TRUE(def->signals().count("signal_three") > 0);
}

TEST(WorkflowDefinitionTest, FullMultiArgDefinition) {
    auto def = WorkflowDefinition::create<MultiArgWorkflow>("FullMulti")
                   .run(&MultiArgWorkflow::run)
                   .signal("multi_signal", &MultiArgWorkflow::multi_signal)
                   .query("multi_query", &MultiArgWorkflow::multi_query)
                   .query("sum", &MultiArgWorkflow::sum_query)
                   .update("multi_update",
                           &MultiArgWorkflow::multi_update,
                           &MultiArgWorkflow::validate_update)
                   .build();

    EXPECT_EQ(def->name(), "FullMulti");
    EXPECT_EQ(def->signals().size(), 1u);
    EXPECT_EQ(def->queries().size(), 2u);
    EXPECT_EQ(def->updates().size(), 1u);
}

// Verify backward compatibility: single-arg still works
TEST(WorkflowDefinitionTest, SingleArgRunStillWorks) {
    auto def = WorkflowDefinition::create<GreetingWorkflow>("Compat")
                   .run(&GreetingWorkflow::run)
                   .build();

    EXPECT_TRUE(def->run_func() != nullptr);
}

// Verify backward compatibility: no-arg still works
TEST(WorkflowDefinitionTest, NoArgRunStillWorks) {
    auto def = WorkflowDefinition::create<SimpleWorkflow>("Compat")
                   .run(&SimpleWorkflow::run)
                   .build();

    EXPECT_TRUE(def->run_func() != nullptr);
}
