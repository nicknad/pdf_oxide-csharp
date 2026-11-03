# E. Stateful builder / API-sequence scenarios

| # | Scenario | How to test | Why |
| --- | --- | --- | --- |
| E1 | Random operation sequences | Property test: generate arbitrary op lists (page add, text, table, image, build, save, free) and assert success or a graceful error | Catches ordering bugs and panics from accumulated state |
| E2 | Invalid page indices | Negative, `== count`, `> count`, and content before `add_page` | Every op calls `page_mut` (`rust/src/lib.rs:177`); it must return `-1` |
| E3 | Save/build twice, then add content | `Save` -> `ToBytes` -> `AddPage` -> `Save` | The builder is documented as remaining usable; verify state is not consumed |
| E4 | Empty builder | Build/save with zero pages | Must produce a valid PDF or a clean error, never a malformed file |
| E5 | Failed op followed by recovery | Trigger an error, then issue a valid op and build | The thread-local error must not poison the builder; state stays consistent |
| E6 | Free-then-use and double-free sequences | Interleave `free` with ops inside a child process | Validates the lifetime contract and detects use-after-free |
| E7 | Partial write and disk full | Save to a read-only directory, `/dev/full`, a quota-limited file system, and kill the process mid-write | Ensures no half-written PDF is mistaken for success and no panic escapes |
| E8 | Output path attacks | `..\`, UNC paths, symlinks, FIFOs, directory-as-file, very long paths | Tests path handling and TOCTOU behavior |
| E9 | Large builder state | 100k pages / 10M ops while monitoring memory | Bounds checks and OOM behavior |
| E10 | Image bytes fuzz | Feed random and truncated PNG/JPEG data to `add_image_bytes` | Exercises encoder/decoder error paths; must return `-1` |
| E11 | Extended builder sequences | Random sequences mixing forms, links, annotations, watermarks, barcodes, graphics, font registration, and tagged-PDF calls | The new op families share the same mutable builder state |
| E12 | Editor operation sequences | Random delete/move/rotate/crop/merge/extract/redact sequences, including invalid indices and disposed handles | Editor operations mutate a single upstream handle and can leave it inconsistent |
| E13 | Streaming table sequences | Random begin/add-row/end/abandon sequences, interleaved with page add and save/build | The open-table state is exclusive and poisons save/build until finished (`rust/src/lib.rs:456-463`) |
