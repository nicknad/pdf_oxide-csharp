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

use pdf_oxide::PdfDocument;
use pdf_oxide::api::Pdf;
use pdf_oxide::geometry::Rect;
use pdf_oxide::writer::{
    DocumentBuilder as PdfWriterBuilder, FluentPageBuilder, ListStyle, PageSize, Table, TableCell,
    TableRow, TableStyle, TextRun, TextRunStyle,
};
use std::cell::RefCell;
use std::ffi::{CStr, CString};
use std::io::{Error as IoError, ErrorKind};
use std::os::raw::c_char;

// Thread-local storage for error messages, retained so callers can retrieve the
// failure detail after an FFI function returns.
thread_local! {
    static LAST_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
}

const PAGE_KIND_A4: i32 = 0;
const PAGE_KIND_LETTER: i32 = 1;
const PAGE_KIND_LEGAL: i32 = 2;
const PAGE_KIND_A3: i32 = 3;
const PAGE_KIND_CUSTOM: i32 = 4;

#[derive(Clone, Copy)]
enum PageKind {
    A4,
    Letter,
    Legal,
    A3,
    Custom { width: f32, height: f32 },
}

impl PageKind {
    fn from_values(kind: i32, width: f32, height: f32) -> Result<Self, Box<dyn std::error::Error>> {
        match kind {
            PAGE_KIND_A4 => Ok(Self::A4),
            PAGE_KIND_LETTER => Ok(Self::Letter),
            PAGE_KIND_LEGAL => Ok(Self::Legal),
            PAGE_KIND_A3 => Ok(Self::A3),
            PAGE_KIND_CUSTOM => {
                if !width.is_finite() || !height.is_finite() || width <= 0.0 || height <= 0.0 {
                    return Err(IoError::new(
                        ErrorKind::InvalidInput,
                        "custom page dimensions must be positive finite numbers",
                    )
                    .into());
                }
                Ok(Self::Custom { width, height })
            }
            other => Err(IoError::new(
                ErrorKind::InvalidInput,
                format!("unknown page size code: {other}"),
            )
            .into()),
        }
    }

    fn page_size(self) -> PageSize {
        match self {
            Self::A4 => PageSize::A4,
            Self::Letter => PageSize::Letter,
            Self::Legal => PageSize::Legal,
            Self::A3 => PageSize::A3,
            Self::Custom { width, height } => PageSize::Custom(width, height),
        }
    }
}

enum TextRunKind {
    Normal,
    Bold,
    Italic,
    Color { r: f32, g: f32, b: f32 },
}

enum BuilderOp {
    Text {
        x: Option<f32>,
        y: Option<f32>,
        text: String,
        font: Option<String>,
        size: Option<f32>,
    },
    Heading {
        level: u8,
        text: String,
    },
    Paragraph(String),
    BulletList(Vec<String>),
    NumberedList {
        items: Vec<String>,
        style: ListStyle,
    },
    CodeBlock {
        language: String,
        source: String,
    },
    RichParagraph(Vec<(String, TextRunKind)>),
    Space(f32),
    HorizontalRule,
    Columns {
        count: u32,
        gap: f32,
        text: String,
    },
    Table {
        rows: Vec<Vec<String>>,
        header: bool,
        style: i32,
    },
    Image {
        data: Vec<u8>,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        alt: Option<String>,
    },
}

struct PageSpec {
    kind: PageKind,
    ops: Vec<BuilderOp>,
}

#[derive(Default)]
struct BuilderMetadata {
    title: Option<String>,
    author: Option<String>,
    subject: Option<String>,
    keywords: Option<String>,
    creator: Option<String>,
}

/// State container for the DocumentBuilder API.
/// Holds the document model accumulated across FFI calls; the PDF is rendered
/// from the model on save/build so callers can add many elements per page.
pub struct PdfBuilderState {
    pages: Vec<PageSpec>,
    metadata: BuilderMetadata,
}

impl PdfBuilderState {
    fn current_page_mut(&mut self) -> Result<&mut PageSpec, Box<dyn std::error::Error>> {
        self.pages.last_mut().ok_or_else(|| {
            IoError::new(ErrorKind::InvalidInput, "add a page before adding content").into()
        })
    }

