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

use base64::Engine as _;
use pdf_oxide::PdfDocument;
use pdf_oxide::api::Pdf;
use pdf_oxide::editor::DocumentEditor;
use pdf_oxide::elements::{ContentElement, PathContent, PathOperation};
use pdf_oxide::extractors::{PageLabelExtractor, PdfImage, XmpExtractor};
use pdf_oxide::fonts::bundled::{DEJAVU_SANS, DEJAVU_SANS_BOLD};
use pdf_oxide::geometry::Rect;
use pdf_oxide::layout::{Color, TextChar, TextLine, TextSpan, Word};
use pdf_oxide::outline::{Destination, OutlineItem};
use pdf_oxide::writer::{
    CellAlign, DocumentBuilder as PdfWriterBuilder, EmbeddedFont, FluentPageBuilder, LineStyle,
    LinkAnnotation, ListStyle, PageSize, StampType, StreamingColumn, StreamingTableConfig, Table,
    TableCell, TableRow, TableStyle, TextAnnotation, TextLayout, TextMarkupAnnotation, TextRun,
    TextRunStyle, WatermarkAnnotation,
};
use pdf_oxide::{TextAnnotationIcon, TextMarkupType};
use serde::Serialize;
use std::cell::RefCell;
use std::collections::BTreeMap;
use std::ffi::{CStr, CString};
use std::io::{Error as IoError, ErrorKind};
use std::os::raw::c_char;

#[cfg(feature = "ffi-test-hooks")]
pub mod test_hooks;

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
    LinkUrl(String),
    LinkPage(usize),
    LinkNamed(String),
    LinkJavaScript(String),
    LinkRect {
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        url: String,
    },
    TextMarkup {
        kind: i32,
        r: f32,
        g: f32,
        b: f32,
    },
    TextMarkupRect {
        kind: i32,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        r: f32,
        g: f32,
        b: f32,
    },
    StickyNote {
        x: Option<f32>,
        y: Option<f32>,
        text: String,
        icon: i32,
    },
    FreeText {
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        text: String,
        font: Option<String>,
        size: Option<f32>,
    },
    Watermark {
        text: String,
    },
    WatermarkPreset {
        kind: i32,
    },
    WatermarkCustom {
        text: String,
        opacity: f32,
        rotation: f32,
        font: String,
        size: f32,
        r: f32,
        g: f32,
        b: f32,
        rect: Option<(f32, f32, f32, f32)>,
    },
    Stamp {
        type_name: String,
    },
    StampAt {
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        type_name: String,
    },
    Footnote {
        ref_mark: String,
        note_text: String,
    },
    FormTextField {
        name: String,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        default_value: Option<String>,
    },
    FormCheckbox {
        name: String,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        checked: bool,
    },
    FormComboBox {
        name: String,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        options: Vec<String>,
        selected: Option<String>,
    },
    FormListBox {
        name: String,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        options: Vec<String>,
        selected: Option<String>,
        multi_select: bool,
    },
    FormRadioGroup {
        name: String,
        buttons: Vec<(String, f32, f32, f32, f32)>,
        selected: Option<String>,
    },
    FormPushButton {
        name: String,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        caption: String,
    },
    FormSignatureField {
        name: String,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
    },
    BarcodeQr {
        data: String,
        x: f32,
        y: f32,
        size: f32,
    },
    Barcode1d {
        barcode_type: i32,
        data: String,
        x: f32,
        y: f32,
        width: f32,
        height: f32,
    },
    VecRect {
        x: f32,
        y: f32,
        width: f32,
        height: f32,
    },
    VecFilledRect {
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        r: f32,
        g: f32,
        b: f32,
    },
    VecLine {
        x1: f32,
        y1: f32,
        x2: f32,
        y2: f32,
    },
    VecStrokeRect {
        x: f32,
        y: f32,
        width: f32,
        height: f32,
        width_style: f32,
        r: f32,
        g: f32,
        b: f32,
        dash: Vec<f32>,
        phase: f32,
    },
    VecStrokeLine {
        x1: f32,
        y1: f32,
        x2: f32,
        y2: f32,
        width_style: f32,
        r: f32,
        g: f32,
        b: f32,
        dash: Vec<f32>,
        phase: f32,
    },
    VecCircle {
        cx: f32,
        cy: f32,
        radius: f32,
        stroke: Option<(f32, f32, f32, f32, Vec<f32>, f32)>,
        fill: Option<(f32, f32, f32)>,
    },
    VecEllipse {
        cx: f32,
        cy: f32,
        rx: f32,
        ry: f32,
        stroke: Option<(f32, f32, f32, f32, Vec<f32>, f32)>,
        fill: Option<(f32, f32, f32)>,
    },
    VecPolygon {
        points: Vec<(f32, f32)>,
        stroke: Option<(f32, f32, f32, f32, Vec<f32>, f32)>,
        fill: Option<(f32, f32, f32)>,
    },
    VecArc {
        cx: f32,
        cy: f32,
        radius: f32,
        start_radians: f32,
        end_radians: f32,
        width_style: f32,
        r: f32,
        g: f32,
        b: f32,
        dash: Vec<f32>,
        phase: f32,
    },
    VecBezier {
        x0: f32,
        y0: f32,
        c1x: f32,
        c1y: f32,
        c2x: f32,
        c2y: f32,
        x3: f32,
        y3: f32,
        stroke: Option<(f32, f32, f32, f32, Vec<f32>, f32)>,
        fill: Option<(f32, f32, f32)>,
    },
    VecPath {
        operations: Vec<PathOperation>,
        stroke: Option<(f32, f32, f32, f32, Vec<f32>, f32)>,
        fill: Option<(f32, f32, f32)>,
    },
    StreamingTable {
        config: StreamingTableConfig,
        rows: Vec<Vec<(String, usize)>>,
        columns: Vec<(String, f32)>,
        repeat_header: bool,
        pad_h: f32,
        pad_top: f32,
        pad_bottom: f32,
    },
}

struct PageSpec {
    kind: PageKind,
    ops: Vec<BuilderOp>,
}

struct PendingStreamingTable {
    page_index: usize,
    config: StreamingTableConfig,
    column_count: usize,
    rows: Vec<Vec<(String, usize)>>,
    /// Column header text and width, kept so the fit check below can measure
    /// the header exactly the way the renderer will.
    columns: Vec<(String, f32)>,
    repeat_header: bool,
    pad_h: f32,
    pad_top: f32,
    pad_bottom: f32,
}

#[derive(Default)]
struct BuilderMetadata {
    title: Option<String>,
    author: Option<String>,
    subject: Option<String>,
    keywords: Option<String>,
    creator: Option<String>,
}

struct BuilderFont {
    name: String,
    data: Vec<u8>,
}

/// State container for the DocumentBuilder API.
/// Holds the document model accumulated across FFI calls; the PDF is rendered
/// from the model on save/build so callers can add many elements per page.
pub struct PdfBuilderState {
    pages: Vec<PageSpec>,
    metadata: BuilderMetadata,
    fonts: Vec<BuilderFont>,
    tagged: bool,
    language: Option<String>,
    role_map: Vec<(String, String)>,
    streaming_table: Option<PendingStreamingTable>,
}

impl PdfBuilderState {
    fn page_mut(
        &mut self,
        page_index: usize,
    ) -> Result<&mut PageSpec, Box<dyn std::error::Error>> {
        self.pages.get_mut(page_index).ok_or_else(|| {
            IoError::new(
                ErrorKind::InvalidInput,
                format!("page index {page_index} is out of range"),
            )
            .into()
        })
    }

    fn build_bytes(&self) -> Result<Vec<u8>, Box<dyn std::error::Error>> {
        if self.streaming_table.is_some() {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "finish the streaming table before saving",
            )
            .into());
        }

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

        if self.tagged {
            document = document.tagged_pdf_ua1();
        }
        if let Some(lang) = &self.language {
            document = document.language(lang.clone());
        }
        for (custom, standard) in &self.role_map {
            document = document.role_map(custom.clone(), standard.clone());
        }
        for font in &self.fonts {
            if let Ok(embedded) = EmbeddedFont::from_data(Some(font.name.clone()), font.data.clone())
            {
                document = document.register_embedded_font(font.name.clone(), embedded);
            }
        }

        for page in &self.pages {
            let mut page_builder = document.page(page.kind.page_size());
            let mut text_state = TextState::default();
            for op in &page.ops {
                page_builder = render_op(page_builder, op, &mut text_state)?;
            }
            page_builder.done();
        }

        Ok(document.build()?)
    }
}

/// Reject a streaming table that the renderer could never paginate.
///
/// pdf_oxide 0.3.78 lays a streaming table out by walking rows and breaking
/// pages; `draw_row_from` breaks the page again whenever not a single line
/// fits, without checking that the new page is any better. When a row's line
/// height (or the repeated header plus one line) exceeds the space a fresh
/// page offers, that loop never terminates and allocates a page per iteration
/// until the process runs out of memory. Reproduced with the fuzz target: an
/// A4 page, a text run at 2.37e14 pt and a streaming table with
/// `repeat_header` peaked at 2.1 GB in ~69k executions.
///
/// The limits here are exact, not estimates: a fresh page starts at
/// `height - 72` and `remaining_space()` subtracts another 72 (see
/// `document_builder.rs`), so it offers `height - 144` points, and a row line
/// is `font_size * 1.2` (the `TextConfig` default). The header height is
/// measured with the same `TextLayout` the renderer uses. If upstream changes
/// those constants, the nightly fuzz gate is what catches it.
fn ensure_streaming_table_fits(
    page_height: f32,
    font: &str,
    font_size: f32,
    columns: &[(String, f32)],
    repeat_header: bool,
    pad_h: f32,
    pad_top: f32,
    pad_bottom: f32,
) -> Result<(), Box<dyn std::error::Error>> {
    const VERTICAL_MARGINS: f32 = 144.0;
    const LINE_HEIGHT_MULTIPLIER: f32 = 1.2;

    let available = (page_height - VERTICAL_MARGINS).max(0.0);
    let line_height = font_size * LINE_HEIGHT_MULTIPLIER;

    // A line that cannot fit on an otherwise empty page can never make
    // progress, whether or not the header is repeated.
    if line_height + pad_top > available {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            format!(
                "streaming table row cannot fit on a {page_height} pt page: a line at {font_size} pt needs {line_height} pt and a fresh page offers {available} pt; reduce the font size or use a larger page"
            ),
        )
        .into());
    }

    if repeat_header {
        let layout = TextLayout::new();
        let mut header_lines = 1usize;
        for (header, width) in columns {
            let content_width = (width - 2.0 * pad_h).max(1.0);
            let lines = layout
                .wrap_text(header, font, font_size, content_width)
                .len()
                .max(1);
            header_lines = header_lines.max(lines);
        }
        let header_height = pad_top + pad_bottom + header_lines as f32 * line_height;
        if header_height + line_height + pad_top > available {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                format!(
                    "streaming table with a repeated header cannot fit on a {page_height} pt page: the {header_lines}-line header plus one row needs {} pt and a fresh page offers {available} pt; reduce the font size, widen the columns or disable header repetition",
                    header_height + line_height + pad_top
                ),
            )
            .into());
        }
    }

    Ok(())
}

