#include "temporalio/testing/activity_environment.h"

#include <utility>

namespace temporalio::testing {

ActivityEnvironment::ActivityEnvironment()
    : heartbeater_([](std::vector<std::any>) {}) {}

ActivityEnvironment::~ActivityEnvironment() = default;

void ActivityEnvironment::set_client(
    std::shared_ptr<client::TemporalClient> client) {
    client_ = std::move(client);
}

void ActivityEnvironment::set_heartbeater(
    std::function<void(std::vector<std::any>)> fn) {
    heartbeater_ = std::move(fn);
}

void ActivityEnvironment::cancel() {
    cancel_source_.request_stop();
}

} // namespace temporalio::testing