    fn build_bytes(&self) -> Result<Vec<u8>, Box<dyn std::error::Error>> {
        let mut document = PdfWriterBuilder::new();
        if let Some(value) = &self.metadata.title {
            document = document.title(value);
        }
        if let Some(value) = &self.metadata.author {
            document = document.author(value);
        }
        if let Some(value) = &self.metadata.subject {
            document = document.subject(value);
        }
        if let Some(value) = &self.metadata.keywords {
            document = document.keywords(value);
        }
        if let Some(value) = &self.metadata.creator {
            document = document.creator(value);
        }

        for page in &self.pages {
            let mut page_builder = document.page(page.kind.page_size());
            for op in &page.ops {
                page_builder = render_op(page_builder, op)?;
            }
            page_builder.done();
        }

        Ok(document.build()?)
    }
}

fn render_op<'a>(
    mut page: FluentPageBuilder<'a>,
    op: &BuilderOp,
) -> Result<FluentPageBuilder<'a>, Box<dyn std::error::Error>> {
    match op {
        BuilderOp::Text {
            x,
            y,
            text,
            font,
            size,
        } => {
            if let (Some(x), Some(y)) = (x, y) {
                page = page.at(*x, *y);
            }
            if let Some(font) = font {
                page = page.font(font, size.unwrap_or(12.0));
            } else if let Some(size) = size {
                page = page.font("Helvetica", *size);
            }
            page = page.text(text);
        }
        BuilderOp::Heading { level, text } => {
            page = page.heading(*level, text);
        }
        BuilderOp::Paragraph(text) => {
            page = page.paragraph(text);
        }
        BuilderOp::BulletList(items) => {
            page = page.bullet_list(items);
        }
        BuilderOp::NumberedList { items, style } => {
            page = page.numbered_list(items, *style);
        }
        BuilderOp::CodeBlock { language, source } => {
            page = page.code_block(language, source);
        }
        BuilderOp::RichParagraph(runs) => {
            let text_runs: Vec<TextRun> = runs
                .iter()
                .map(|(text, kind)| {
                    let style = match kind {
                        TextRunKind::Normal => TextRunStyle::Normal,
                        TextRunKind::Bold => TextRunStyle::Bold,
                        TextRunKind::Italic => TextRunStyle::Italic,
                        TextRunKind::Color { r, g, b } => TextRunStyle::Color {
                            r: *r,
                            g: *g,
                            b: *b,
                        },
                    };
                    TextRun {
                        text: text.clone(),
                        style,
                    }
                })
                .collect();
            page = page.rich_paragraph(&text_runs);
        }
        BuilderOp::Space(points) => {
            page = page.space(*points);
        }
        BuilderOp::HorizontalRule => {
            page = page.horizontal_rule();
        }
        BuilderOp::Columns { count, gap, text } => {
            page = page.columns(*count, *gap, text);
        }
        BuilderOp::Table {
            rows,
            header,
            style,
        } => {
            let table_rows: Vec<TableRow> = rows
                .iter()
                .enumerate()
                .map(|(index, row)| {
                    let cells: Vec<TableCell> = row
                        .iter()
                        .map(|cell| TableCell::text(cell.clone()))
                        .collect();
                    if *header && index == 0 {
                        TableRow::header(cells)
                    } else {
                        TableRow::new(cells)
                    }
                })
                .collect();
            let mut table = Table::from_rows(table_rows);
            table = match *style {
                1 => table.with_style(TableStyle::bordered()),
                2 => table.with_style(TableStyle::minimal()),
                _ => table,
            };
            page = page.table(table);
        }
        BuilderOp::Image {
            data,
            x,
            y,
            width,
            height,
            alt,
        } => {
            let rect = Rect::new(*x, *y, *width, *height);
            page = match alt {
                Some(alt_text) => page.image_from_bytes_with_alt(data, rect, alt_text)?,
                None => page.image_from_bytes(data, rect)?,
            };
        }
    }

    Ok(page)
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
    let page_index = usize::try_from(page_index)
        .map_err(|_| IoError::new(ErrorKind::InvalidInput, "page index must be non-negative"))?;
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
        return Err(
            IoError::new(ErrorKind::InvalidInput, "image count must not be negative").into(),
        );
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

fn builder_mut<'a>(
    state: *mut PdfBuilderState,
) -> Result<&'a mut PdfBuilderState, Box<dyn std::error::Error>> {
    if state.is_null() {
        return Err(IoError::new(ErrorKind::InvalidInput, "builder state must not be null").into());
    }
    Ok(unsafe { &mut *state })
}