/// The page builder's current text configuration, mirrored so that table fit
/// checks can use the same font and size the renderer will use.
struct TextState {
    font: String,
    size: f32,
}

impl Default for TextState {
    fn default() -> Self {
        Self {
            font: "Helvetica".to_string(),
            size: 12.0,
        }
    }
}

fn render_op<'a>(
    mut page: FluentPageBuilder<'a>,
    op: &BuilderOp,
    text_state: &mut TextState,
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
                text_state.font = font.clone();
                text_state.size = size.unwrap_or(12.0);
            } else if let Some(size) = size {
                page = page.font("Helvetica", *size);
                text_state.size = *size;
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
        BuilderOp::StreamingTable {
            config,
            rows,
            columns,
            repeat_header,
            pad_h,
            pad_top,
            pad_bottom,
        } => {
            ensure_streaming_table_fits(
                page.page_dimensions().1,
                &text_state.font,
                text_state.size,
                columns,
                *repeat_header,
                *pad_h,
                *pad_top,
                *pad_bottom,
            )?;
            let mut table = page.streaming_table(config.clone());
            for row in rows {
                table.push_row(|r| {
                    for (text, span) in row {
                        if *span > 1 {
                            r.span_cell(text.clone(), *span);
                        } else {
                            r.cell(text.clone());
                        }
                    }
                })?;
            }
            page = table.finish();
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
        BuilderOp::LinkUrl(url) => {
            page = page.link_url(url);
        }
        BuilderOp::LinkPage(target_page) => {
            page = page.link_page(*target_page);
        }
        BuilderOp::LinkNamed(destination) => {
            page = page.link_named(destination);
        }
        BuilderOp::LinkJavaScript(script) => {
            page = page.link_javascript(script);
        }
        BuilderOp::LinkRect {
            x,
            y,
            width,
            height,
            url,
        } => {
            let link = LinkAnnotation::uri(Rect::new(*x, *y, *width, *height), url);
            page = page.add_annotation(link);
        }
        BuilderOp::TextMarkup { kind, r, g, b } => {
            let color = (*r, *g, *b);
            page = match text_markup_type_from_code(*kind)? {
                TextMarkupType::Highlight => page.highlight(color),
                TextMarkupType::Underline => page.underline(color),
                TextMarkupType::StrikeOut => page.strikeout(color),
                TextMarkupType::Squiggly => page.squiggly(color),
            };
        }
        BuilderOp::TextMarkupRect {
            kind,
            x,
            y,
            width,
            height,
            r,
            g,
            b,
        } => {
            let markup = TextMarkupAnnotation::from_rect(
                text_markup_type_from_code(*kind)?,
                Rect::new(*x, *y, *width, *height),
            )
            .with_color(*r, *g, *b);
            page = page.add_annotation(markup);
        }
        BuilderOp::StickyNote { x, y, text, icon } => {
            let icon = text_annotation_icon_from_code(*icon)?;
            page = match (x, y) {
                (Some(x), Some(y)) => {
                    let note = TextAnnotation::new(Rect::new(*x, *y, 24.0, 24.0), text)
                        .with_icon(icon);
                    page.add_annotation(note)
                }
                _ => page.sticky_note_with_icon(text, icon),
            };
        }
        BuilderOp::FreeText {
            x,
            y,
            width,
            height,
            text,
            font,
            size,
        } => {
            let rect = Rect::new(*x, *y, *width, *height);
            page = match font {
                Some(font) if !font.is_empty() => {
                    page.freetext_styled(rect, text, font, size.unwrap_or(12.0))
                }
                _ => page.freetext(rect, text),
            };
        }
        BuilderOp::Watermark { text } => {
            page = page.watermark(text);
        }
        BuilderOp::WatermarkPreset { kind } => {
            page = match kind {
                0 => page.watermark_confidential(),
                1 => page.watermark_draft(),
                other => {
                    return Err(IoError::new(
                        ErrorKind::InvalidInput,
                        format!("unknown watermark preset code: {other}"),
                    )
                    .into());
                }
            };
        }
        BuilderOp::WatermarkCustom {
            text,
            opacity,
            rotation,
            font,
            size,
            r,
            g,
            b,
            rect,
        } => {
            let mut watermark = WatermarkAnnotation::new(text)
                .with_opacity(*opacity)
                .with_rotation(*rotation)
                .with_font(font.as_str(), *size)
                .with_color(*r, *g, *b);
            if let Some((x, y, width, height)) = rect {
                watermark = watermark.with_rect(Rect::new(*x, *y, *width, *height));
            }
            page = page.watermark_custom(watermark);
        }
        BuilderOp::Stamp { type_name } => {
            let stamp_type = if type_name.is_empty() {
                StampType::Draft
            } else {
                parse_stamp_type(type_name)
            };
            page = page.stamp(stamp_type);
        }
        BuilderOp::StampAt {
            x,
            y,
            width,
            height,
            type_name,
        } => {
            page = page.stamp_at(
                Rect::new(*x, *y, *width, *height),
                parse_stamp_type(type_name),
            );
        }
        BuilderOp::Footnote {
            ref_mark,
            note_text,
        } => {
            page = page.footnote(ref_mark, note_text);
        }
        BuilderOp::FormTextField {
            name,
            x,
            y,
            width,
            height,
            default_value,
        } => {
            page = page.text_field(name, *x, *y, *width, *height, default_value.clone());
        }
        BuilderOp::FormCheckbox {
            name,
            x,
            y,
            width,
            height,
            checked,
        } => {
            page = page.checkbox(name, *x, *y, *width, *height, *checked);
        }
        BuilderOp::FormComboBox {
            name,
            x,
            y,
            width,
            height,
            options,
            selected,
        } => {
            page = page.combo_box(name, *x, *y, *width, *height, options.clone(), selected.clone());
        }
        BuilderOp::FormListBox {
            name,
            x,
            y,
            width,
            height,
            options,
            selected,
            multi_select,
        } => {
            page = page.list_box(
                name,
                *x,
                *y,
                *width,
                *height,
                options.clone(),
                selected.clone(),
                *multi_select,
            );
        }
        BuilderOp::FormRadioGroup {
            name,
            buttons,
            selected,
        } => {
            page = page.radio_group(name, buttons.clone(), selected.clone());
        }
        BuilderOp::FormPushButton {
            name,
            x,
            y,
            width,
            height,
            caption,
        } => {
            page = page.push_button(name, *x, *y, *width, *height, caption);
        }
        BuilderOp::FormSignatureField {
            name,
            x,
            y,
            width,
            height,
        } => {
            page = page.signature_field(name, *x, *y, *width, *height);
        }
        BuilderOp::BarcodeQr { data, x, y, size } => {
            page = page.barcode_qr(data, *x, *y, *size)?;
        }
        BuilderOp::Barcode1d {
            barcode_type,
            data,
            x,
            y,
            width,
            height,
        } => {
            page = page.barcode_1d(
                parse_barcode_type(*barcode_type)?,
                data,
                *x,
                *y,
                *width,
                *height,
            )?;
        }
        BuilderOp::VecRect {
            x,
            y,
            width,
            height,
        } => {
            page = page.rect(*x, *y, *width, *height);
        }
        BuilderOp::VecFilledRect {
            x,
            y,
            width,
            height,
            r,
            g,
            b,
        } => {
            page = page.filled_rect(*x, *y, *width, *height, *r, *g, *b);
        }
        BuilderOp::VecLine { x1, y1, x2, y2 } => {
            page = page.line(*x1, *y1, *x2, *y2);
        }
        BuilderOp::VecStrokeRect {
            x,
            y,
            width,
            height,
            width_style,
            r,
            g,
            b,
            dash,
            phase,
        } => {
            let style = make_line_style(*width_style, *r, *g, *b, dash, *phase)?;
            page = page.stroke_rect(*x, *y, *width, *height, style);
        }
        BuilderOp::VecStrokeLine {
            x1,
            y1,
            x2,
            y2,
            width_style,
            r,
            g,
            b,
            dash,
            phase,
        } => {
            let style = make_line_style(*width_style, *r, *g, *b, dash, *phase)?;
            page = page.stroke_line(*x1, *y1, *x2, *y2, style);
        }
        BuilderOp::VecCircle {
            cx,
            cy,
            radius,
            stroke,
            fill,
        } => {
            let stroke = optional_line_style(stroke)?;
            page = page.circle(*cx, *cy, *radius, stroke, *fill);
        }
        BuilderOp::VecEllipse {
            cx,
            cy,
            rx,
            ry,
            stroke,
            fill,
        } => {
            let stroke = optional_line_style(stroke)?;
            page = page.ellipse(*cx, *cy, *rx, *ry, stroke, *fill);
        }
        BuilderOp::VecPolygon {
            points,
            stroke,
            fill,
        } => {
            let stroke = optional_line_style(stroke)?;
            page = page.polygon(points, stroke, *fill);
        }
        BuilderOp::VecArc {
            cx,
            cy,
            radius,
            start_radians,
            end_radians,
            width_style,
            r,
            g,
            b,
            dash,
            phase,
        } => {
            let style = make_line_style(*width_style, *r, *g, *b, dash, *phase)?;
            page = page.arc(
                *cx,
                *cy,
                *radius,
                *start_radians,
                *end_radians,
                style,
            );
        }
        BuilderOp::VecBezier {
            x0,
            y0,
            c1x,
            c1y,
            c2x,
            c2y,
            x3,
            y3,
            stroke,
            fill,
        } => {
            let stroke = optional_line_style(stroke)?;
            let stroke = stroke.unwrap_or_else(|| LineStyle::new(1.0, 0.0, 0.0, 0.0));
            page = page.bezier_curve(
                *x0, *y0, *c1x, *c1y, *c2x, *c2y, *x3, *y3, stroke, *fill,
            );
        }
        BuilderOp::VecPath {
            operations,
            stroke,
            fill,
        } => {
            let stroke = optional_line_style(stroke)?;
            let mut path = PathContent::from_operations(operations.clone());
            path.stroke_color = stroke.as_ref().map(|style| Color {
                r: style.color.0,
                g: style.color.1,
                b: style.color.2,
            });
            path.stroke_width = stroke.as_ref().map(|style| style.width).unwrap_or(1.0);
            path.dash_pattern = stroke.as_ref().and_then(|style| style.dash.clone());
            path.fill_color = fill.map(|(r, g, b)| Color { r, g, b });
            page = page.element(ContentElement::Path(path));
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
// EXTENDED EXTRACTION FUNCTIONS
// ============================================================================

fn open_document(path: *const c_char) -> Result<PdfDocument, Box<dyn std::error::Error>> {
    let path_str = c_string_to_string(path, "path")?;
    Ok(PdfDocument::open(&path_str)?)
}

fn page_index_arg(page_index: i32) -> Result<usize, Box<dyn std::error::Error>> {
    usize::try_from(page_index).map_err(|_| {
        IoError::new(ErrorKind::InvalidInput, "page index must be non-negative").into()
    })
}

fn ffi_string<F>(action: F) -> *mut c_char
where
    F: FnOnce() -> Result<CString, Box<dyn std::error::Error>>,
{
    match action() {
        Ok(value) => value.into_raw(),
        Err(e) => {
            set_last_error(e);
            std::ptr::null_mut()
        }
    }
}

fn ffi_bytes<F>(out_data: *mut *mut u8, out_len: *mut i32, action: F) -> i32
where
    F: FnOnce() -> Result<Vec<u8>, Box<dyn std::error::Error>>,
{
    if out_data.is_null() || out_len.is_null() {
        set_last_error("output pointers must not be null");
        return -1;
    }

    match action() {
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

fn json_string<T: Serialize>(value: &T) -> Result<CString, Box<dyn std::error::Error>> {
    Ok(CString::new(serde_json::to_string(value)?)?)
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct RectDto {
    x: f32,
    y: f32,
    width: f32,
    height: f32,
}

impl RectDto {
    fn from_rect(rect: &Rect) -> Self {
        Self {
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height,
        }
    }

    fn from_corners(x0: f32, y0: f32, x1: f32, y1: f32) -> Self {
        Self {
            x: x0,
            y: y0,
            width: x1 - x0,
            height: y1 - y0,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ColorDto {
    r: f32,
    g: f32,
    b: f32,
}

impl From<&Color> for ColorDto {
    fn from(color: &Color) -> Self {
        Self {
            r: color.r,
            g: color.g,
            b: color.b,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct SpanDto {
    text: String,
    bbox: RectDto,
    font_name: String,
    font_size: f32,
    font_weight: u16,
    is_italic: bool,
    is_monospace: bool,
    color: ColorDto,
    sequence: usize,
    mcid: Option<u32>,
}

impl From<&TextSpan> for SpanDto {
    fn from(span: &TextSpan) -> Self {
        Self {
            text: span.text.clone(),
            bbox: RectDto::from_rect(&span.bbox),
            font_name: span.font_name.clone(),
            font_size: span.font_size,
            font_weight: span.font_weight as u16,
            is_italic: span.is_italic,
            is_monospace: span.is_monospace,
            color: ColorDto::from(&span.color),
            sequence: span.sequence,
            mcid: span.mcid,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct CharDto {
    text: String,
    bbox: RectDto,
    font_name: String,
    font_size: f32,
    font_weight: u16,
    is_italic: bool,
    is_monospace: bool,
    color: ColorDto,
    origin_x: f32,
    origin_y: f32,
    rotation_degrees: f32,
    mcid: Option<u32>,
}

impl From<&TextChar> for CharDto {
    fn from(value: &TextChar) -> Self {
        Self {
            text: value.char.to_string(),
            bbox: RectDto::from_rect(&value.bbox),
            font_name: value.font_name.clone(),
            font_size: value.font_size,
            font_weight: value.font_weight as u16,
            is_italic: value.is_italic,
            is_monospace: value.is_monospace,
            color: ColorDto::from(&value.color),
            origin_x: value.origin_x,
            origin_y: value.origin_y,
            rotation_degrees: value.rotation_degrees,
            mcid: value.mcid,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct WordDto {
    text: String,
    bbox: RectDto,
    avg_font_size: f32,
    dominant_font: String,
    is_bold: bool,
    is_italic: bool,
    sequence: usize,
    rotation_degrees: f32,
}

impl From<&Word> for WordDto {
    fn from(value: &Word) -> Self {
        Self {
            text: value.text.clone(),
            bbox: RectDto::from_rect(&value.bbox),
            avg_font_size: value.avg_font_size,
            dominant_font: value.dominant_font.clone(),
            is_bold: value.is_bold,
            is_italic: value.is_italic,
            sequence: value.sequence,
            rotation_degrees: value.rotation_degrees,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct LineDto {
    text: String,
    bbox: RectDto,
    word_count: usize,
    words: Vec<WordDto>,
}

impl From<&TextLine> for LineDto {
    fn from(value: &TextLine) -> Self {
        Self {
            text: value.text.clone(),
            bbox: RectDto::from_rect(&value.bbox),
            word_count: value.words.len(),
            words: value.words.iter().map(WordDto::from).collect(),
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct PathDto {
    bbox: RectDto,
    stroke_color: Option<ColorDto>,
    fill_color: Option<ColorDto>,
    stroke_width: f32,
    operation_count: usize,
    matrix: Option<[f32; 6]>,
    layer: Option<String>,
}

impl From<&PathContent> for PathDto {
    fn from(value: &PathContent) -> Self {
        Self {
            bbox: RectDto::from_rect(&value.bbox),
            stroke_color: value.stroke_color.as_ref().map(ColorDto::from),
            fill_color: value.fill_color.as_ref().map(ColorDto::from),
            stroke_width: value.stroke_width,
            operation_count: value.operations.len(),
            matrix: value.matrix,
            layer: value.layer.clone(),
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct TableCellDto {
    text: String,
    colspan: u32,
    rowspan: u32,
    bbox: Option<RectDto>,
    is_header: bool,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct TableRowDto {
    is_header: bool,
    cells: Vec<TableCellDto>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct TableDto {
    has_header: bool,
    col_count: usize,
    bbox: Option<RectDto>,
    rows: Vec<TableRowDto>,
}

impl From<&pdf_oxide::structure::table_extractor::Table> for TableDto {
    fn from(value: &pdf_oxide::structure::table_extractor::Table) -> Self {
        Self {
            has_header: value.has_header,
            col_count: value.col_count,
            bbox: value.bbox.as_ref().map(RectDto::from_rect),
            rows: value
                .rows
                .iter()
                .map(|row| TableRowDto {
                    is_header: row.is_header,
                    cells: row
                        .cells
                        .iter()
                        .map(|cell| TableCellDto {
                            text: cell.text.clone(),
                            colspan: cell.colspan,
                            rowspan: cell.rowspan,
                            bbox: cell.bbox.as_ref().map(RectDto::from_rect),
                            is_header: cell.is_header,
                        })
                        .collect(),
                })
                .collect(),
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct ImageDto {
    width: u32,
    height: u32,
    color_space: String,
    bits_per_component: u8,
    bbox: Option<RectDto>,
    rotation: i32,
    matrix: [f32; 6],
    png_base64: String,
}

impl ImageDto {
    fn from_image(value: &PdfImage) -> Result<Self, Box<dyn std::error::Error>> {
        let png = value.to_png_bytes()?;
        Ok(Self {
            width: value.width(),
            height: value.height(),
            color_space: format!("{:?}", value.color_space()),
            bits_per_component: value.bits_per_component(),
            bbox: value.bbox().map(RectDto::from_rect),
            rotation: value.rotation_degrees(),
            matrix: value.matrix(),
            png_base64: base64::engine::general_purpose::STANDARD.encode(png),
        })
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct EmbeddedFileDto {
    name: String,
    size: usize,
    data_base64: String,
}

impl From<(String, Vec<u8>)> for EmbeddedFileDto {
    fn from((name, data): (String, Vec<u8>)) -> Self {
        Self {
            size: data.len(),
            data_base64: base64::engine::general_purpose::STANDARD.encode(&data),
            name,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct PageInfoDto {
    width: f32,
    height: f32,
    rotation: i32,
    media_box: RectDto,
    visible_box: Option<RectDto>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct PageLabelDto {
    page: usize,
    label: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct OutlineItemDto {
    title: String,
    page: Option<usize>,
    named_destination: Option<String>,
    children: Vec<OutlineItemDto>,
}

impl From<&OutlineItem> for OutlineItemDto {
    fn from(value: &OutlineItem) -> Self {
        let (page, named_destination) = match &value.dest {
            Some(Destination::PageIndex(page)) => (Some(*page), None),
            Some(Destination::Named(name)) => (None, Some(name.clone())),
            None => (None, None),
        };
        Self {
            title: value.title.clone(),
            page,
            named_destination,
            children: value.children.iter().map(OutlineItemDto::from).collect(),
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct XmpDto {
    title: Option<String>,
    creators: Vec<String>,
    description: Option<String>,
    subjects: Vec<String>,
    language: Option<String>,
    rights: Option<String>,
    format: Option<String>,
    creator_tool: Option<String>,
    create_date: Option<String>,
    modify_date: Option<String>,
    metadata_date: Option<String>,
    producer: Option<String>,
    keywords: Option<String>,
    pdf_version: Option<String>,
    trapped: Option<String>,
    usage_terms: Option<String>,
    rights_marked: Option<bool>,
    rights_web_statement: Option<String>,
    custom: BTreeMap<String, String>,
    raw_xml: Option<String>,
}

impl From<pdf_oxide::extractors::XmpMetadata> for XmpDto {
    fn from(value: pdf_oxide::extractors::XmpMetadata) -> Self {
        let mut custom = BTreeMap::new();
        for (key, item) in value.custom {
            custom.insert(key, item);
        }
        Self {
            title: value.dc_title,
            creators: value.dc_creator,
            description: value.dc_description,
            subjects: value.dc_subject,
            language: value.dc_language,
            rights: value.dc_rights,
            format: value.dc_format,
            creator_tool: value.xmp_creator_tool,
            create_date: value.xmp_create_date,
            modify_date: value.xmp_modify_date,
            metadata_date: value.xmp_metadata_date,
            producer: value.pdf_producer,
            keywords: value.pdf_keywords,
            pdf_version: value.pdf_version,
            trapped: value.pdf_trapped,
            usage_terms: value.xmp_rights_usage_terms,
            rights_marked: value.xmp_rights_marked,
            rights_web_statement: value.xmp_rights_web_statement,
            custom,
            raw_xml: value.raw_xml,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct DocumentMetadataDto {
    title: Option<String>,
    author: Option<String>,
    subject: Option<String>,
    keywords: Option<String>,
    creator: Option<String>,
    producer: Option<String>,
    xmp: Option<XmpDto>,
}

fn decode_pdf_text(bytes: &[u8]) -> String {
    if bytes.len() >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF {
        let pairs: Vec<u16> = bytes[2..]
            .chunks_exact(2)
            .map(|chunk| u16::from_be_bytes([chunk[0], chunk[1]]))
            .collect();
        String::from_utf16(&pairs).unwrap_or_else(|_| String::from_utf8_lossy(bytes).into_owned())
    } else if bytes.len() >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE {
        let pairs: Vec<u16> = bytes[2..]
            .chunks_exact(2)
            .map(|chunk| u16::from_le_bytes([chunk[0], chunk[1]]))
            .collect();
        String::from_utf16(&pairs).unwrap_or_else(|_| String::from_utf8_lossy(bytes).into_owned())
    } else {
        String::from_utf8_lossy(bytes).into_owned()
    }
}

fn info_string(doc: &PdfDocument, key: &str) -> Option<String> {
    let info_raw = doc.trailer().as_dict()?.get("Info")?;
    let info = doc.resolve_object(info_raw).ok()?;
    let value_raw = info.as_dict()?.get(key)?.clone();
    let value = doc.resolve_object(&value_raw).ok()?;
    let text = decode_pdf_text(value.as_string()?).trim().to_string();
    (!text.is_empty()).then_some(text)
}

/// Extract the text of a single page.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_text_page(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let page_index = page_index_arg(page_index)?;
        Ok(CString::new(doc.extract_text(page_index)?)?)
    })
}

/// Convert a single page to plain text.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_plain_text(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let page_index = page_index_arg(page_index)?;
        Ok(CString::new(
            doc.to_plain_text(page_index, &Default::default())?,
        )?)
    })
}

/// Convert all pages to plain text.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_plain_text_all(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        Ok(CString::new(doc.to_plain_text_all(&Default::default())?)?)
    })
}

/// Convert a single page to HTML.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_html(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let page_index = page_index_arg(page_index)?;
        Ok(CString::new(doc.to_html(page_index, &Default::default())?)?)
    })
}

/// Convert all pages to HTML.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_html_all(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        Ok(CString::new(doc.to_html_all(&Default::default())?)?)
    })
}

/// Convert the document to DOCX bytes. Free with `pdf_bytes_free`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_docx_bytes(
    path: *const c_char,
    out_data: *mut *mut u8,
    out_len: *mut i32,
) -> i32 {
    ffi_bytes(out_data, out_len, || {
        Ok(open_document(path)?.to_docx_bytes()?)
    })
}

/// Convert the document to PPTX bytes. Free with `pdf_bytes_free`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_pptx_bytes(
    path: *const c_char,
    out_data: *mut *mut u8,
    out_len: *mut i32,
) -> i32 {
    ffi_bytes(out_data, out_len, || {
        Ok(open_document(path)?.to_pptx_bytes()?)
    })
}

/// Convert the document to XLSX bytes. Free with `pdf_bytes_free`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_to_xlsx_bytes(
    path: *const c_char,
    out_data: *mut *mut u8,
    out_len: *mut i32,
) -> i32 {
    ffi_bytes(out_data, out_len, || {
        Ok(open_document(path)?.to_xlsx_bytes()?)
    })
}

/// Extract images from a page as JSON, including base64-encoded PNG data.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_images(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let images = doc.extract_images(page_index_arg(page_index)?)?;
        let dtos: Vec<ImageDto> = images
            .iter()
            .map(ImageDto::from_image)
            .collect::<Result<_, _>>()?;
        json_string(&dtos)
    })
}

/// Extract embedded files as JSON, including base64-encoded data.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_embedded_files(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let files: Vec<EmbeddedFileDto> = doc
            .extract_embedded_files()?
            .into_iter()
            .map(EmbeddedFileDto::from)
            .collect();
        json_string(&files)
    })
}

/// Extract embedded fonts as JSON, including base64-encoded data.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_embedded_fonts(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let fonts: Vec<EmbeddedFileDto> = doc
            .extract_embedded_fonts()?
            .into_iter()
            .map(EmbeddedFileDto::from)
            .collect();
        json_string(&fonts)
    })
}

/// Extract tables from a page as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_tables(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let tables = doc.extract_tables(page_index_arg(page_index)?)?;
        let dtos: Vec<TableDto> = tables.iter().map(TableDto::from).collect();
        json_string(&dtos)
    })
}

/// Extract words from a page as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_words(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let words = doc.extract_words(page_index_arg(page_index)?)?;
        let dtos: Vec<WordDto> = words.iter().map(WordDto::from).collect();
        json_string(&dtos)
    })
}

/// Extract text lines from a page as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_text_lines(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let lines = doc.extract_text_lines(page_index_arg(page_index)?)?;
        let dtos: Vec<LineDto> = lines.iter().map(LineDto::from).collect();
        json_string(&dtos)
    })
}

/// Extract characters from a page as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_chars(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let chars = doc.extract_chars(page_index_arg(page_index)?)?;
        let dtos: Vec<CharDto> = chars.iter().map(CharDto::from).collect();
        json_string(&dtos)
    })
}

/// Extract styled text spans from a page as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_spans(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let spans = doc.extract_spans(page_index_arg(page_index)?)?;
        let dtos: Vec<SpanDto> = spans.iter().map(SpanDto::from).collect();
        json_string(&dtos)
    })
}

/// Extract vector paths from a page as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_extract_paths(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let paths = doc.extract_paths(page_index_arg(page_index)?)?;
        let dtos: Vec<PathDto> = paths.iter().map(PathDto::from).collect();
        json_string(&dtos)
    })
}

