fn main() {
    csbindgen::Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("pdfoxide_bindings")
        .csharp_namespace("PdfOxide")
        .generate_csharp_file("../dotnet/PdfOxideBindings.cs")
        .unwrap();
}