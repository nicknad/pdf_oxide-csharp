//! PdfOxide FFI Bindings
//!
//! This module provides C-compatible FFI functions for the pdf_oxide library.
//! These functions allow external programs (such as C# applications) to interact
//! with pdf_oxide for PDF creation, conversion, and text extraction.
//!
//! # Memory Management
//!
//! All string-returning functions return pointers to C-strings that MUST be
//! freed by the caller using `pdf_string_free()`. Failure to do so will cause
//! memory leaks.
//!
//! # Error Handling
//!
//! When a function returns an error code (-1), the error message can be
//! retrieved using `pdf_last_error_message()`. The returned string must also
//! be freed with `pdf_string_free()`.
//!
//! # Thread Safety
//!
//! Error state is stored in a thread-local variable, making these functions
//! safe to use across multiple threads, though each call should complete
//! before another call from the same thread to ensure error messages are
//! not overwritten.

use pdf_oxide::api::Pdf;
use pdf_oxide::writer::{DocumentBuilder, PageSize};
use pdf_oxide::PdfDocument;
use std::cell::RefCell;
use std::ffi::{CStr, CString};
use std::io::{Error as IoError, ErrorKind};
use std::os::raw::c_char;

// Thread-local storage for error messages, retained so callers can retrieve the
// failure detail after an FFI function returns.
thread_local! {
    static LAST_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
}

/// State container for the DocumentBuilder API.
/// This holds the internal DocumentBuilder instance across multiple FFI calls.
pub struct PdfBuilderState {
    builder: DocumentBuilder,
}

// ============================================================================
// TEXT EXTRACTION FUNCTIONS
// ============================================================================

/// Extract all text content from a PDF file.
///
/// # Arguments
/// * `path` - Path to the PDF file as a null-terminated C string
///
/// # Returns
/// * Pointer to allocated C string containing extracted text, or NULL on error
/// #
/// # Safety
/// The returned pointer must be freed using `pdf_string_free()`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_text(path: *const c_char) -> *mut c_char {
    match extract_text_internal(path) {
        Ok(cstr) => cstr.into_raw(),
        Err(e) => {
            set_last_error(e);
            std::ptr::null_mut()
        }
    }
}

/// Internal implementation for text extraction.
/// Opens the PDF, iterates through all pages, and concatenates text content.
fn extract_text_internal(path: *const c_char) -> Result<CString, Box<dyn std::error::Error>> {
    let path_str = c_string_to_string(path, "path")?;

    let doc = PdfDocument::open(&path_str)?;
    let page_count = doc.page_count()?;

    // Concatenate text from all pages with newlines between pages
    let mut full_text = String::new();
    for i in 0..page_count {
        let text = doc.extract_text(i)?;
        full_text.push_str(&text);
        if i < page_count - 1 {
            full_text.push('\n');
        }
    }

    Ok(CString::new(full_text)?)
}

// ============================================================================
// MARKDOWN CONVERSION FUNCTIONS
// ============================================================================

/// Convert a single PDF page to Markdown format.
///
/// # Arguments
/// * `path` - Path to the PDF file as a null-terminated C string
/// * `page_index` - Zero-based index of the page to convert
///
/// # Returns
/// * Pointer to allocated C string containing Markdown, or NULL on error
///
/// # Safety
/// The returned pointer must be freed using `pdf_string_free()`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_markdown(path: *const c_char, page_index: i32) -> *mut c_char {
    match to_markdown_internal(path, page_index) {
        Ok(cstr) => cstr.into_raw(),
        Err(e) => {
            set_last_error(e);
            std::ptr::null_mut()
        }
    }
}

/// Convert all pages in a PDF to Markdown format.
///
/// # Arguments
/// * `path` - Path to the PDF file as a null-terminated C string
///
/// # Returns
/// * Pointer to allocated C string containing Markdown for all pages, or NULL on error
///
/// # Safety
/// The returned pointer must be freed using `pdf_string_free()`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_markdown_all(path: *const c_char) -> *mut c_char {
    match to_markdown_all_internal(path) {
        Ok(cstr) => cstr.into_raw(),
        Err(e) => {
            set_last_error(e);
            std::ptr::null_mut()
        }
    }
}

/// Internal implementation for single-page markdown conversion.
fn to_markdown_internal(
    path: *const c_char,
    page_index: i32,
) -> Result<CString, Box<dyn std::error::Error>> {
    let path_str = c_string_to_string(path, "path")?;
    let doc = PdfDocument::open(&path_str)?;
    let page_index = usize::try_from(page_index).map_err(|_| {
        IoError::new(ErrorKind::InvalidInput, "page index must be non-negative")
    })?;
    let md = doc.to_markdown(page_index, &Default::default())?;
    Ok(CString::new(md)?)
}

