#![no_main]

use libfuzzer_sys::fuzz_target;
use std::ffi::CString;
use std::os::raw::c_char;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};

use pdfoxide_bindings::{
    pdf_builder_add_code_block, pdf_builder_add_columns, pdf_builder_add_heading,
    pdf_builder_add_horizontal_rule, pdf_builder_add_image_bytes, pdf_builder_add_list,
    pdf_builder_add_page, pdf_builder_add_paragraph, pdf_builder_add_rich_paragraph,
    pdf_builder_add_space, pdf_builder_add_table, pdf_builder_add_text, pdf_builder_add_text_at,
    pdf_builder_build, pdf_builder_bytes_free, pdf_builder_create, pdf_builder_free,
    pdf_builder_save, pdf_builder_set_metadata,
};

static COUNTER: AtomicU64 = AtomicU64::new(0);

struct Cursor<'a> {
    data: &'a [u8],
    position: usize,
}

impl<'a> Cursor<'a> {
    fn new(data: &'a [u8]) -> Self {
        Self { data, position: 0 }
    }

    fn finished(&self) -> bool {
        self.position >= self.data.len()
    }

    fn next_u8(&mut self) -> u8 {
        let value = self.data.get(self.position).copied().unwrap_or(0);
        self.position = self.position.saturating_add(1);
        value
    }

    fn next_f32(&mut self) -> f32 {
        f32::from_bits(u32::from_le_bytes([
            self.next_u8(),
            self.next_u8(),
            self.next_u8(),
            self.next_u8(),
        ]))
    }

    fn next_bytes(&mut self, maximum: usize) -> &'a [u8] {
        let remaining = self.data.len().saturating_sub(self.position);
        let requested = usize::from(self.next_u8());
        let length = (requested % (maximum + 1)).min(remaining);
        let slice = &self.data[self.position..self.position + length];
        self.position += length;
        slice
    }
}

fn page_index(cursor: &mut Cursor<'_>, pages: i32) -> i32 {
    let raw = i32::from(cursor.next_u8());
    (raw % (pages + 2)) - 1
}

fn to_c_string(bytes: &[u8]) -> CString {
    let filtered: Vec<u8> = bytes.iter().copied().filter(|value| *value != 0).collect();
    CString::new(filtered).unwrap_or_default()
}

fn temp_path() -> PathBuf {
    let id = COUNTER.fetch_add(1, Ordering::Relaxed);
    let mut path = std::env::temp_dir();
    path.push(format!(
        "pdf_oxide_ffi_builder_fuzz_{}_{}.pdf",
        std::process::id(),
        id
    ));
    path
}

fn to_c_path(path: &Path) -> CString {
    CString::new(path.to_string_lossy().as_bytes()).unwrap_or_default()
}

