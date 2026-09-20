#include <draco/compression/decode.h>
#include <fstream>
#include <filesystem>
#include <iostream>
#include <vector>
#include <stdexcept>
#include <cmath>
#include <cstdint>

// One isolated, bounded batch. No CAD, network, paths embedded in input, or shared state.
int main(int argc, char** argv) {
  try {
    if (argc != 3 || std::filesystem::exists(std::filesystem::u8path(argv[2]))) throw std::runtime_error("Invalid decoder arguments");
    std::ifstream input(std::filesystem::u8path(argv[1]), std::ios::binary);
    std::ofstream output(std::filesystem::u8path(argv[2]), std::ios::binary);
    auto read = [&]() { uint32_t value; if (!input.read(reinterpret_cast<char*>(&value),4)) throw std::runtime_error("Truncated batch"); return value; };
    auto write = [&](uint32_t value) { output.write(reinterpret_cast<char*>(&value),4); };
    if (read() != 0x44524331) throw std::runtime_error("Invalid batch");
    const auto count=read(); if (count > 2048) throw std::runtime_error("Too many meshes"); write(count);
    uint64_t totalPoints=0, totalFaces=0;
    for (uint32_t n=0;n<count;n++) {
      const auto length=read(), positionId=read(), normalId=read();
      if (length > 64*1024*1024) throw std::runtime_error("Oversized compressed mesh");
      std::vector<char> bytes(length); if (!input.read(bytes.data(),length)) throw std::runtime_error("Truncated mesh");
      draco::DecoderBuffer buffer; buffer.Init(bytes.data(),bytes.size()); draco::Decoder decoder;
      auto result=decoder.DecodeMeshFromBuffer(&buffer); if (!result.ok()) throw std::runtime_error("Invalid Draco mesh");
      auto mesh=std::move(result).value();
      totalPoints+=mesh->num_points(); totalFaces+=mesh->num_faces();
      if (totalPoints>6000000 || totalFaces>4000000) throw std::runtime_error("Decoded geometry limit");
      const auto* positions=mesh->GetAttributeByUniqueId(positionId);
      const auto* normals=normalId==UINT32_MAX?nullptr:mesh->GetAttributeByUniqueId(normalId);
      if (!positions || positions->num_components()!=3 || (normalId!=UINT32_MAX && (!normals || normals->num_components()!=3))) throw std::runtime_error("Invalid mesh attributes");
      write(mesh->num_points()); write(mesh->num_faces()*3); write(normals?1:0);
      for (auto attribute : {positions,normals}) {
        if (!attribute) continue;
        for (uint32_t p=0;p<mesh->num_points();p++) {
          float v[3]; if (!attribute->ConvertValue<float,3>(attribute->mapped_index(draco::PointIndex(p)),v)) throw std::runtime_error("Invalid attribute value");
          for (auto value:v) if (!std::isfinite(value)) throw std::runtime_error("Non-finite vertex");
          output.write(reinterpret_cast<char*>(v),sizeof(v));
        }
      }
      for (uint32_t f=0;f<mesh->num_faces();f++) for (auto point:mesh->face(draco::FaceIndex(f))) write(point.value());
      if (!output) throw std::runtime_error("Could not write geometry");
    }
    if (input.peek()!=std::char_traits<char>::eof()) throw std::runtime_error("Trailing batch data");
    return 0;
  } catch (const std::exception& error) { std::cerr << error.what(); return 1; }
}
