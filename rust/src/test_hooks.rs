//! Test-only FFI hooks.
//!
//! These are compiled only when the `ffi-test-hooks` feature is enabled. They let
//! the crash host verify what happens when a panic or explicit abort crosses the
//! `extern "C"` boundary. Never enable this feature in a shipped build.

/// Panic or abort on purpose.
///
/// * `kind == 1` calls [`std::process::abort`].
/// * Any other value panics with a fixed message.
///
/// This symbol is intentionally not listed in `build.rs`, so `csbindgen` does not
/// generate a managed declaration for it. The crash host declares it directly.
#[unsafe(no_mangle)]
pub extern "C" fn pdfoxide_test_panic(kind: i32) {
    match kind {
        1 => std::process::abort(),
        _ => panic!("intentional panic from the ffi-test-hooks build"),
    }
}
