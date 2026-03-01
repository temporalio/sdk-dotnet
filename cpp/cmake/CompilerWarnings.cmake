# CompilerWarnings.cmake - Strict compiler warning flags

function(temporalio_set_compiler_warnings target)
    if(MSVC)
        target_compile_options(${target} PRIVATE
            /W4
            /WX
            /permissive-
            /Zc:__cplusplus   # Report correct __cplusplus value
            /Zc:preprocessor  # Standards-conforming preprocessor
            /external:anglebrackets  # Treat angle-bracket includes as external
            /external:W0             # Suppress warnings in external headers
        )
    elseif(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang")
        target_compile_options(${target} PRIVATE
            -Wall
            -Wextra
            -Werror
            -Wpedantic
            -Wconversion
            -Wsign-conversion
            -Wnon-virtual-dtor
            -Wold-style-cast
            -Wcast-align
            -Woverloaded-virtual
            -Wno-unused-parameter         # Common during early development
            -Wno-missing-field-initializers  # C++20 designated initializers intentionally omit fields
        )

        # Coroutine support flags (compiler-specific)
        if(CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
            # GCC requires -fcoroutines for C++20 coroutine support
            target_compile_options(${target} PRIVATE -fcoroutines)
        elseif(CMAKE_CXX_COMPILER_ID STREQUAL "Clang")
            # Clang 16+: coroutines enabled automatically with -std=c++20
            # Clang < 16: needs -fcoroutines-ts
            if(CMAKE_CXX_COMPILER_VERSION VERSION_LESS "16.0")
                target_compile_options(${target} PRIVATE -fcoroutines-ts)
            endif()
            # interop.h (auto-generated from Rust cbindgen) uses anonymous
            # structs inside anonymous unions — a valid C idiom that Clang
            # treats as a C++ extension under -Wpedantic.
            target_compile_options(${target} PRIVATE -Wno-nested-anon-types)
            # Some private fields are declared for future use (e.g. counters
            # in WorkflowInstance). GCC does not warn on these but Clang does.
            target_compile_options(${target} PRIVATE -Wno-unused-private-field)
        endif()
    else()
        message(WARNING "Temporalio: Unknown compiler '${CMAKE_CXX_COMPILER_ID}' - no extra warnings set")
    endif()
endfunction()