fn with_builder<F>(state: *mut PdfBuilderState, action: F) -> i32
where
    F: FnOnce(&mut PdfBuilderState) -> Result<(), Box<dyn std::error::Error>>,
{
    let result = match builder_mut(state) {
        Ok(state) => action(state),
        Err(e) => Err(e),
    };

    match result {
        Ok(()) => 0,
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

fn optional_string(
    ptr: *const c_char,
    argument: &str,
) -> Result<Option<String>, Box<dyn std::error::Error>> {
    if ptr.is_null() {
        Ok(None)
    } else {
        Ok(Some(c_string_to_string(ptr, argument)?))
    }
}

fn validate_rect(
    x: f32,
    y: f32,
    width: f32,
    height: f32,
) -> Result<(), Box<dyn std::error::Error>> {
    if !x.is_finite() || !y.is_finite() || !width.is_finite() || !height.is_finite() {
        return Err(
            IoError::new(ErrorKind::InvalidInput, "rectangle values must be finite").into(),
        );
    }
    if width <= 0.0 || height <= 0.0 {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "rectangle dimensions must be positive",
        )
        .into());
    }
    Ok(())
}

fn list_style_from_code(style: i32) -> Result<ListStyle, Box<dyn std::error::Error>> {
    match style {
        0 => Ok(ListStyle::Decimal),
        1 => Ok(ListStyle::RomanLower),
        2 => Ok(ListStyle::AlphaLower),
        other => Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("unknown list style code: {other}"),
        )
        .into()),
    }
}

/// Create a new DocumentBuilder state for building PDFs programmatically.
///
/// # Returns
/// * Pointer to the builder state, or NULL if allocation fails
///
/// # Safety
/// The returned pointer must be freed using `pdf_builder_free()`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_create() -> *mut PdfBuilderState {
    Box::into_raw(Box::new(PdfBuilderState {
        pages: Vec::new(),
        metadata: BuilderMetadata::default(),
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
        drop(unsafe { Box::from_raw(state) });
    }
}

/// Set document metadata fields. Null string pointers leave that field unset.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_set_metadata(
    state: *mut PdfBuilderState,
    title: *const c_char,
    author: *const c_char,
    subject: *const c_char,
    keywords: *const c_char,
    creator: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let title = optional_string(title, "title")?;
        let author = optional_string(author, "author")?;
        let subject = optional_string(subject, "subject")?;
        let keywords = optional_string(keywords, "keywords")?;
        let creator = optional_string(creator, "creator")?;

        if let Some(value) = title {
            state.metadata.title = Some(value);
        }
        if let Some(value) = author {
            state.metadata.author = Some(value);
        }
        if let Some(value) = subject {
            state.metadata.subject = Some(value);
        }
        if let Some(value) = keywords {
            state.metadata.keywords = Some(value);
        }
        if let Some(value) = creator {
            state.metadata.creator = Some(value);
        }
        Ok(())
    })
}

/// Add a page and make it the target of subsequent content calls.
///
/// `kind` is 0=A4, 1=Letter, 2=Legal, 3=A3, 4=Custom.
/// `width` and `height` are only used for custom pages, in points.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_page(
    state: *mut PdfBuilderState,
    kind: i32,
    width: f32,
    height: f32,
) -> i32 {
    with_builder(state, |state| {
        let kind = PageKind::from_values(kind, width, height)?;
        state.pages.push(PageSpec {
            kind,
            ops: Vec::new(),
        });
        Ok(())
    })
}

/// Add text at the current cursor position. A null font uses the default.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_text(
    state: *mut PdfBuilderState,
    text: *const c_char,
    font: *const c_char,
    size: f32,
) -> i32 {
    with_builder(state, |state| {
        let text = c_string_to_string(text, "text")?;
        let font = optional_string(font, "font")?;
        let size = (size.is_finite() && size > 0.0).then_some(size);
        state.current_page_mut()?.ops.push(BuilderOp::Text {
            x: None,
            y: None,
            text,
            font,
            size,
        });
        Ok(())
    })
}

/// Add text at an absolute position, in PDF points.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_text_at(
    state: *mut PdfBuilderState,
    x: f32,
    y: f32,
    text: *const c_char,
    font: *const c_char,
    size: f32,
) -> i32 {
    with_builder(state, |state| {
        if !x.is_finite() || !y.is_finite() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "text position must be finite").into(),
            );
        }
        let text = c_string_to_string(text, "text")?;
        let font = optional_string(font, "font")?;
        let size = (size.is_finite() && size > 0.0).then_some(size);
        state.current_page_mut()?.ops.push(BuilderOp::Text {
            x: Some(x),
            y: Some(y),
            text,
            font,
            size,
        });
        Ok(())
    })
}

