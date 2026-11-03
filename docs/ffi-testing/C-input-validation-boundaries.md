# C. Input validation and boundary scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| C1 | NULL path/text/array pointers | Call raw `NativeMethods` with `null` for every pointer parameter | Confirms `c_string_to_string` / explicit null guards return `-1` + error, never AV |
| C2 | Empty string vs NULL | Pass `""` and `null` separately to Markdown/text/path APIs | An empty path is a valid `CString` but an invalid file; behavior must differ from null |
| C3 | Interior NUL truncation | Include `"\0"` in text, path, and filename | C# sends `UTF8(s + "\0")`, so an interior NUL truncates silently; document and test |
| C4 | Invalid UTF-8 bytes | Native driver passes non-UTF-8 `c_char` bytes | `CStr::to_str` must produce a clean error (`rust/src/lib.rs:2131`) |
| C5 | Numeric extremes | `i32::MIN/MAX` page index, `rows`, `cols`, `len`, `count`, `style`; `f32` NaN/±Inf/`-0.0`/subnormal | Targets `validate_rect`, `size.is_finite()`, `PageKind::from_values`, `checked_mul` |
| C6 | Invalid enum codes | Page kind outside 0..4, list style outside 0..2, table style outside 0..2, run style outside 0..3 | Must return `-1`, not panic on `match` fallthrough |
| C7 | Silent coercion of invalid sizes | `pdf_builder_add_text` with `size=NaN/0/-1` | Code silently ignores an invalid size (`rust/src/lib.rs:1578`) instead of erroring; assert the intended policy |
| C8 | `len` disagrees with the buffer | `add_image_bytes` with `len > actual`, `len < actual`, `i32::MAX` | `from_raw_parts` trusts `len` (`rust/src/lib.rs:1973`) and reads out of bounds when mismatched |
| C9 | Array cardinality mismatches | `add_table` with fewer cells than `rows*cols`; `images_to_pdf` with `len >` the real array; NULL elements inside arrays | Native trusts the counts and NULL-dereferences elements; ASAN should flag |
| C10 | Rich paragraph color array too short | `styles=[3]`, `colors` containing only 2 floats | `index*3+2` reads out of bounds (`rust/src/lib.rs:1767`); C# always sends `len*3`, but the ABI is unsafe |
| C11 | Extremely long strings and paths | 1-100 MB content, 32k-character paths, maximum Windows path length | Tests allocation, `CString`, and OS path limits |
| C12 | Unicode and encoding edges | CJK, emoji (4-byte), combining marks, RTL, UTF-16 BOM metadata, odd-length UTF-16 | Exercises `decode_pdf_text` and `pdf_get_string_len` byte-length vs character count |
| C13 | Embedded NUL in extracted PDF text | Craft a PDF whose content stream decodes to `\0` | `CString::new(full_text)?` (`rust/src/lib.rs:378`) turns a successful extraction into an error |
| C14 | Non-finite geometry in JSON | Craft a PDF yielding NaN/Inf bounding boxes or font sizes | `serde_json::to_string` rejects non-finite floats, so the whole extraction returns NULL |