fuzz_target!(|data: &[u8]| {
    if data.is_empty() {
        return;
    }

    let mut cursor = Cursor::new(data);
    let mut pages: i32 = 0;

    unsafe {
        let state = pdf_builder_create();
        if state.is_null() {
            return;
        }

        let operations = 1 + usize::from(cursor.next_u8() % 64);
        for _ in 0..operations {
            if cursor.finished() {
                break;
            }

            match cursor.next_u8() % 14 {
                0 => {
                    let kind = i32::from(cursor.next_u8() % 6);
                    let width = cursor.next_f32();
                    let height = cursor.next_f32();
                    if pdf_builder_add_page(state, kind, width, height) == 0 {
                        pages += 1;
                    }
                }
                1 => {
                    let text = to_c_string(cursor.next_bytes(64));
                    let index = page_index(&mut cursor, pages);
                    let size = cursor.next_f32();
                    let _ = pdf_builder_add_text(state, index, text.as_ptr(), std::ptr::null(), size);
                }
                2 => {
                    let text = to_c_string(cursor.next_bytes(64));
                    let index = page_index(&mut cursor, pages);
                    let x = cursor.next_f32();
                    let y = cursor.next_f32();
                    let size = cursor.next_f32();
                    let _ = pdf_builder_add_text_at(
                        state,
                        index,
                        x,
                        y,
                        text.as_ptr(),
                        std::ptr::null(),
                        size,
                    );
                }
                3 => {
                    let text = to_c_string(cursor.next_bytes(64));
                    let index = page_index(&mut cursor, pages);
                    let level = i32::from(cursor.next_u8() % 9);
                    let _ = pdf_builder_add_heading(state, index, level, text.as_ptr());
                }
                4 => {
                    let text = to_c_string(cursor.next_bytes(128));
                    let index = page_index(&mut cursor, pages);
                    let _ = pdf_builder_add_paragraph(state, index, text.as_ptr());
                }
                5 => {
                    let items = to_c_string(cursor.next_bytes(128));
                    let index = page_index(&mut cursor, pages);
                    let numbered = i32::from(cursor.next_u8() % 2);
                    let style = i32::from(cursor.next_u8() % 4);
                    let _ = pdf_builder_add_list(state, index, items.as_ptr(), numbered, style);
                }
                6 => {
                    let language = to_c_string(cursor.next_bytes(16));
                    let source = to_c_string(cursor.next_bytes(128));
                    let index = page_index(&mut cursor, pages);
                    let _ =
                        pdf_builder_add_code_block(state, index, language.as_ptr(), source.as_ptr());
                }
                7 => {
                    let first = to_c_string(cursor.next_bytes(32));
                    let second = to_c_string(cursor.next_bytes(32));
                    let third = to_c_string(cursor.next_bytes(32));
                    let texts = [first.as_ptr(), second.as_ptr(), third.as_ptr()];
                    let styles = [0_i32, 1, 3];
                    let colors = [1.0_f32, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 1.0];
                    let index = page_index(&mut cursor, pages);
                    let _ = pdf_builder_add_rich_paragraph(
                        state,
                        index,
                        texts.as_ptr(),
                        styles.as_ptr(),
                        colors.as_ptr(),
                        3,
                    );
                }
                8 => {
                    let points = cursor.next_f32();
                    let index = page_index(&mut cursor, pages);
                    let _ = pdf_builder_add_space(state, index, points);
                }
                9 => {
                    let index = page_index(&mut cursor, pages);
                    let _ = pdf_builder_add_horizontal_rule(state, index);
                }
                10 => {
                    let text = to_c_string(cursor.next_bytes(128));
                    let index = page_index(&mut cursor, pages);
                    let count = i32::from(cursor.next_u8() % 8);
                    let gap = cursor.next_f32();
                    let _ = pdf_builder_add_columns(state, index, count, gap, text.as_ptr());
                }
                11 => {
                    let rows = 1 + i32::from(cursor.next_u8() % 4);
                    let cols = 1 + i32::from(cursor.next_u8() % 4);
                    let count = (rows * cols) as usize;
                    let mut cells: Vec<CString> = Vec::with_capacity(count);
                    for _ in 0..count {
                        cells.push(to_c_string(cursor.next_bytes(16)));
                    }
                    let pointers: Vec<*const c_char> =
                        cells.iter().map(|cell| cell.as_ptr()).collect();
                    let index = page_index(&mut cursor, pages);
                    let header = i32::from(cursor.next_u8() % 2);
                    let style = i32::from(cursor.next_u8() % 4);
                    let _ = pdf_builder_add_table(
                        state,
                        index,
                        pointers.as_ptr(),
                        rows,
                        cols,
                        header,
                        style,
                    );
                }
                12 => {
                    let image = cursor.next_bytes(256);
                    let index = page_index(&mut cursor, pages);
                    let x = cursor.next_f32();
                    let y = cursor.next_f32();
                    let width = cursor.next_f32();
                    let height = cursor.next_f32();
                    let _ = pdf_builder_add_image_bytes(
                        state,
                        index,
                        image.as_ptr(),
                        image.len() as i32,
                        x,
                        y,
                        width,
                        height,
                        std::ptr::null(),
                    );
                }
                _ => {
                    let title = to_c_string(cursor.next_bytes(32));
                    let author = to_c_string(cursor.next_bytes(32));
                    let _ = pdf_builder_set_metadata(
                        state,
                        title.as_ptr(),
                        author.as_ptr(),
                        std::ptr::null(),
                        std::ptr::null(),
                        std::ptr::null(),
                    );
                }
            }
        }

        let mut data_pointer: *mut u8 = std::ptr::null_mut();
        let mut length: i32 = 0;
        if pdf_builder_build(state, &mut data_pointer, &mut length) == 0
            && !data_pointer.is_null()
            && length > 0
        {
            let _ = std::slice::from_raw_parts(data_pointer, length as usize);
            pdf_builder_bytes_free(data_pointer, length);
        }

        if cursor.next_u8() % 2 == 0 {
            let path = temp_path();
            let c_path = to_c_path(&path);
            let _ = pdf_builder_save(state, c_path.as_ptr());
            let _ = std::fs::remove_file(&path);
        }

        pdf_builder_free(state);
    }
});