/// Add a heading at the current cursor position. Levels are 1 through 6.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_heading(
    state: *mut PdfBuilderState,
    level: i32,
    text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        if !(1..=6).contains(&level) {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "heading level must be between 1 and 6",
            )
            .into());
        }
        let text = c_string_to_string(text, "text")?;
        state.current_page_mut()?.ops.push(BuilderOp::Heading {
            level: level as u8,
            text,
        });
        Ok(())
    })
}

/// Add a wrapped paragraph at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_paragraph(
    state: *mut PdfBuilderState,
    text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let text = c_string_to_string(text, "text")?;
        state
            .current_page_mut()?
            .ops
            .push(BuilderOp::Paragraph(text));
        Ok(())
    })
}

/// Add a bullet or numbered list. Items are newline-separated.
/// `numbered` is 0 for bullets; `style` is 0=decimal, 1=roman, 2=alpha.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_list(
    state: *mut PdfBuilderState,
    items: *const c_char,
    numbered: i32,
    style: i32,
) -> i32 {
    with_builder(state, |state| {
        let text = c_string_to_string(items, "items")?;
        let values: Vec<String> = text.lines().map(str::to_owned).collect();
        if values.is_empty() {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "list must contain at least one item",
            )
            .into());
        }

        let op = if numbered == 0 {
            BuilderOp::BulletList(values)
        } else {
            BuilderOp::NumberedList {
                items: values,
                style: list_style_from_code(style)?,
            }
        };
        state.current_page_mut()?.ops.push(op);
        Ok(())
    })
}

/// Add a monospace code block at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_code_block(
    state: *mut PdfBuilderState,
    language: *const c_char,
    source: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let language = c_string_to_string(language, "language")?;
        let source = c_string_to_string(source, "source")?;
        state
            .current_page_mut()?
            .ops
            .push(BuilderOp::CodeBlock { language, source });
        Ok(())
    })
}

/// Add a paragraph made of styled text runs.
///
/// `styles` uses 0=normal, 1=bold, 2=italic, 3=color. `colors` is a flat
/// RGB array (3 floats per run) and must be provided when any run is colored.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_add_rich_paragraph(
    state: *mut PdfBuilderState,
    texts: *const *const c_char,
    styles: *const i32,
    colors: *const f32,
    len: i32,
) -> i32 {
    with_builder(state, |state| {
        if len < 0 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "text run count must not be negative",
            )
            .into());
        }
        if len == 0 {
            return Ok(());
        }
        if texts.is_null() || styles.is_null() {
            return Err(IoError::new(ErrorKind::InvalidInput, "text runs must not be null").into());
        }

        let mut runs = Vec::with_capacity(len as usize);
        for index in 0..len as usize {
            let text_ptr = unsafe { *texts.add(index) };
            let style_code = unsafe { *styles.add(index) };
            let text = c_string_to_string(text_ptr, "text run")?;
            let style = match style_code {
                0 => TextRunKind::Normal,
                1 => TextRunKind::Bold,
                2 => TextRunKind::Italic,
                3 => {
                    if colors.is_null() {
                        return Err(IoError::new(
                            ErrorKind::InvalidInput,
                            "colors must not be null when a run uses the color style",
                        )
                        .into());
                    }
                    let base = index * 3;
                    TextRunKind::Color {
                        r: unsafe { *colors.add(base) },
                        g: unsafe { *colors.add(base + 1) },
                        b: unsafe { *colors.add(base + 2) },
                    }
                }
                other => {
                    return Err(IoError::new(
                        ErrorKind::InvalidInput,
                        format!("unknown text run style code: {other}"),
                    )
                    .into());
                }
            };
            runs.push((text, style));
        }

        state
            .current_page_mut()?
            .ops
            .push(BuilderOp::RichParagraph(runs));
        Ok(())
    })
}

/// Add vertical space at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_space(state: *mut PdfBuilderState, points: f32) -> i32 {
    with_builder(state, |state| {
        if !points.is_finite() || points < 0.0 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "space must be a non-negative finite number",
            )
            .into());
        }
        state.current_page_mut()?.ops.push(BuilderOp::Space(points));
        Ok(())
    })
}

/// Add a horizontal rule at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_horizontal_rule(state: *mut PdfBuilderState) -> i32 {
    with_builder(state, |state| {
        state
            .current_page_mut()?
            .ops
            .push(BuilderOp::HorizontalRule);
        Ok(())
    })
}