/// Get the PDF version as a string such as `1.7`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_get_version(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let (major, minor) = doc.version();
        Ok(CString::new(format!("{major}.{minor}"))?)
    })
}

/// Get page dimensions and rotation as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_get_page_info(path: *const c_char, page_index: i32) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let page_index = page_index_arg(page_index)?;
        let (x0, y0, x1, y1) = doc.get_page_media_box(page_index)?;
        let rotation = doc.get_page_rotation(page_index)?;
        let visible_box = doc
            .get_page_visible_box(page_index)
            .ok()
            .map(|(vx0, vy0, vx1, vy1)| RectDto::from_corners(vx0, vy0, vx1, vy1));
        json_string(&PageInfoDto {
            width: x1 - x0,
            height: y1 - y0,
            rotation,
            media_box: RectDto::from_corners(x0, y0, x1, y1),
            visible_box,
        })
    })
}

/// Get document metadata (Info dictionary plus XMP) as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_get_metadata(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let xmp = XmpExtractor::extract(&doc)?.map(XmpDto::from);
        json_string(&DocumentMetadataDto {
            title: info_string(&doc, "Title"),
            author: info_string(&doc, "Author"),
            subject: info_string(&doc, "Subject"),
            keywords: info_string(&doc, "Keywords"),
            creator: info_string(&doc, "Creator"),
            producer: info_string(&doc, "Producer"),
            xmp,
        })
    })
}

