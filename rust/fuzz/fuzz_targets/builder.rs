#![no_main]

use libfuzzer_sys::fuzz_target;
use std::ffi::CString;
use std::os::raw::c_char;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};

use pdfoxide_bindings::{
    pdf_builder_add_barcode, pdf_builder_add_checkbox, pdf_builder_add_circle,
    pdf_builder_add_code_block, pdf_builder_add_columns, pdf_builder_add_footnote,
    pdf_builder_add_heading, pdf_builder_add_horizontal_rule, pdf_builder_add_image_bytes,
    pdf_builder_add_line, pdf_builder_add_link_url, pdf_builder_add_list, pdf_builder_add_page,
    pdf_builder_add_paragraph, pdf_builder_add_qr_code, pdf_builder_add_rect,
    pdf_builder_add_rich_paragraph, pdf_builder_add_role_map, pdf_builder_add_space,
    pdf_builder_add_stamp, pdf_builder_add_table, pdf_builder_add_text, pdf_builder_add_text_at,
    pdf_builder_add_text_field, pdf_builder_add_watermark, pdf_builder_build,
    pdf_builder_bytes_free, pdf_builder_create, pdf_builder_free,
    pdf_builder_register_font_bytes, pdf_builder_save, pdf_builder_set_language,
    pdf_builder_set_metadata, pdf_builder_set_tagged_pdf_ua1,
    pdf_builder_streaming_table_add_row, pdf_builder_streaming_table_begin,
    pdf_builder_streaming_table_end,
};

static COUNTER: AtomicU64 = AtomicU64::new(0);