/// Internal implementation for all-pages markdown conversion.
fn to_markdown_all_internal(path: *const c_char) -> Result<CString, Box<dyn std::error::Error>> {
    let path_str = c_string_to_string(path, "path")?;
    let doc = PdfDocument::open(&path_str)?;
    let md = doc.to_markdown_all(&Default::default())?;
    Ok(CString::new(md)?)
}

// ============================================================================
// PAGE COUNT FUNCTION
// ============================================================================

/// Get the number of pages in a PDF file.
///
/// # Arguments
/// * `path` - Path to the PDF file as a null-terminated C string
///
/// # Returns
/// * Number of pages, or -1 if an error occurred
#[unsafe(export_name = "pdfoxide_csharp_pdf_get_page_count")]
pub extern "C" fn pdf_get_page_count(path: *const c_char) -> i32 {
    match get_page_count_internal(path) {
        Ok(count) => count as i32,
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

/// Internal implementation for getting page count.
fn get_page_count_internal(path: *const c_char) -> Result<usize, Box<dyn std::error::Error>> {
    let path_str = c_string_to_string(path, "path")?;
    let doc = PdfDocument::open(&path_str)?;
    Ok(doc.page_count()?)
}

// ============================================================================
// PDF CREATION FUNCTIONS
// ============================================================================

/// Create a PDF from Markdown content.
///
/// # Arguments
/// * `markdown` - Markdown content as a null-terminated C string
/// * `output_path` - Output file path as a null-terminated C string
///
/// # Returns
/// * 0 on success, -1 on error
#[unsafe(export_name = "pdfoxide_csharp_pdf_from_markdown")]
pub extern "C" fn pdf_from_markdown(markdown: *const c_char, output_path: *const c_char) -> i32 {
    match from_markdown_internal(markdown, output_path) {
        Ok(_) => 0,
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

/// Internal implementation for markdown-to-PDF conversion.
fn from_markdown_internal(
    markdown: *const c_char,
    output_path: *const c_char,
) -> Result<(), Box<dyn std::error::Error>> {
    let md_str = c_string_to_string(markdown, "markdown")?;
    let out_str = c_string_to_string(output_path, "output_path")?;
    let mut pdf = Pdf::from_markdown(&md_str)?;
    pdf.save(&out_str)?;
    Ok(())
}

/// Create a PDF from plain text content.
///
/// # Arguments
/// * `text` - Plain text content as a null-terminated C string
/// * `output_path` - Output file path as a null-terminated C string
///
/// # Returns
/// * 0 on success, -1 on error
#[unsafe(export_name = "pdfoxide_csharp_pdf_from_text")]
pub extern "C" fn pdf_from_text(text: *const c_char, output_path: *const c_char) -> i32 {
    match from_text_internal(text, output_path) {
        Ok(_) => 0,
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

/// Internal implementation for text-to-PDF conversion.
fn from_text_internal(
    text: *const c_char,
    output_path: *const c_char,
) -> Result<(), Box<dyn std::error::Error>> {
    let text_str = c_string_to_string(text, "text")?;
    let out_str = c_string_to_string(output_path, "output_path")?;
    let mut pdf = Pdf::from_text(&text_str)?;
    pdf.save(&out_str)?;
    Ok(())
}

/// Create a PDF from multiple image files.
/// Each image becomes a separate page in the output PDF.
///
/// # Arguments
/// * `paths` - Pointer to array of image file paths as C strings
/// * `len` - Number of images in the array
/// * `output_path` - Output file path as a null-terminated C string
///
/// # Returns
/// * 0 on success, -1 on error
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_images_to_pdf(
    paths: *const *const c_char,
    len: i32,
    output_path: *const c_char,
) -> i32 {
    match images_to_pdf_internal(paths, len, output_path) {
        Ok(_) => 0,
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

/// Internal implementation for images-to-PDF conversion.
fn images_to_pdf_internal(
    paths: *const *const c_char,
    len: i32,
    output_path: *const c_char,
) -> Result<(), Box<dyn std::error::Error>> {
    if len < 0 {
        return Err(IoError::new(ErrorKind::InvalidInput, "image count must not be negative").into());
    }
    if len > 0 && paths.is_null() {
        return Err(IoError::new(ErrorKind::InvalidInput, "image paths must not be null").into());
    }

    let out_str = c_string_to_string(output_path, "output_path")?;
    let mut image_paths = Vec::with_capacity(len as usize);

    // Extract all image paths from the pointer array
    for i in 0..len as usize {
        let path = unsafe { *paths.add(i) };
        image_paths.push(c_string_to_string(path, "image path")?);
    }

    let path_refs: Vec<&str> = image_paths.iter().map(|s| s.as_str()).collect();
    let mut pdf = Pdf::from_images(&path_refs)?;
    pdf.save(&out_str)?;
    Ok(())
}

// ============================================================================
// DOCUMENT BUILDER API
// ============================================================================

/// Create a new DocumentBuilder state for building PDFs programmatically.
///
/// # Returns
/// * Pointer to the builder state, or NULL if allocation fails
///
/// # Safety
/// The returned pointer must be freed using `pdf_builder_create()`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_create() -> *mut PdfBuilderState {
    Box::into_raw(Box::new(PdfBuilderState {
        builder: DocumentBuilder::new(),
    }))
}

/// Free a DocumentBuilder state.
///
/// # Arguments
/// * `state` - Pointer to the builder state to free
///
/// # Safety
/// This must only be called with a valid pointer returned by `pdf_builder_create()`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_free(state: *mut PdfBuilderState) {
    if !state.is_null() {
        // Convert pointer back to Box and drop it
        drop(unsafe { Box::from_raw(state) });
    }
}

/// Add a sample page with text content to the document.
///
/// # Arguments
/// * `state` - Pointer to the builder state
/// * `text` - Text content for the page as a null-terminated C string
///
/// # Returns
/// * 0 on success, -1 on error
///
/// # Note
/// Uses A4 page size. The DocumentBuilder uses a fluent API that consumes
/// the builder/page, so this provides a simplified wrapper for basic usage.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_add_sample_page(
    state: *mut PdfBuilderState,
    text: *const c_char,
) -> i32 {
    let text_str = match c_string_to_string(text, "text") {
        Ok(s) => s,
        Err(e) => {
            set_last_error(e);
            return -1;
        }
    };

    if state.is_null() {
        set_last_error("builder state must not be null");
        return -1;
    }

    if let Some(state) = unsafe { state.as_mut() } {
        // Add a page with A4 size and the provided text
        state.builder.page(PageSize::A4).text(&text_str);
        0
    } else {
        set_last_error("builder state must not be null");
        -1
    }
}

/// Save the document to a file and free the builder state.
///
/// # Arguments
/// * `state` - Pointer to the builder state (will be consumed)
/// * `path` - Output file path as a null-terminated C string
///
/// # Returns
/// * 0 on success, -1 on error
///
/// # Note
/// This function consumes the builder state - do not use the state pointer
/// after calling this function.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_save(state: *mut PdfBuilderState, path: *const c_char) -> i32 {
    if state.is_null() {
        set_last_error("builder state must not be null");
        -1
    } else {
        // This function consumes the state on every call with a non-null state,
        // including invalid paths and save failures. The managed wrapper must
        // invalidate its pointer whenever this function returns.
        let state = unsafe { Box::from_raw(state) };
        let path_str = match c_string_to_string(path, "path") {
            Ok(s) => s,
            Err(e) => {
                set_last_error(e);
                return -1;
            }
        };

        match state.builder.save(&path_str) {
            Ok(_) => 0,
            Err(e) => {
                set_last_error(e);
                -1
            }
        }
    }
}

// ============================================================================
// ERROR HANDLING FUNCTIONS
// ============================================================================

/// Retrieve the last error message.
///
/// # Returns
/// * Pointer to allocated C string containing the error message, or NULL if no error
///
/// # Safety
/// The returned pointer must be freed using `pdf_string_free()`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_last_error_message() -> *mut c_char {
    LAST_ERROR.with(|e| {
        if let Some(msg) = e.borrow().as_ref() {
            CString::new(msg.clone())
                .map(CString::into_raw)
                .unwrap_or(std::ptr::null_mut())
        } else {
            std::ptr::null_mut()
        }
    })
}

/// Free a string allocated by the FFI functions.
///
/// # Arguments
/// * `ptr` - Pointer to the string to free
///
/// # Safety
/// This must only be called with pointers returned by FFI functions
/// (not arbitrary memory).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_string_free(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }
    // Convert back to CString and drop (frees memory)
    drop(unsafe { CString::from_raw(ptr) });
}

/// Get the length of a string returned by FFI functions.
///
/// # Arguments
/// * `ptr` - Pointer to the string
///
/// # Returns
/// * Length in bytes, or 0 if ptr is NULL
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_get_string_len(ptr: *const c_char) -> i32 {
    if ptr.is_null() {
        return 0;
    }
    unsafe { CStr::from_ptr(ptr).to_bytes().len() as i32 }
}

/// Convert a nullable C string to an owned UTF-8 Rust string.
///
/// Non-null pointers must still point to readable, NUL-terminated memory, as
/// required by the C ABI contract.
fn c_string_to_string(
    ptr: *const c_char,
    argument: &str,
) -> Result<String, Box<dyn std::error::Error>> {
    if ptr.is_null() {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("{argument} must not be null"),
        )
        .into());
    }

    let value = unsafe { CStr::from_ptr(ptr) }.to_str()?;
    Ok(value.to_owned())
}

/// Internal helper to set the thread-local error message.
fn set_last_error(err: impl ToString) {
    LAST_ERROR.with(|e| {
        *e.borrow_mut() = Some(err.to_string());
    });
}
