cmake_minimum_required(VERSION 3.20)
project(temporalio_consumer_test CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

find_package(temporalio CONFIG REQUIRED)

add_executable(consumer_test main.cpp)
target_link_libraries(consumer_test PRIVATE temporalio::temporalio)