// pdf_oxide 0.3.78 can loop allocating while paginating a streaming table on a
// page smaller than its calculated row/header height. Skip streaming tables on
// small pages until the upstream layout bug is fixed; remove this workaround
// once it lands.
const MIN_STREAMING_TABLE_PAGE: f32 = 300.0;

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
        let requested = usize::from(self.next_u8());
        let start = self.position.min(self.data.len());
        let remaining = self.data.len() - start;
        let length = (requested % (maximum + 1)).min(remaining);
        let end = start + length;
        let slice = &self.data[start..end];
        self.position = end;
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
    let mut page_min_dims: Vec<f32> = Vec::new();

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

            match cursor.next_u8() % 21 {
                0 => {
                    let kind = i32::from(cursor.next_u8() % 6);
                    let width = cursor.next_f32();
                    let height = cursor.next_f32();
                    if pdf_builder_add_page(state, kind, width, height) == 0 {
                        pages += 1;
                        let (page_width, page_height) = match kind {
                            0 => (595.28, 841.89),
                            1 => (612.0, 792.0),
                            2 => (612.0, 1008.0),
                            3 => (841.89, 1190.55),
                            _ => (width, height),
                        };
                        page_min_dims.push(page_width.min(page_height));
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
                13 => {
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
                14 => {
                    let header0 = to_c_string(cursor.next_bytes(16));
                    let header1 = to_c_string(cursor.next_bytes(16));
                    let index = page_index(&mut cursor, pages);
                    let page_too_small = usize::try_from(index)
                        .ok()
                        .and_then(|index| page_min_dims.get(index))
                        .is_some_and(|minimum| *minimum < MIN_STREAMING_TABLE_PAGE);
                    if !page_too_small {
                        let headers = [header0.as_ptr(), header1.as_ptr()];
                        let widths = [50.0_f32, 100.0_f32];
                        let aligns = [0_i32, 2_i32];
                        let begun = pdf_builder_streaming_table_begin(
                            state,
                            index,
                            headers.as_ptr(),
                            widths.as_ptr(),
                            aligns.as_ptr(),
                            2,
                            1,
                            0,
                            0.0,
                            0.0,
                            0.0,
                            0,
                            0.0,
                            0.0,
                            0.0,
                            0.0,
                            2.0,
                            1.0,
                            1.0,
                            1,
                        );
                        if begun == 0 {
                            let rows = usize::from(cursor.next_u8() % 4);
                            for _ in 0..rows {
                                let cell0 = to_c_string(cursor.next_bytes(16));
                                let cell1 = to_c_string(cursor.next_bytes(16));
                                let cells = [cell0.as_ptr(), cell1.as_ptr()];
                                let spans = [1_i32, 1_i32];
                                let _ = pdf_builder_streaming_table_add_row(
                                    state,
                                    cells.as_ptr(),
                                    spans.as_ptr(),
                                    2,
                                );
                            }
                            if cursor.next_u8() % 2 == 0 {
                                let _ = pdf_builder_streaming_table_end(state);
                            }
                        }
                    }
                }
                15 => {
                    let name = to_c_string(cursor.next_bytes(16));
                    let index = page_index(&mut cursor, pages);
                    let x = cursor.next_f32();
                    let y = cursor.next_f32();
                    let width = cursor.next_f32();
                    let height = cursor.next_f32();
                    match cursor.next_u8() % 2 {
                        0 => {
                            let _ = pdf_builder_add_text_field(
                                state,
                                index,
                                name.as_ptr(),
                                x,
                                y,
                                width,
                                height,
                                std::ptr::null(),
                            );
                        }
                        _ => {
                            let checked = i32::from(cursor.next_u8() % 2);
                            let _ = pdf_builder_add_checkbox(
                                state,
                                index,
                                name.as_ptr(),
                                x,
                                y,
                                width,
                                height,
                                checked,
                            );
                        }
                    }
                }
                16 => {
                    let text = to_c_string(cursor.next_bytes(32));
                    let index = page_index(&mut cursor, pages);
                    match cursor.next_u8() % 4 {
                        0 => {
                            let _ = pdf_builder_add_link_url(state, index, text.as_ptr());
                        }
                        1 => {
                            let _ = pdf_builder_add_watermark(state, index, text.as_ptr());
                        }
                        2 => {
                            let _ = pdf_builder_add_stamp(state, index, text.as_ptr());
                        }
                        _ => {
                            let reference = to_c_string(cursor.next_bytes(8));
                            let _ =
                                pdf_builder_add_footnote(state, index, reference.as_ptr(), text.as_ptr());
                        }
                    }
                }
                17 => {
                    let index = page_index(&mut cursor, pages);
                    let x = cursor.next_f32();
                    let y = cursor.next_f32();
                    let width = cursor.next_f32().abs();
                    let height = cursor.next_f32().abs();
                    match cursor.next_u8() % 3 {
                        0 => {
                            let _ = pdf_builder_add_rect(state, index, x, y, width, height);
                        }
                        1 => {
                            let _ = pdf_builder_add_line(state, index, x, y, x + width, y + height);
                        }
                        _ => {
                            let radius = width.min(100.0);
                            let _ = pdf_builder_add_circle(
                                state,
                                index,
                                x,
                                y,
                                radius,
                                1,
                                1.0,
                                0.0,
                                0.0,
                                0.0,
                                std::ptr::null(),
                                0,
                                0.0,
                                0,
                                0.0,
                                0.0,
                                0.0,
                            );
                        }
                    }
                }
                18 => {
                    let data = to_c_string(cursor.next_bytes(32));
                    let index = page_index(&mut cursor, pages);
                    let x = cursor.next_f32();
                    let y = cursor.next_f32();
                    let size = cursor.next_f32().abs().min(200.0);
                    match cursor.next_u8() % 2 {
                        0 => {
                            let _ = pdf_builder_add_qr_code(state, index, data.as_ptr(), x, y, size);
                        }
                        _ => {
                            let barcode_type = i32::from(cursor.next_u8() % 8);
                            let _ = pdf_builder_add_barcode(
                                state,
                                index,
                                barcode_type,
                                data.as_ptr(),
                                x,
                                y,
                                size,
                                size,
                            );
                        }
                    }
                }
                19 => {
                    let name = to_c_string(cursor.next_bytes(16));
                    let font = cursor.next_bytes(64);
                    let _ = pdf_builder_register_font_bytes(
                        state,
                        name.as_ptr(),
                        font.as_ptr(),
                        font.len() as i32,
                    );
                }
                _ => {
                    let language = to_c_string(cursor.next_bytes(8));
                    let custom = to_c_string(cursor.next_bytes(16));
                    let standard = to_c_string(cursor.next_bytes(16));
                    let _ = pdf_builder_set_tagged_pdf_ua1(state, i32::from(cursor.next_u8() % 2));
                    let _ = pdf_builder_set_language(state, language.as_ptr());
                    let _ = pdf_builder_add_role_map(state, custom.as_ptr(), standard.as_ptr());
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