/// Get XMP metadata as JSON, or `null` when the document has none.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_get_xmp_metadata(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let xmp = XmpExtractor::extract(&doc)?.map(XmpDto::from);
        json_string(&xmp)
    })
}

/// Get the document outline (bookmarks) as JSON.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_get_outline(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let items: Vec<OutlineItemDto> = doc
            .get_outline()?
            .unwrap_or_default()
            .iter()
            .map(OutlineItemDto::from)
            .collect();
        json_string(&items)
    })
}

/// Get the page labels as JSON, such as `[{"page":0,"label":"i"}, ...]`.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_get_page_labels(path: *const c_char) -> *mut c_char {
    ffi_string(|| {
        let doc = open_document(path)?;
        let ranges = PageLabelExtractor::extract(&doc)?;
        let page_count = doc.page_count()?;
        let labels: Vec<PageLabelDto> = PageLabelExtractor::get_all_labels(&ranges, page_count)
            .into_iter()
            .enumerate()
            .map(|(page, label)| PageLabelDto { page, label })
            .collect();
        json_string(&labels)
    })
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

fn page_index_from_i32(page_index: i32) -> Result<usize, Box<dyn std::error::Error>> {
    usize::try_from(page_index).map_err(|_| {
        IoError::new(
            ErrorKind::InvalidInput,
            format!("page index {page_index} must not be negative"),
        )
        .into()
    })
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

fn text_markup_type_from_code(kind: i32) -> Result<TextMarkupType, Box<dyn std::error::Error>> {
    match kind {
        0 => Ok(TextMarkupType::Highlight),
        1 => Ok(TextMarkupType::Underline),
        2 => Ok(TextMarkupType::StrikeOut),
        3 => Ok(TextMarkupType::Squiggly),
        other => Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("unknown text markup kind: {other}"),
        )
        .into()),
    }
}

fn text_annotation_icon_from_code(
    icon: i32,
) -> Result<TextAnnotationIcon, Box<dyn std::error::Error>> {
    match icon {
        0 => Ok(TextAnnotationIcon::Comment),
        1 => Ok(TextAnnotationIcon::Key),
        2 => Ok(TextAnnotationIcon::Note),
        3 => Ok(TextAnnotationIcon::Help),
        4 => Ok(TextAnnotationIcon::NewParagraph),
        5 => Ok(TextAnnotationIcon::Paragraph),
        6 => Ok(TextAnnotationIcon::Insert),
        other => Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("unknown text annotation icon code: {other}"),
        )
        .into()),
    }
}

fn parse_stamp_type(name: &str) -> StampType {
    match name {
        "Approved" => StampType::Approved,
        "Experimental" => StampType::Experimental,
        "NotApproved" => StampType::NotApproved,
        "AsIs" => StampType::AsIs,
        "Expired" => StampType::Expired,
        "NotForPublicRelease" => StampType::NotForPublicRelease,
        "Confidential" => StampType::Confidential,
        "Final" => StampType::Final,
        "Sold" => StampType::Sold,
        "Departmental" => StampType::Departmental,
        "ForComment" => StampType::ForComment,
        "TopSecret" => StampType::TopSecret,
        "Draft" => StampType::Draft,
        "ForPublicRelease" => StampType::ForPublicRelease,
        other => StampType::Custom(other.to_string()),
    }
}

fn parse_barcode_type(
    barcode_type: i32,
) -> Result<pdf_oxide::writer::BarcodeType, Box<dyn std::error::Error>> {
    use pdf_oxide::writer::BarcodeType;
    match barcode_type {
        0 => Ok(BarcodeType::Code128),
        1 => Ok(BarcodeType::Code39),
        2 => Ok(BarcodeType::Ean13),
        3 => Ok(BarcodeType::Ean8),
        4 => Ok(BarcodeType::UpcA),
        5 => Ok(BarcodeType::Itf),
        6 => Ok(BarcodeType::Code93),
        7 => Ok(BarcodeType::Codabar),
        other => Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("unknown barcode type code: {other}"),
        )
        .into()),
    }
}

fn parse_options(
    ptr: *const c_char,
    argument: &str,
) -> Result<Vec<String>, Box<dyn std::error::Error>> {
    let raw = c_string_to_string(ptr, argument)?;
    if raw.is_empty() {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "options must contain at least one entry",
        )
        .into());
    }

    let options: Vec<String> = raw.split('\n').map(str::to_owned).collect();
    if options.is_empty() || options.iter().any(|option| option.is_empty()) {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "options must be newline-separated and must not be empty",
        )
        .into());
    }
    Ok(options)
}

fn require_non_empty(value: &str, argument: &str) -> Result<(), Box<dyn std::error::Error>> {
    if value.is_empty() {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("{argument} must not be empty"),
        )
        .into());
    }
    Ok(())
}

fn validate_color(
    r: f32,
    g: f32,
    b: f32,
) -> Result<(), Box<dyn std::error::Error>> {
    if !r.is_finite() || !g.is_finite() || !b.is_finite() {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "color components must be finite",
        )
        .into());
    }
    Ok(())
}

fn make_line_style(
    width: f32,
    r: f32,
    g: f32,
    b: f32,
    dash: &[f32],
    phase: f32,
) -> Result<LineStyle, Box<dyn std::error::Error>> {
    if !width.is_finite() || width <= 0.0 {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "line width must be a positive finite number",
        )
        .into());
    }
    validate_color(r, g, b)?;
    if !phase.is_finite() {
        return Err(
            IoError::new(ErrorKind::InvalidInput, "dash phase must be finite").into(),
        );
    }
    if dash.iter().any(|value| !value.is_finite() || *value < 0.0) {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "dash pattern values must be non-negative finite numbers",
        )
        .into());
    }

    let style = LineStyle::new(width, r, g, b);
    Ok(if dash.is_empty() {
        style
    } else {
        style.with_dash(dash, phase)
    })
}

