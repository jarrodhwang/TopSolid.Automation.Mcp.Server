#include <BRep_Builder.hxx>
#include <BRepMesh_IncrementalMesh.hxx>
#include <BRepPrimAPI_MakeBox.hxx>
#include <BRepTools.hxx>
#include <BRep_Tool.hxx>
#include <IGESControl_Reader.hxx>
#include <STEPControl_Reader.hxx>
#include <STEPControl_Writer.hxx>
#include <Standard_Failure.hxx>
#include <Standard_Version.hxx>
#include <StlAPI_Writer.hxx>
#include <TopExp_Explorer.hxx>
#include <TopoDS.hxx>
#include <TopoDS_Shape.hxx>
#include <Poly_Triangulation.hxx>
#include <algorithm>
#include <chrono>
#include <cmath>
#include <filesystem>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>

// Separate process: a failed neutral-CAD reader must not bring down Studio or TopSolid.
// Inputs/outputs are explicit local paths supplied by the host, never executable code or URLs.
int Run(int argc, char** argv)
{
    std::filesystem::path output;
    try
    {
        if (argc == 3 && std::string(argv[1]) == "--write-test-step")
        {
            if (std::filesystem::exists(std::filesystem::u8path(argv[2]))) throw std::runtime_error("Fixture already exists");
            STEPControl_Writer writer;
            if (writer.Transfer(BRepPrimAPI_MakeBox(40, 30, 20).Shape(), STEPControl_AsIs) != IFSelect_RetDone ||
                writer.Write(argv[2]) != IFSelect_RetDone) throw std::runtime_error("Could not create test fixture");
            return 0;
        }
        if (argc != 5) throw std::runtime_error("Usage: TopSolid.OcctPreview input.step output.stl linearToleranceMm angularToleranceDegrees");
        const std::filesystem::path input = std::filesystem::u8path(argv[1]);
        output = std::filesystem::u8path(argv[2]);
        if (!std::filesystem::is_regular_file(input) || std::filesystem::exists(output))
            throw std::runtime_error("Input must exist and output must be a new file");
        size_t linearEnd = 0, angularEnd = 0;
        const double linear = std::stod(argv[3], &linearEnd), angleDegrees = std::stod(argv[4], &angularEnd);
        if (linearEnd != std::string(argv[3]).size() || angularEnd != std::string(argv[4]).size() ||
            !std::isfinite(linear) || !std::isfinite(angleDegrees) || linear <= 0 || angleDegrees <= 0 || angleDegrees > 45)
            throw std::runtime_error("Invalid display tolerance");
        const auto start = std::chrono::steady_clock::now();
        auto extension = input.extension().string();
        std::transform(extension.begin(), extension.end(), extension.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
        TopoDS_Shape shape;
        if (extension == ".step" || extension == ".stp")
        {
            STEPControl_Reader reader;
            if (reader.ReadFile(input.u8string().c_str()) != IFSelect_RetDone) throw std::runtime_error("STEP import failed");
            reader.SetSystemLengthUnit(1.0); // OCCT display contract is millimetres.
            if (!reader.TransferRoots()) throw std::runtime_error("STEP contains no transferred roots");
            shape = reader.OneShape();
        }
        else if (extension == ".iges" || extension == ".igs")
        {
            IGESControl_Reader reader;
            if (reader.ReadFile(input.u8string().c_str()) != IFSelect_RetDone || !reader.TransferRoots()) throw std::runtime_error("IGES import failed");
            shape = reader.OneShape();
        }
        else if (extension == ".brep")
        {
            BRep_Builder builder;
            if (!BRepTools::Read(shape, input.u8string().c_str(), builder)) throw std::runtime_error("BREP import failed");
        }
        else throw std::runtime_error("Only STEP, IGES and OCCT BREP inputs are supported");
        if (shape.IsNull()) throw std::runtime_error("No geometry in imported document");
        const auto imported = std::chrono::steady_clock::now();
        BRepMesh_IncrementalMesh mesher(shape, linear, false, angleDegrees * 3.14159265358979323846 / 180, true);
        if (!mesher.IsDone() || mesher.GetStatusFlags() != 0) throw std::runtime_error("OCCT could not tessellate every requested face");
        uint64_t faces = 0, triangles = 0;
        for (TopExp_Explorer it(shape, TopAbs_FACE); it.More(); it.Next())
        {
            TopLoc_Location location;
            const auto mesh = BRep_Tool::Triangulation(TopoDS::Face(it.Current()), location);
            if (mesh.IsNull()) throw std::runtime_error("An imported face has no triangulation");
            ++faces; triangles += mesh->NbTriangles();
        }
        if (triangles == 0 || triangles > UINT32_MAX) throw std::runtime_error("Geometry exceeds binary STL's triangle-count field; use a tiled interchange format");
        StlAPI_Writer writer; writer.ASCIIMode() = false;
        if (!writer.Write(shape, output.u8string().c_str())) throw std::runtime_error("STL export failed");
        const auto finished = std::chrono::steady_clock::now();
        const auto ms = [](auto duration) { return std::chrono::duration<double, std::milli>(duration).count(); };
        std::cout << "{\"kernel\":\"OCCT " << OCC_VERSION_COMPLETE << "\",\"faces\":" << faces << ",\"triangles\":" << triangles
                  << ",\"units\":\"mm\",\"linearToleranceMm\":" << linear << ",\"angularToleranceDegrees\":" << angleDegrees
                  << ",\"parallelMeshing\":true,\"importMilliseconds\":" << ms(imported - start)
                  << ",\"meshAndWriteMilliseconds\":" << ms(finished - imported) << "}\n";
        return 0;
    }
    catch (const Standard_Failure& error) { std::cerr << "OCCT failure: " << error.GetMessageString() << '\n'; }
    catch (const std::exception& error) { std::cerr << error.what() << '\n'; }
    // The host owns deletion of its unique partial output. Never remove an input or a pre-existing file here.
    return 1;
}

#ifdef _WIN32
int wmain(int argc, wchar_t** argv)
{
    // Windows paths (including Korean names) arrive as UTF-16. OCCT accepts UTF-8 paths.
    std::vector<std::string> utf8;
    for (int i = 0; i < argc; ++i) utf8.push_back(std::filesystem::path(argv[i]).u8string());
    std::vector<char*> args;
    for (auto& value : utf8) args.push_back(value.data());
    return Run(argc, args.data());
}
#else
int main(int argc, char** argv) { return Run(argc, argv); }
#endif
