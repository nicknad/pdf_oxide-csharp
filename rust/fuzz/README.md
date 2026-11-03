# FFI fuzz targets

Two `cargo fuzz` targets exercise the C ABI directly through the Rust exports:

- `document` - writes mutated bytes to a temp file and calls every read/extract API.
- `builder` - replays a random operation sequence against `PdfBuilderState`.

The crate is built with the `rlib` crate type as well as `cdylib` so these
targets can call the exported functions without loading the shared library.

## Run

```powershell
cargo install cargo-fuzz
cargo +nightly fuzz run document -- -max_total_time=300
cargo +nightly fuzz run builder -- -max_total_time=300
```

List or replay a corpus:

```powershell
cargo +nightly fuzz list
cargo +nightly fuzz run document rust/fuzz/artifacts/document/<crash-file>
```

Seed the document corpus with real files (optional; `test-assets/` is not tracked):

```powershell
New-Item -ItemType Directory -Force rust/fuzz/corpus/document | Out-Null
Get-ChildItem test-assets/*.pdf | Select-Object -First 20 | Copy-Item -Destination rust/fuzz/corpus/document
```

## Sanitizers

`cargo fuzz` enables AddressSanitizer by default on Linux and macOS. On Windows it
requires nightly and a compatible toolchain; see the `cargo-fuzz` documentation.
Run the same targets from Linux in CI for the strongest signal.

## Panic/abort hook

To exercise the panic-across-FFI scenario (`panic-hook` in `FfiCrashHost`), rebuild
the native library with the test feature, then copy it into `runtimes/`:

```powershell
cargo build --manifest-path rust/Cargo.toml --features ffi-test-hooks
Copy-Item rust/target/debug/pdfoxide_bindings.dll runtimes/win-x64/native/pdfoxide_bindings.dll -Force
```

Never ship a binary built with `ffi-test-hooks`.