fn optional_line_style(
    stroke: &Option<(f32, f32, f32, f32, Vec<f32>, f32)>,
) -> Result<Option<LineStyle>, Box<dyn std::error::Error>> {
    match stroke {
        Some((width, r, g, b, dash, phase)) => {
            Ok(Some(make_line_style(*width, *r, *g, *b, dash, *phase)?))
        }
        None => Ok(None),
    }
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
        fonts: Vec::new(),
        tagged: false,
        language: None,
        role_map: Vec::new(),
        streaming_table: None,
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

/// Return the current number of pages in the builder, or -1 on error.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_get_page_count(state: *mut PdfBuilderState) -> i32 {
    match builder_mut(state) {
        Ok(state) => match i32::try_from(state.pages.len()) {
            Ok(count) => count,
            Err(_) => {
                set_last_error("page count exceeds the supported range");
                -1
            }
        },
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

/// Add text at the current cursor position. A null font uses the default.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_text(
    state: *mut PdfBuilderState,
    page_index: i32,
    text: *const c_char,
    font: *const c_char,
    size: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let text = c_string_to_string(text, "text")?;
        let font = optional_string(font, "font")?;
        let size = (size.is_finite() && size > 0.0).then_some(size);
        state.page_mut(page_index)?.ops.push(BuilderOp::Text {
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
    page_index: i32,
    x: f32,
    y: f32,
    text: *const c_char,
    font: *const c_char,
    size: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !x.is_finite() || !y.is_finite() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "text position must be finite").into(),
            );
        }
        let text = c_string_to_string(text, "text")?;
        let font = optional_string(font, "font")?;
        let size = (size.is_finite() && size > 0.0).then_some(size);
        state.page_mut(page_index)?.ops.push(BuilderOp::Text {
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
    page_index: i32,
    level: i32,
    text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !(1..=6).contains(&level) {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "heading level must be between 1 and 6",
            )
            .into());
        }
        let text = c_string_to_string(text, "text")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::Heading {
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
    page_index: i32,
    text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let text = c_string_to_string(text, "text")?;
        state
            .page_mut(page_index)?
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
    page_index: i32,
    items: *const c_char,
    numbered: i32,
    style: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
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
        state.page_mut(page_index)?.ops.push(op);
        Ok(())
    })
}

/// Add a monospace code block at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_code_block(
    state: *mut PdfBuilderState,
    page_index: i32,
    language: *const c_char,
    source: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let language = c_string_to_string(language, "language")?;
        let source = c_string_to_string(source, "source")?;
        state
            .page_mut(page_index)?
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
    page_index: i32,
    texts: *const *const c_char,
    styles: *const i32,
    colors: *const f32,
    len: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
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
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::RichParagraph(runs));
        Ok(())
    })
}

/// Add vertical space at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_space(
    state: *mut PdfBuilderState,
    page_index: i32,
    points: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !points.is_finite() || points < 0.0 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "space must be a non-negative finite number",
            )
            .into());
        }
        state.page_mut(page_index)?.ops.push(BuilderOp::Space(points));
        Ok(())
    })
}

/// Add a horizontal rule at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_horizontal_rule(
    state: *mut PdfBuilderState,
    page_index: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::HorizontalRule);
        Ok(())
    })
}

/// Lay text out across multiple balanced columns.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_columns(
    state: *mut PdfBuilderState,
    page_index: i32,
    count: i32,
    gap: f32,
    text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
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
        state.page_mut(page_index)?.ops.push(BuilderOp::Columns {
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
    page_index: i32,
    cells: *const *const c_char,
    rows: i32,
    cols: i32,
    header: i32,
    style: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
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

        state.page_mut(page_index)?.ops.push(BuilderOp::Table {
            rows: table_rows,
            header: header != 0,
            style,
        });
        Ok(())
    })
}

fn cell_align_from_code(align: i32) -> Result<CellAlign, Box<dyn std::error::Error>> {
    match align {
        0 => Ok(CellAlign::Left),
        1 => Ok(CellAlign::Center),
        2 => Ok(CellAlign::Right),
        other => Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("unknown cell alignment code: {other}"),
        )
        .into()),
    }
}

/// Begin a streaming table on a page.
///
/// `headers` is a length-`column_count` array of null-terminated C strings;
/// `widths` and `aligns` are length-`column_count` arrays (aligns: 0=Left,
/// 1=Center, 2=Right). Rows are appended with
/// `pdf_builder_streaming_table_add_row` and the table is committed with
/// `pdf_builder_streaming_table_end`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_streaming_table_begin(
    state: *mut PdfBuilderState,
    page_index: i32,
    headers: *const *const c_char,
    widths: *const f32,
    aligns: *const i32,
    column_count: i32,
    repeat_header: i32,
    header_fill_flag: i32,
    hr: f32,
    hg: f32,
    hb: f32,
    grid_flag: i32,
    gr: f32,
    gg: f32,
    gb: f32,
    grid_width: f32,
    pad_h: f32,
    pad_top: f32,
    pad_bottom: f32,
    max_rowspan: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if state.streaming_table.is_some() {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "a streaming table is already open",
            )
            .into());
        }
        if column_count <= 0 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "column count must be positive",
            )
            .into());
        }
        if headers.is_null() || widths.is_null() || aligns.is_null() {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "streaming table column arrays must not be null",
            )
            .into());
        }
        if page_index >= state.pages.len() {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                format!("page index {page_index} is out of range"),
            )
            .into());
        }

        let count = column_count as usize;
        let widths_slice = unsafe { std::slice::from_raw_parts(widths, count) };
        let aligns_slice = unsafe { std::slice::from_raw_parts(aligns, count) };
        let headers_slice = unsafe { std::slice::from_raw_parts(headers, count) };

        let mut config = StreamingTableConfig::new();
        let mut columns: Vec<(String, f32)> = Vec::with_capacity(count);
        for index in 0..count {
            let width = widths_slice[index];
            if !width.is_finite() || width <= 0.0 {
                return Err(IoError::new(
                    ErrorKind::InvalidInput,
                    "column widths must be positive finite numbers",
                )
                .into());
            }
            let align = cell_align_from_code(aligns_slice[index])?;
            let header = c_string_to_string(headers_slice[index], "column header")?;
            columns.push((header.clone(), width));
            config = config.column(
                StreamingColumn::new(header)
                    .width_pt(width)
                    .align(align),
            );
        }

        config = config.repeat_header(repeat_header != 0);
        config = config.header_fill(if header_fill_flag != 0 {
            Some((hr, hg, hb))
        } else {
            None
        });
        config = if grid_flag != 0 {
            config.grid((gr, gg, gb), grid_width)
        } else {
            config.grid((0.0, 0.0, 0.0), 0.0)
        };
        config = config.cell_padding(pad_h, pad_top, pad_bottom);
        config = config.max_rowspan(usize::try_from(max_rowspan).unwrap_or(1));

        state.streaming_table = Some(PendingStreamingTable {
            page_index,
            config,
            column_count: count,
            rows: Vec::new(),
            columns,
            repeat_header: repeat_header != 0,
            pad_h,
            pad_top,
            pad_bottom,
        });
        Ok(())
    })
}

/// Append a row to the open streaming table. `cells` is a length-`count`
/// array of null-terminated C strings; `rowspans` may be null (all cells span
/// one row) or provide a rowspan per cell.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_streaming_table_add_row(
    state: *mut PdfBuilderState,
    cells: *const *const c_char,
    rowspans: *const i32,
    count: i32,
) -> i32 {
    with_builder(state, |state| {
        if cells.is_null() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "table cells must not be null").into(),
            );
        }
        if count <= 0 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "cell count must be positive",
            )
            .into());
        }

        let pending = state.streaming_table.as_mut().ok_or_else(|| {
            IoError::new(ErrorKind::InvalidInput, "no streaming table is open")
        })?;
        let count = count as usize;
        if count != pending.column_count {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                format!(
                    "row has {count} cells, expected {}",
                    pending.column_count
                ),
            )
            .into());
        }

        let cells_slice = unsafe { std::slice::from_raw_parts(cells, count) };
        let mut row = Vec::with_capacity(count);
        for index in 0..count {
            let text = c_string_to_string(cells_slice[index], "table cell")?;
            let span = if rowspans.is_null() {
                1
            } else {
                let raw = unsafe { *rowspans.add(index) };
                usize::try_from(raw).unwrap_or(1)
            };
            row.push((text, span.max(1)));
        }
        pending.rows.push(row);
        Ok(())
    })
}

/// Commit the open streaming table into its page's operation list.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_streaming_table_end(state: *mut PdfBuilderState) -> i32 {
    with_builder(state, |state| {
        let pending = state.streaming_table.take().ok_or_else(|| {
            IoError::new(ErrorKind::InvalidInput, "no streaming table is open")
        })?;
        state
            .page_mut(pending.page_index)?
            .ops
            .push(BuilderOp::StreamingTable {
                config: pending.config,
                rows: pending.rows,
                columns: pending.columns,
                repeat_header: pending.repeat_header,
                pad_h: pending.pad_h,
                pad_top: pending.pad_top,
                pad_bottom: pending.pad_bottom,
            });
        Ok(())
    })
}

/// Add a page with the same kind as the reference page and return its index.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_page_like(
    state: *mut PdfBuilderState,
    reference_page_index: i32,
) -> i32 {
    let result = (|| -> Result<i32, Box<dyn std::error::Error>> {
        let state = builder_mut(state)?;
        let reference = page_index_from_i32(reference_page_index)?;
        let kind = state
            .pages
            .get(reference)
            .ok_or_else(|| {
                IoError::new(
                    ErrorKind::InvalidInput,
                    format!("page index {reference} is out of range"),
                )
            })?
            .kind;
        let new_index = state.pages.len();
        state.pages.push(PageSpec {
            kind,
            ops: Vec::new(),
        });
        i32::try_from(new_index)
            .map_err(|_| IoError::new(ErrorKind::InvalidInput, "page index is too large").into())
    })();

    match result {
        Ok(index) => index,
        Err(e) => {
            set_last_error(e);
            -1
        }
    }
}

/// Add an image file at a rectangle. `alt` may be null.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_image_file(
    state: *mut PdfBuilderState,
    page_index: i32,
    path: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    alt: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        validate_rect(x, y, width, height)?;
        let path = c_string_to_string(path, "path")?;
        let data = std::fs::read(&path)?;
        let alt = optional_string(alt, "alt")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::Image {
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
    page_index: i32,
    data: *const u8,
    len: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    alt: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
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
        state.page_mut(page_index)?.ops.push(BuilderOp::Image {
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

/// Add a URL link annotation to the most recent text on the page.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_link_url(
    state: *mut PdfBuilderState,
    page_index: i32,
    url: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let url = c_string_to_string(url, "url")?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::LinkUrl(url));
        Ok(())
    })
}

/// Add an internal page link annotation to the most recent text on the page.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_link_page(
    state: *mut PdfBuilderState,
    page_index: i32,
    target_page: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let target_page = page_index_from_i32(target_page)?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::LinkPage(target_page));
        Ok(())
    })
}

