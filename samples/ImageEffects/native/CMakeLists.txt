cmake_minimum_required(VERSION 3.21)
project(acme_effects LANGUAGES C)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_C_EXTENSIONS OFF)

add_library(acme_core SHARED src/core.c)
target_include_directories(acme_core PUBLIC include)
target_compile_definitions(acme_core PRIVATE ACME_CORE_BUILD)

add_library(acme_effects SHARED src/effects.c src/codec.c)
target_include_directories(acme_effects PRIVATE include vendor)
target_compile_definitions(acme_effects PRIVATE ACME_EFFECTS_BUILD)
if(WIN32 AND NOT MSVC)
    # GNU-style Windows linkers can consume the DLL export table directly.
    # Zig's linker does not emit the import archive CMake expects for a target
    # link, so retain the build edge and link the actual colocated DLL.
    add_dependencies(acme_effects acme_core)
    target_link_libraries(acme_effects PRIVATE "$<TARGET_FILE:acme_core>")
else()
    target_link_libraries(acme_effects PRIVATE acme_core)
endif()

foreach(target IN ITEMS acme_core acme_effects)
    set_target_properties(${target} PROPERTIES
        POSITION_INDEPENDENT_CODE ON
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out"
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out")
endforeach()

if(APPLE)
    # Both dylibs are staged in the same per-RID native directory.
    set_target_properties(acme_core acme_effects PROPERTIES
        MACOSX_RPATH ON
        BUILD_WITH_INSTALL_NAME_DIR ON
        INSTALL_NAME_DIR "@loader_path"
        BUILD_WITH_INSTALL_RPATH ON
        INSTALL_RPATH "@loader_path")
elseif(UNIX)
    set_target_properties(acme_effects PROPERTIES
        BUILD_RPATH "$ORIGIN"
        INSTALL_RPATH "$ORIGIN")
endif()