/// Lay text out across multiple balanced columns.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_columns(
    state: *mut PdfBuilderState,
    count: i32,
    gap: f32,
    text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        if count <= 0 {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "column count must be positive").into(),
            );
        }
        if !gap.is_finite() || gap < 0.0 {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "column gap must be non-negative").into(),
            );
        }
        let text = c_string_to_string(text, "text")?;
        state.current_page_mut()?.ops.push(BuilderOp::Columns {
            count: count as u32,
            gap,
            text,
        });
        Ok(())
    })
}

/// Add a table with `rows` x `cols` plain-text cells in row-major order.
/// `header` marks the first row as a header. `style` is 0=default,
/// 1=bordered, 2=minimal.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_add_table(
    state: *mut PdfBuilderState,
    cells: *const *const c_char,
    rows: i32,
    cols: i32,
    header: i32,
    style: i32,
) -> i32 {
    with_builder(state, |state| {
        if rows <= 0 || cols <= 0 {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "table dimensions must be positive").into(),
            );
        }
        if style < 0 || style > 2 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                format!("unknown table style code: {style}"),
            )
            .into());
        }
        rows.checked_mul(cols).ok_or_else(|| {
            IoError::new(ErrorKind::InvalidInput, "table dimensions are too large")
        })?;
        if cells.is_null() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "table cells must not be null").into(),
            );
        }

        let mut table_rows = Vec::with_capacity(rows as usize);
        for row_index in 0..rows as usize {
            let mut row = Vec::with_capacity(cols as usize);
            for col_index in 0..cols as usize {
                let index = row_index * cols as usize + col_index;
                let ptr = unsafe { *cells.add(index) };
                row.push(c_string_to_string(ptr, "table cell")?);
            }
            table_rows.push(row);
        }

        state.current_page_mut()?.ops.push(BuilderOp::Table {
            rows: table_rows,
            header: header != 0,
            style,
        });
        Ok(())
    })
}

/// Add an image file at a rectangle. `alt` may be null.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_image_file(
    state: *mut PdfBuilderState,
    path: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    alt: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        validate_rect(x, y, width, height)?;
        let path = c_string_to_string(path, "path")?;
        let data = std::fs::read(&path)?;
        let alt = optional_string(alt, "alt")?;
        state.current_page_mut()?.ops.push(BuilderOp::Image {
            data,
            x,
            y,
            width,
            height,
            alt,
        });
        Ok(())
    })
}

/// Add an in-memory image (PNG or JPEG) at a rectangle. `alt` may be null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_add_image_bytes(
    state: *mut PdfBuilderState,
    data: *const u8,
    len: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    alt: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        validate_rect(x, y, width, height)?;
        if len <= 0 {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "image data must not be empty").into(),
            );
        }
        if data.is_null() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "image data must not be null").into(),
            );
        }
        let bytes = unsafe { std::slice::from_raw_parts(data, len as usize) }.to_vec();
        let alt = optional_string(alt, "alt")?;
        state.current_page_mut()?.ops.push(BuilderOp::Image {
            data: bytes,
            x,
            y,
            width,
            height,
            alt,
        });
        Ok(())
    })
}

/// Save the accumulated document to a file. The builder remains usable.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_save(state: *mut PdfBuilderState, path: *const c_char) -> i32 {
    with_builder(state, |state| {
        let path = c_string_to_string(path, "path")?;
        let bytes = state.build_bytes()?;
        std::fs::write(&path, bytes)?;
        Ok(())
    })
}

/// Build the accumulated document and return its bytes.
///
/// On success the caller owns the buffer and must release it with
/// `pdf_builder_bytes_free`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_build(
    state: *mut PdfBuilderState,
    out_data: *mut *mut u8,
    out_len: *mut i32,
) -> i32 {
    if out_data.is_null() || out_len.is_null() {
        set_last_error("output pointers must not be null");
        return -1;
    }

    let result = match builder_mut(state) {
        Ok(state) => state.build_bytes(),
        Err(e) => Err(e),
    };

    match result {
        Ok(bytes) => {
            let mut buffer = bytes.into_boxed_slice();
            let len = buffer.len();
            if len > i32::MAX as usize {
                set_last_error("document is too large to return as a single buffer");
                return -1;
            }
            let ptr = buffer.as_mut_ptr();
            std::mem::forget(buffer);
            unsafe {
                *out_data = ptr;
                *out_len = len as i32;
            }
            0
        }
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

/// Free a buffer returned by `pdf_builder_build`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_bytes_free(data: *mut u8, len: i32) {
    if data.is_null() || len <= 0 {
        return;
    }
    drop(unsafe { Box::from_raw(std::ptr::slice_from_raw_parts_mut(data, len as usize)) });
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