/// Add a named destination link annotation to the most recent text on the page.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_link_named(
    state: *mut PdfBuilderState,
    page_index: i32,
    destination: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let destination = c_string_to_string(destination, "destination")?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::LinkNamed(destination));
        Ok(())
    })
}

/// Add a JavaScript action link annotation to the most recent text on the page.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_link_javascript(
    state: *mut PdfBuilderState,
    page_index: i32,
    script: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let script = c_string_to_string(script, "script")?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::LinkJavaScript(script));
        Ok(())
    })
}

/// Add a URL link annotation over a rectangle.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_link_rect(
    state: *mut PdfBuilderState,
    page_index: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    url: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        validate_rect(x, y, width, height)?;
        let url = c_string_to_string(url, "url")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::LinkRect {
            x,
            y,
            width,
            height,
            url,
        });
        Ok(())
    })
}

/// Add a text markup annotation to the most recent text on the page.
///
/// `kind` is 0=Highlight, 1=Underline, 2=StrikeOut, 3=Squiggly.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_text_markup(
    state: *mut PdfBuilderState,
    page_index: i32,
    kind: i32,
    r: f32,
    g: f32,
    b: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        text_markup_type_from_code(kind)?;
        validate_color(r, g, b)?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::TextMarkup { kind, r, g, b });
        Ok(())
    })
}

/// Add a text markup annotation over a rectangle.
///
/// `kind` is 0=Highlight, 1=Underline, 2=StrikeOut, 3=Squiggly.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_text_markup_rect(
    state: *mut PdfBuilderState,
    page_index: i32,
    kind: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    r: f32,
    g: f32,
    b: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        text_markup_type_from_code(kind)?;
        validate_rect(x, y, width, height)?;
        validate_color(r, g, b)?;
        state.page_mut(page_index)?.ops.push(BuilderOp::TextMarkupRect {
            kind,
            x,
            y,
            width,
            height,
            r,
            g,
            b,
        });
        Ok(())
    })
}

/// Add a sticky note annotation at the current cursor position.
///
/// `icon` is 0=Comment, 1=Key, 2=Note, 3=Help, 4=NewParagraph, 5=Paragraph,
/// 6=Insert.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_sticky_note(
    state: *mut PdfBuilderState,
    page_index: i32,
    text: *const c_char,
    icon: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        text_annotation_icon_from_code(icon)?;
        let text = c_string_to_string(text, "text")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::StickyNote {
            x: None,
            y: None,
            text,
            icon,
        });
        Ok(())
    })
}

/// Add a sticky note annotation at an absolute position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_sticky_note_at(
    state: *mut PdfBuilderState,
    page_index: i32,
    x: f32,
    y: f32,
    text: *const c_char,
    icon: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !x.is_finite() || !y.is_finite() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "sticky note position must be finite").into(),
            );
        }
        text_annotation_icon_from_code(icon)?;
        let text = c_string_to_string(text, "text")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::StickyNote {
            x: Some(x),
            y: Some(y),
            text,
            icon,
        });
        Ok(())
    })
}

/// Add a free text annotation over a rectangle. `font` may be null.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_freetext(
    state: *mut PdfBuilderState,
    page_index: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    text: *const c_char,
    font: *const c_char,
    size: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        validate_rect(x, y, width, height)?;
        let text = c_string_to_string(text, "text")?;
        let font = optional_string(font, "font")?;
        let size = (size.is_finite() && size > 0.0).then_some(size);
        state.page_mut(page_index)?.ops.push(BuilderOp::FreeText {
            x,
            y,
            width,
            height,
            text,
            font,
            size,
        });
        Ok(())
    })
}

/// Add a text watermark centered on the page.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_watermark(
    state: *mut PdfBuilderState,
    page_index: i32,
    text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let text = c_string_to_string(text, "text")?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::Watermark { text });
        Ok(())
    })
}

/// Add a preset watermark. `kind` is 0=Confidential, 1=Draft.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_watermark_preset(
    state: *mut PdfBuilderState,
    page_index: i32,
    kind: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if kind != 0 && kind != 1 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                format!("unknown watermark preset code: {kind}"),
            )
            .into());
        }
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::WatermarkPreset { kind });
        Ok(())
    })
}

/// Add a fully custom watermark. When `has_rect` is non-zero the rectangle
/// values are used; otherwise the watermark uses the writer default rectangle.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_watermark_custom(
    state: *mut PdfBuilderState,
    page_index: i32,
    text: *const c_char,
    opacity: f32,
    rotation: f32,
    font: *const c_char,
    size: f32,
    r: f32,
    g: f32,
    b: f32,
    has_rect: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let text = c_string_to_string(text, "text")?;
        let font = c_string_to_string(font, "font")?;
        if !opacity.is_finite() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "watermark opacity must be finite").into(),
            );
        }
        if !rotation.is_finite() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "watermark rotation must be finite").into(),
            );
        }
        if !size.is_finite() || size <= 0.0 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "watermark font size must be a positive finite number",
            )
            .into());
        }
        validate_color(r, g, b)?;
        let rect = if has_rect != 0 {
            validate_rect(x, y, width, height)?;
            Some((x, y, width, height))
        } else {
            None
        };
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::WatermarkCustom {
                text,
                opacity,
                rotation,
                font,
                size,
                r,
                g,
                b,
                rect,
            });
        Ok(())
    })
}

/// Add a stamp annotation at the current cursor position.
///
/// Pass an empty string to use the default draft stamp.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_stamp(
    state: *mut PdfBuilderState,
    page_index: i32,
    type_name: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let type_name = c_string_to_string(type_name, "type_name")?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::Stamp { type_name });
        Ok(())
    })
}

/// Add a stamp annotation over a rectangle.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_stamp_at(
    state: *mut PdfBuilderState,
    page_index: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    type_name: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        validate_rect(x, y, width, height)?;
        let type_name = c_string_to_string(type_name, "type_name")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::StampAt {
            x,
            y,
            width,
            height,
            type_name,
        });
        Ok(())
    })
}

/// Add a footnote at the current cursor position.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_footnote(
    state: *mut PdfBuilderState,
    page_index: i32,
    ref_mark: *const c_char,
    note_text: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let ref_mark = c_string_to_string(ref_mark, "reference mark")?;
        let note_text = c_string_to_string(note_text, "note text")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::Footnote {
            ref_mark,
            note_text,
        });
        Ok(())
    })
}

/// Add a single-line text form field. `default_value` may be null.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_text_field(
    state: *mut PdfBuilderState,
    page_index: i32,
    name: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    default_value: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        validate_rect(x, y, width, height)?;
        let default_value = optional_string(default_value, "default value")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::FormTextField {
            name,
            x,
            y,
            width,
            height,
            default_value,
        });
        Ok(())
    })
}

/// Add a checkbox form field.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_checkbox(
    state: *mut PdfBuilderState,
    page_index: i32,
    name: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    checked: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        validate_rect(x, y, width, height)?;
        state.page_mut(page_index)?.ops.push(BuilderOp::FormCheckbox {
            name,
            x,
            y,
            width,
            height,
            checked: checked != 0,
        });
        Ok(())
    })
}

/// Add a combo-box form field.
///
/// Options are supplied as a newline-separated string. Option values must not
/// contain newlines; this ambiguity is rejected indirectly by empty entries.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_combo_box(
    state: *mut PdfBuilderState,
    page_index: i32,
    name: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    options: *const c_char,
    selected: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        validate_rect(x, y, width, height)?;
        let options = parse_options(options, "options")?;
        let selected = optional_string(selected, "selected")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::FormComboBox {
            name,
            x,
            y,
            width,
            height,
            options,
            selected,
        });
        Ok(())
    })
}

/// Add a list-box form field.
///
/// Options are supplied as a newline-separated string. Option values must not
/// contain newlines; this ambiguity is rejected indirectly by empty entries.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_list_box(
    state: *mut PdfBuilderState,
    page_index: i32,
    name: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    options: *const c_char,
    selected: *const c_char,
    multi_select: i32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        validate_rect(x, y, width, height)?;
        let options = parse_options(options, "options")?;
        let selected = optional_string(selected, "selected")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::FormListBox {
            name,
            x,
            y,
            width,
            height,
            options,
            selected,
            multi_select: multi_select != 0,
        });
        Ok(())
    })
}

/// Add a radio-button group.
///
/// # Safety
/// `values`, `xs`, `ys`, `ws`, and `hs` must point to arrays of at least
/// `count` elements.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_builder_add_radio_group(
    state: *mut PdfBuilderState,
    page_index: i32,
    name: *const c_char,
    values: *const *const c_char,
    xs: *const f32,
    ys: *const f32,
    ws: *const f32,
    hs: *const f32,
    count: i32,
    selected: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        if count <= 0 {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "radio button count must be positive").into(),
            );
        }
        if values.is_null() || xs.is_null() || ys.is_null() || ws.is_null() || hs.is_null() {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "radio button data must not be null",
            )
            .into());
        }

        let mut buttons = Vec::with_capacity(count as usize);
        for index in 0..count as usize {
            let value = c_string_to_string(unsafe { *values.add(index) }, "radio option")?;
            require_non_empty(&value, "radio option")?;
            let x = unsafe { *xs.add(index) };
            let y = unsafe { *ys.add(index) };
            let width = unsafe { *ws.add(index) };
            let height = unsafe { *hs.add(index) };
            validate_rect(x, y, width, height)?;
            buttons.push((value, x, y, width, height));
        }

        let selected = optional_string(selected, "selected")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::FormRadioGroup {
            name,
            buttons,
            selected,
        });
        Ok(())
    })
}

/// Add a push-button form field with a visible caption.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_push_button(
    state: *mut PdfBuilderState,
    page_index: i32,
    name: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    caption: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        validate_rect(x, y, width, height)?;
        let caption = c_string_to_string(caption, "caption")?;
        state.page_mut(page_index)?.ops.push(BuilderOp::FormPushButton {
            name,
            x,
            y,
            width,
            height,
            caption,
        });
        Ok(())
    })
}

/// Add an unsigned signature placeholder field.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_signature_field(
    state: *mut PdfBuilderState,
    page_index: i32,
    name: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        validate_rect(x, y, width, height)?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::FormSignatureField {
                name,
                x,
                y,
                width,
                height,
            });
        Ok(())
    })
}

/// Add a square QR code image.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_qr_code(
    state: *mut PdfBuilderState,
    page_index: i32,
    data: *const c_char,
    x: f32,
    y: f32,
    size: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let data = c_string_to_string(data, "data")?;
        require_non_empty(&data, "data")?;
        validate_rect(x, y, size, size)?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::BarcodeQr { data, x, y, size });
        Ok(())
    })
}

/// Add a 1-D barcode image. `barcode_type` is 0=Code128, 1=Code39, 2=Ean13,
/// 3=Ean8, 4=UpcA, 5=Itf, 6=Code93, 7=Codabar.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_barcode(
    state: *mut PdfBuilderState,
    page_index: i32,
    barcode_type: i32,
    data: *const c_char,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        parse_barcode_type(barcode_type)?;
        let data = c_string_to_string(data, "data")?;
        require_non_empty(&data, "data")?;
        validate_rect(x, y, width, height)?;
        state.page_mut(page_index)?.ops.push(BuilderOp::Barcode1d {
            barcode_type,
            data,
            x,
            y,
            width,
            height,
        });
        Ok(())
    })
}

