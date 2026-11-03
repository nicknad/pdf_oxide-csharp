# Native FFI ABI driver

A small C program that loads `pdfoxide_bindings` with `dlopen`/`LoadLibrary` and
resolves every symbol through `dlsym`/`GetProcAddress`. It is independent of .NET
marshalling, so it verifies the real C ABI and export table.

## Build

Linux/macOS:

```bash
cc -std=c11 -Wall -Wextra -Werror tools/ffi-abi-driver/ffi_abi_driver.c -o ffi_abi_driver -ldl
```

Windows (MSVC Developer Prompt):

```powershell
cl /W4 /std:c11 tools\ffi-abi-driver\ffi_abi_driver.c /Fe:ffi_abi_driver.exe
```

Windows (MinGW):

```powershell
gcc -std=c11 -Wall -Wextra -Werror tools/ffi-abi-driver/ffi_abi_driver.c -o ffi_abi_driver.exe
```

## Run

```bash
./ffi_abi_driver runtimes/linux-x64/native/libpdfoxide_bindings.so symbols
./ffi_abi_driver runtimes/linux-x64/native/libpdfoxide_bindings.so smoke
./ffi_abi_driver runtimes/linux-x64/native/libpdfoxide_bindings.so null-path
```

On Windows:

```powershell
.\ffi_abi_driver.exe runtimes\win-x64\native\pdfoxide_bindings.dll symbols
```

Scenarios:

- `symbols` - every required shim and upstream export is present.
- `smoke` - create a PDF from text, extract text, convert to Markdown, free every
  returned string.
- `null-path` - a NULL path returns NULL with a retrievable error message.

## Sanitizers

Build the driver with `-fsanitize=address` and load a cdylib built with
`RUSTFLAGS="-Zsanitizer=address"`. See `.github/workflows/sanitizers.yml`.
