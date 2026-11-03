#![no_main]

use libfuzzer_sys::fuzz_target;
use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};

use pdfoxide_bindings::{
    pdf_bytes_free, pdf_extract_chars, pdf_extract_embedded_files, pdf_extract_embedded_fonts,
    pdf_extract_images, pdf_extract_paths, pdf_extract_spans, pdf_extract_tables, pdf_extract_text,
    pdf_extract_text_lines, pdf_extract_text_page, pdf_extract_words, pdf_get_metadata,
    pdf_get_outline, pdf_get_page_count, pdf_get_page_info, pdf_get_page_labels, pdf_get_version,
    pdf_get_xmp_metadata, pdf_string_free, pdf_to_docx_bytes, pdf_to_html, pdf_to_html_all,
    pdf_to_markdown, pdf_to_markdown_all, pdf_to_plain_text, pdf_to_plain_text_all,
    pdf_to_pptx_bytes, pdf_to_xlsx_bytes,
};

static COUNTER: AtomicU64 = AtomicU64::new(0);

fn temp_path() -> PathBuf {
    let id = COUNTER.fetch_add(1, Ordering::Relaxed);
    let mut path = std::env::temp_dir();
    path.push(format!(
        "pdf_oxide_ffi_fuzz_{}_{}.pdf",
        std::process::id(),
        id
    ));
    path
}

fn to_c_path(path: &Path) -> CString {
    CString::new(path.to_string_lossy().as_bytes()).unwrap_or_default()
}

unsafe fn take_string(pointer: *mut c_char) {
    if pointer.is_null() {
        return;
    }
    let _ = unsafe { CStr::from_ptr(pointer) }.to_bytes();
    unsafe { pdf_string_free(pointer) };
}

unsafe fn take_bytes(data: *mut u8, length: i32) {
    if data.is_null() || length <= 0 {
        return;
    }
    let _ = unsafe { std::slice::from_raw_parts(data, length as usize) };
    unsafe { pdf_bytes_free(data, length) };
}

unsafe fn call_bytes<F>(operation: F)
where
    F: FnOnce(*mut *mut u8, *mut i32) -> i32,
{
    let mut data: *mut u8 = std::ptr::null_mut();
    let mut length: i32 = 0;
    let status = operation(&mut data, &mut length);
    if status == 0 {
        unsafe { take_bytes(data, length) };
    }
}

fuzz_target!(|data: &[u8]| {
    if data.len() < 3 {
        return;
    }

    let selector = usize::from(data[0]) % 27;
    let page = i32::from(data[1]) - 128;

    let path = temp_path();
    if std::fs::write(&path, &data[2..]).is_err() {
        return;
    }

    let c_path = to_c_path(&path);
    let path_pointer = c_path.as_ptr();

    unsafe {
        match selector {
            0 => take_string(pdf_extract_text(path_pointer)),
            1 => take_string(pdf_to_markdown(path_pointer, page)),
            2 => take_string(pdf_to_markdown_all(path_pointer)),
            3 => {
                let _ = pdf_get_page_count(path_pointer);
            }
            4 => take_string(pdf_extract_text_page(path_pointer, page)),
            5 => take_string(pdf_to_plain_text(path_pointer, page)),
            6 => take_string(pdf_to_plain_text_all(path_pointer)),
            7 => take_string(pdf_to_html(path_pointer, page)),
            8 => take_string(pdf_to_html_all(path_pointer)),
            9 => take_string(pdf_extract_images(path_pointer, page)),
            10 => take_string(pdf_extract_embedded_files(path_pointer)),
            11 => take_string(pdf_extract_embedded_fonts(path_pointer)),
            12 => take_string(pdf_extract_tables(path_pointer, page)),
            13 => take_string(pdf_extract_words(path_pointer, page)),
            14 => take_string(pdf_extract_text_lines(path_pointer, page)),
            15 => take_string(pdf_extract_chars(path_pointer, page)),
            16 => take_string(pdf_extract_spans(path_pointer, page)),
            17 => take_string(pdf_extract_paths(path_pointer, page)),
            18 => take_string(pdf_get_version(path_pointer)),
            19 => take_string(pdf_get_page_info(path_pointer, page)),
            20 => take_string(pdf_get_metadata(path_pointer)),
            21 => take_string(pdf_get_xmp_metadata(path_pointer)),
            22 => take_string(pdf_get_outline(path_pointer)),
            23 => take_string(pdf_get_page_labels(path_pointer)),
            24 => call_bytes(|out, len| pdf_to_docx_bytes(path_pointer, out, len)),
            25 => call_bytes(|out, len| pdf_to_pptx_bytes(path_pointer, out, len)),
            26 => call_bytes(|out, len| pdf_to_xlsx_bytes(path_pointer, out, len)),
            _ => unreachable!(),
        }
    }

    let _ = std::fs::remove_file(&path);
});