fn read_dash_pattern(
    dash: *const f32,
    dash_len: i32,
) -> Result<Vec<f32>, Box<dyn std::error::Error>> {
    if dash_len < 0 {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "dash length must not be negative",
        )
        .into());
    }
    if dash_len == 0 {
        return Ok(Vec::new());
    }
    if dash.is_null() {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "dash pointer must not be null when dash length is non-zero",
        )
        .into());
    }
    let values = unsafe { std::slice::from_raw_parts(dash, dash_len as usize) }.to_vec();
    if values.iter().any(|value| !value.is_finite() || *value < 0.0) {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "dash pattern values must be non-negative finite numbers",
        )
        .into());
    }
    Ok(values)
}

fn read_float_array(
    values: *const f32,
    count: usize,
    argument: &str,
) -> Result<Vec<f32>, Box<dyn std::error::Error>> {
    if count == 0 {
        return Ok(Vec::new());
    }
    if values.is_null() {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("{argument} pointer must not be null"),
        )
        .into());
    }
    let slice = unsafe { std::slice::from_raw_parts(values, count) };
    if slice.iter().any(|value| !value.is_finite()) {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            format!("{argument} values must be finite"),
        )
        .into());
    }
    Ok(slice.to_vec())
}

fn read_points(
    coords: *const f32,
    point_count: i32,
) -> Result<Vec<(f32, f32)>, Box<dyn std::error::Error>> {
    if point_count < 2 {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "polygon must contain at least two points",
        )
        .into());
    }
    let coord_count = (point_count as usize)
        .checked_mul(2)
        .ok_or_else(|| IoError::new(ErrorKind::InvalidInput, "point count is too large"))?;
    let values = read_float_array(coords, coord_count, "polygon coordinates")?;
    Ok(values
        .chunks_exact(2)
        .map(|pair| (pair[0], pair[1]))
        .collect())
}

fn read_path_operations(
    kinds: *const i32,
    coords: *const f32,
    op_count: i32,
) -> Result<Vec<PathOperation>, Box<dyn std::error::Error>> {
    if op_count < 0 {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "path operation count must not be negative",
        )
        .into());
    }
    if op_count == 0 {
        return Ok(Vec::new());
    }
    if kinds.is_null() {
        return Err(IoError::new(
            ErrorKind::InvalidInput,
            "path kinds pointer must not be null",
        )
        .into());
    }

    let kinds = unsafe { std::slice::from_raw_parts(kinds, op_count as usize) };
    let mut required = 0usize;
    for kind in kinds {
        let add = match kind {
            0 | 1 => 2,
            2 => 6,
            3 => 4,
            4 => 0,
            other => {
                return Err(IoError::new(
                    ErrorKind::InvalidInput,
                    format!("unknown path operation kind: {other}"),
                )
                .into());
            }
        };
        required = required
            .checked_add(add)
            .ok_or_else(|| IoError::new(ErrorKind::InvalidInput, "path is too large"))?;
    }

    let coords = read_float_array(coords, required, "path coordinates")?;
    let mut operations = Vec::with_capacity(kinds.len());
    let mut offset = 0usize;
    for kind in kinds {
        match kind {
            0 => {
                operations.push(PathOperation::MoveTo(coords[offset], coords[offset + 1]));
                offset += 2;
            }
            1 => {
                operations.push(PathOperation::LineTo(coords[offset], coords[offset + 1]));
                offset += 2;
            }
            2 => {
                operations.push(PathOperation::CurveTo(
                    coords[offset],
                    coords[offset + 1],
                    coords[offset + 2],
                    coords[offset + 3],
                    coords[offset + 4],
                    coords[offset + 5],
                ));
                offset += 6;
            }
            3 => {
                operations.push(PathOperation::Rectangle(
                    coords[offset],
                    coords[offset + 1],
                    coords[offset + 2],
                    coords[offset + 3],
                ));
                offset += 4;
            }
            4 => operations.push(PathOperation::ClosePath),
            _ => unreachable!(),
        }
    }

    Ok(operations)
}

/// Add a stroked rectangle with the default 1pt black style.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_rect(
    state: *mut PdfBuilderState,
    page_index: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        validate_rect(x, y, width, height)?;
        state.page_mut(page_index)?.ops.push(BuilderOp::VecRect {
            x,
            y,
            width,
            height,
        });
        Ok(())
    })
}

/// Add a filled rectangle.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_filled_rect(
    state: *mut PdfBuilderState,
    page_index: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    r: f32,
    g: f32,
    b: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        validate_rect(x, y, width, height)?;
        validate_color(r, g, b)?;
        state.page_mut(page_index)?.ops.push(BuilderOp::VecFilledRect {
            x,
            y,
            width,
            height,
            r,
            g,
            b,
        });
        Ok(())
    })
}

/// Add a straight line with the default 1pt black style.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_line(
    state: *mut PdfBuilderState,
    page_index: i32,
    x1: f32,
    y1: f32,
    x2: f32,
    y2: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !x1.is_finite() || !y1.is_finite() || !x2.is_finite() || !y2.is_finite() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "line coordinates must be finite").into(),
            );
        }
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::VecLine { x1, y1, x2, y2 });
        Ok(())
    })
}

/// Add a stroked rectangle with a caller-supplied line style.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_stroke_rect(
    state: *mut PdfBuilderState,
    page_index: i32,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    line_width: f32,
    r: f32,
    g: f32,
    b: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        validate_rect(x, y, width, height)?;
        let dash = read_dash_pattern(dash, dash_len)?;
        make_line_style(line_width, r, g, b, &dash, phase)?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::VecStrokeRect {
                x,
                y,
                width,
                height,
                width_style: line_width,
                r,
                g,
                b,
                dash,
                phase,
            });
        Ok(())
    })
}

/// Add a stroked line with a caller-supplied line style.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_stroke_line(
    state: *mut PdfBuilderState,
    page_index: i32,
    x1: f32,
    y1: f32,
    x2: f32,
    y2: f32,
    line_width: f32,
    r: f32,
    g: f32,
    b: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !x1.is_finite() || !y1.is_finite() || !x2.is_finite() || !y2.is_finite() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "line coordinates must be finite").into(),
            );
        }
        let dash = read_dash_pattern(dash, dash_len)?;
        make_line_style(line_width, r, g, b, &dash, phase)?;
        state
            .page_mut(page_index)?
            .ops
            .push(BuilderOp::VecStrokeLine {
                x1,
                y1,
                x2,
                y2,
                width_style: line_width,
                r,
                g,
                b,
                dash,
                phase,
            });
        Ok(())
    })
}

/// Add a circle with optional stroke and fill.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_circle(
    state: *mut PdfBuilderState,
    page_index: i32,
    cx: f32,
    cy: f32,
    radius: f32,
    has_stroke: i32,
    line_width: f32,
    sr: f32,
    sg: f32,
    sb: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
    has_fill: i32,
    fr: f32,
    fg: f32,
    fb: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !cx.is_finite() || !cy.is_finite() || !radius.is_finite() || radius <= 0.0 {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "circle radius must be positive and all values finite",
            )
            .into());
        }
        let dash = read_dash_pattern(dash, dash_len)?;
        let stroke = if has_stroke != 0 {
            make_line_style(line_width, sr, sg, sb, &dash, phase)?;
            Some((line_width, sr, sg, sb, dash, phase))
        } else {
            None
        };
        let fill = if has_fill != 0 {
            validate_color(fr, fg, fb)?;
            Some((fr, fg, fb))
        } else {
            None
        };
        state.page_mut(page_index)?.ops.push(BuilderOp::VecCircle {
            cx,
            cy,
            radius,
            stroke,
            fill,
        });
        Ok(())
    })
}

/// Add an ellipse with optional stroke and fill.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_ellipse(
    state: *mut PdfBuilderState,
    page_index: i32,
    cx: f32,
    cy: f32,
    rx: f32,
    ry: f32,
    has_stroke: i32,
    line_width: f32,
    sr: f32,
    sg: f32,
    sb: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
    has_fill: i32,
    fr: f32,
    fg: f32,
    fb: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !cx.is_finite()
            || !cy.is_finite()
            || !rx.is_finite()
            || !ry.is_finite()
            || rx <= 0.0
            || ry <= 0.0
        {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "ellipse radii must be positive and all values finite",
            )
            .into());
        }
        let dash = read_dash_pattern(dash, dash_len)?;
        let stroke = if has_stroke != 0 {
            make_line_style(line_width, sr, sg, sb, &dash, phase)?;
            Some((line_width, sr, sg, sb, dash, phase))
        } else {
            None
        };
        let fill = if has_fill != 0 {
            validate_color(fr, fg, fb)?;
            Some((fr, fg, fb))
        } else {
            None
        };
        state.page_mut(page_index)?.ops.push(BuilderOp::VecEllipse {
            cx,
            cy,
            rx,
            ry,
            stroke,
            fill,
        });
        Ok(())
    })
}

/// Add a polygon through a flat `(x, y)` coordinate array.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_polygon(
    state: *mut PdfBuilderState,
    page_index: i32,
    coords: *const f32,
    point_count: i32,
    has_stroke: i32,
    line_width: f32,
    sr: f32,
    sg: f32,
    sb: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
    has_fill: i32,
    fr: f32,
    fg: f32,
    fb: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let points = read_points(coords, point_count)?;
        let dash = read_dash_pattern(dash, dash_len)?;
        let stroke = if has_stroke != 0 {
            make_line_style(line_width, sr, sg, sb, &dash, phase)?;
            Some((line_width, sr, sg, sb, dash, phase))
        } else {
            None
        };
        let fill = if has_fill != 0 {
            validate_color(fr, fg, fb)?;
            Some((fr, fg, fb))
        } else {
            None
        };
        state.page_mut(page_index)?.ops.push(BuilderOp::VecPolygon {
            points,
            stroke,
            fill,
        });
        Ok(())
    })
}

/// Add a stroked circular arc. Angles are in radians.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_arc(
    state: *mut PdfBuilderState,
    page_index: i32,
    cx: f32,
    cy: f32,
    radius: f32,
    start_radians: f32,
    end_radians: f32,
    line_width: f32,
    r: f32,
    g: f32,
    b: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !cx.is_finite()
            || !cy.is_finite()
            || !radius.is_finite()
            || radius <= 0.0
            || !start_radians.is_finite()
            || !end_radians.is_finite()
        {
            return Err(IoError::new(
                ErrorKind::InvalidInput,
                "arc values must be finite and radius must be positive",
            )
            .into());
        }
        let dash = read_dash_pattern(dash, dash_len)?;
        make_line_style(line_width, r, g, b, &dash, phase)?;
        state.page_mut(page_index)?.ops.push(BuilderOp::VecArc {
            cx,
            cy,
            radius,
            start_radians,
            end_radians,
            width_style: line_width,
            r,
            g,
            b,
            dash,
            phase,
        });
        Ok(())
    })
}

