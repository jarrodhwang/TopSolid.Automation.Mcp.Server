cmake_minimum_required(VERSION 3.24)
project(TopSolidOcctPreview VERSION 1.0 LANGUAGES CXX)
find_package(OpenCASCADE 7.9.3 EXACT REQUIRED)
add_executable(TopSolid.OcctPreview main.cpp)
target_compile_features(TopSolid.OcctPreview PRIVATE cxx_std_17)
target_include_directories(TopSolid.OcctPreview PRIVATE ${OpenCASCADE_INCLUDE_DIR})
target_link_directories(TopSolid.OcctPreview PRIVATE ${OpenCASCADE_LIBRARY_DIR})
target_link_libraries(TopSolid.OcctPreview PRIVATE TKernel TKMath TKG2d TKG3d TKGeomBase TKBRep
  TKGeomAlgo TKTopAlgo TKPrim TKMesh TKXSBase TKDESTEP TKDEIGES TKDESTL)
if(MSVC)
  target_compile_options(TopSolid.OcctPreview PRIVATE /W4 /EHsc /utf-8)
  # x64 baseline remains portable. Do not force AVX2/AVX512 on every workstation.
endif()
install(TARGETS TopSolid.OcctPreview RUNTIME DESTINATION .)