/// Add a cubic Bezier curve with optional stroke and fill.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_bezier(
    state: *mut PdfBuilderState,
    page_index: i32,
    x0: f32,
    y0: f32,
    c1x: f32,
    c1y: f32,
    c2x: f32,
    c2y: f32,
    x3: f32,
    y3: f32,
    has_stroke: i32,
    line_width: f32,
    sr: f32,
    sg: f32,
    sb: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
    has_fill: i32,
    fr: f32,
    fg: f32,
    fb: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        if !x0.is_finite()
            || !y0.is_finite()
            || !c1x.is_finite()
            || !c1y.is_finite()
            || !c2x.is_finite()
            || !c2y.is_finite()
            || !x3.is_finite()
            || !y3.is_finite()
        {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "bezier coordinates must be finite").into(),
            );
        }
        let dash = read_dash_pattern(dash, dash_len)?;
        let stroke = if has_stroke != 0 {
            make_line_style(line_width, sr, sg, sb, &dash, phase)?;
            Some((line_width, sr, sg, sb, dash, phase))
        } else {
            None
        };
        let fill = if has_fill != 0 {
            validate_color(fr, fg, fb)?;
            Some((fr, fg, fb))
        } else {
            None
        };
        state.page_mut(page_index)?.ops.push(BuilderOp::VecBezier {
            x0,
            y0,
            c1x,
            c1y,
            c2x,
            c2y,
            x3,
            y3,
            stroke,
            fill,
        });
        Ok(())
    })
}

/// Add a generic path from operation kinds and a flat coordinate array.
///
/// Kinds: 0=MoveTo(2), 1=LineTo(2), 2=CurveTo(6), 3=Rectangle(4), 4=ClosePath(0).
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_path(
    state: *mut PdfBuilderState,
    page_index: i32,
    kinds: *const i32,
    coords: *const f32,
    op_count: i32,
    has_stroke: i32,
    line_width: f32,
    sr: f32,
    sg: f32,
    sb: f32,
    dash: *const f32,
    dash_len: i32,
    phase: f32,
    has_fill: i32,
    fr: f32,
    fg: f32,
    fb: f32,
) -> i32 {
    with_builder(state, |state| {
        let page_index = page_index_from_i32(page_index)?;
        let operations = read_path_operations(kinds, coords, op_count)?;
        let dash = read_dash_pattern(dash, dash_len)?;
        let stroke = if has_stroke != 0 {
            make_line_style(line_width, sr, sg, sb, &dash, phase)?;
            Some((line_width, sr, sg, sb, dash, phase))
        } else {
            None
        };
        let fill = if has_fill != 0 {
            validate_color(fr, fg, fb)?;
            Some((fr, fg, fb))
        } else {
            None
        };
        state.page_mut(page_index)?.ops.push(BuilderOp::VecPath {
            operations,
            stroke,
            fill,
        });
        Ok(())
    })
}

/// Register an embedded font from a file path.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_register_font_file(
    state: *mut PdfBuilderState,
    name: *const c_char,
    path: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        let path = c_string_to_string(path, "path")?;
        require_non_empty(&path, "path")?;
        let data = std::fs::read(&path)?;
        if data.is_empty() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "font file must not be empty").into(),
            );
        }
        state.fonts.push(BuilderFont { name, data });
        Ok(())
    })
}

/// Register an embedded font from memory.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_register_font_bytes(
    state: *mut PdfBuilderState,
    name: *const c_char,
    data: *const u8,
    len: i32,
) -> i32 {
    with_builder(state, |state| {
        let name = c_string_to_string(name, "name")?;
        require_non_empty(&name, "name")?;
        if len <= 0 {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "font data must not be empty").into(),
            );
        }
        if data.is_null() {
            return Err(
                IoError::new(ErrorKind::InvalidInput, "font data must not be null").into(),
            );
        }
        let bytes = unsafe { std::slice::from_raw_parts(data, len as usize) }.to_vec();
        state.fonts.push(BuilderFont { name, data: bytes });
        Ok(())
    })
}

/// Register the bundled DejaVu Sans fonts.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_register_bundled_fonts(state: *mut PdfBuilderState) -> i32 {
    with_builder(state, |state| {
        let fonts = [
            ("DejaVuSans", DEJAVU_SANS),
            ("DejaVuSans-Bold", DEJAVU_SANS_BOLD),
        ];
        for (name, data) in fonts {
            if !state.fonts.iter().any(|font| font.name == name) {
                state.fonts.push(BuilderFont {
                    name: name.to_string(),
                    data: data.to_vec(),
                });
            }
        }
        Ok(())
    })
}

/// Enable or disable PDF/UA-1 tagging.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_set_tagged_pdf_ua1(
    state: *mut PdfBuilderState,
    enabled: i32,
) -> i32 {
    with_builder(state, |state| {
        state.tagged = enabled != 0;
        Ok(())
    })
}

/// Set the document language tag.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_set_language(
    state: *mut PdfBuilderState,
    lang: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let lang = c_string_to_string(lang, "lang")?;
        require_non_empty(&lang, "lang")?;
        state.language = Some(lang);
        Ok(())
    })
}

/// Add a custom-to-standard role map entry.
#[unsafe(no_mangle)]
pub extern "C" fn pdf_builder_add_role_map(
    state: *mut PdfBuilderState,
    custom: *const c_char,
    standard: *const c_char,
) -> i32 {
    with_builder(state, |state| {
        let custom = c_string_to_string(custom, "custom")?;
        require_non_empty(&custom, "custom")?;
        let standard = c_string_to_string(standard, "standard")?;
        require_non_empty(&standard, "standard")?;
        state.role_map.push((custom, standard));
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

/// Free a buffer returned by any function that writes to `out_data`/`out_len`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn pdf_bytes_free(data: *mut u8, len: i32) {
    if data.is_null() || len <= 0 {
        return;
    }
    drop(unsafe { Box::from_raw(std::ptr::slice_from_raw_parts_mut(data, len as usize)) });
}

// ============================================================================
// DOCUMENT EDITOR PASSWORD SHIMS
// ============================================================================

fn editor_open_with_password_internal(
    path: *const c_char,
    password: *const c_char,
) -> Result<DocumentEditor, Box<dyn std::error::Error>> {
    let path_str = c_string_to_string(path, "path")?;
    let password_str = c_string_to_string(password, "password")?;
    let doc = PdfDocument::open(&path_str)?;
    if !doc.authenticate(password_str.as_bytes())? {
        return Err(IoError::new(ErrorKind::InvalidInput, "password authentication failed").into());
    }
    Ok(DocumentEditor::from_document(doc)?)
}

fn editor_open_from_bytes_with_password_internal(
    data: *const u8,
    len: usize,
    password: *const c_char,
) -> Result<DocumentEditor, Box<dyn std::error::Error>> {
    if data.is_null() || len == 0 {
        return Err(IoError::new(ErrorKind::InvalidInput, "data must not be null or empty").into());
    }
    let password_str = c_string_to_string(password, "password")?;
    let bytes = unsafe { std::slice::from_raw_parts(data, len) }.to_vec();
    let doc = PdfDocument::from_bytes(bytes)?;
    if !doc.authenticate(password_str.as_bytes())? {
        return Err(IoError::new(ErrorKind::InvalidInput, "password authentication failed").into());
    }
    Ok(DocumentEditor::from_document(doc)?)
}

fn editor_handle_result(
    result: Result<DocumentEditor, Box<dyn std::error::Error>>,
    error_code: *mut i32,
) -> *mut std::ffi::c_void {
    match result {
        Ok(editor) => {
            if !error_code.is_null() {
                unsafe { *error_code = 0; }
            }
            Box::into_raw(Box::new(editor)) as *mut std::ffi::c_void
        }
        Err(e) => {
            set_last_error(&e);
            if !error_code.is_null() {
                unsafe { *error_code = -1; }
            }
            std::ptr::null_mut()
        }
    }
}

/// Open a password-protected PDF for editing.
#[unsafe(no_mangle)]
pub extern "C" fn pdfoxide_editor_open_with_password(
    path: *const c_char,
    password: *const c_char,
    error_code: *mut i32,
) -> *mut std::ffi::c_void {
    editor_handle_result(editor_open_with_password_internal(path, password), error_code)
}

/// Open a password-protected PDF from memory for editing.
#[unsafe(no_mangle)]
pub extern "C" fn pdfoxide_editor_open_from_bytes_with_password(
    data: *const u8,
    len: usize,
    password: *const c_char,
    error_code: *mut i32,
) -> *mut std::ffi::c_void {
    editor_handle_result(
        editor_open_from_bytes_with_password_internal(data, len, password),
        error_code,
    )
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

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    /// Build a one-page A4 document holding a text run of `size` points and a
    /// three-row streaming table, mirroring the fuzz target's op sequence.
    fn build_with_streaming_table(size: f32, repeat_header: i32) -> Result<Vec<u8>, String> {
        unsafe {
            let state = pdf_builder_create();
            assert!(!state.is_null());
            assert_eq!(pdf_builder_add_page(state, 0, 595.0, 842.0), 0);

            let text = CString::new("#").unwrap();
            assert_eq!(
                pdf_builder_add_text(state, 0, text.as_ptr(), std::ptr::null(), size),
                0
            );

            let header0 = CString::new("a").unwrap();
            let header1 = CString::new("b").unwrap();
            let headers = [header0.as_ptr(), header1.as_ptr()];
            let widths = [50.0f32, 100.0];
            let aligns = [0i32, 2];
            assert_eq!(
                pdf_builder_streaming_table_begin(
                    state,
                    0,
                    headers.as_ptr(),
                    widths.as_ptr(),
                    aligns.as_ptr(),
                    2,
                    repeat_header,
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
                ),
                0
            );

            let cell0 = CString::new("x").unwrap();
            let cell1 = CString::new("y").unwrap();
            let cells = [cell0.as_ptr(), cell1.as_ptr()];
            let spans = [1i32, 1];
            assert_eq!(
                pdf_builder_streaming_table_add_row(state, cells.as_ptr(), spans.as_ptr(), 2),
                0
            );
            assert_eq!(pdf_builder_streaming_table_end(state), 0);

            let mut data: *mut u8 = std::ptr::null_mut();
            let mut len = 0i32;
            let rc = pdf_builder_build(state, &mut data, &mut len);
            let result = if rc == 0 {
                let bytes = std::slice::from_raw_parts(data, len as usize).to_vec();
                pdf_builder_bytes_free(data, len);
                Ok(bytes)
            } else {
                Err(c_string_to_string(pdf_last_error_message(), "error").unwrap_or_default())
            };
            pdf_builder_free(state);
            result
        }
    }

    #[test]
    fn streaming_table_with_an_unrenderable_row_is_rejected() {
        // The exact font size the fuzz target found: it used to make
        // pdf_oxide's pagination loop allocate until the process died.
        let error = build_with_streaming_table(2.37e14, 1)
            .expect_err("an unrenderable table must be rejected, not built");
        assert!(error.contains("cannot fit"), "unexpected error: {error}");
    }

    #[test]
    fn streaming_table_that_fits_is_still_built() {
        let bytes = build_with_streaming_table(12.0, 1).expect("a normal table must still build");
        assert!(bytes.starts_with(b"%PDF"));
    }
}
