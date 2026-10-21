# PLAN.Docs.SoftwareSet.Review — measured review of the software document set

Scope: TODO-53 turn 1; review of [the reference plan](PLAN.Docs.SoftwareSet.md), source inspection and temporary
layout experiments only. No implementation or source-document corrections.

Status: reviewed 2026-10-20 at `0787f34` on `todo53-software-set`; planner decisions required before implementation.

## Findings and evidence

### F1. The document map is sound; its size estimates and STR classification need correction

All named sources exist. The four workspace annexes are Acquisition, Imaging, Detector and Waveform; History is
separate. The latest committed manual edition is `2026-10-17-1` (three editions exist). Measured UTF-8 file sizes
and Markdown structure, including generated tables:

| Source | Bytes | Lines | Tables / body rows | Fenced blocks |
|---|---:|---:|---:|---|
| `VV.Studio.SRS.md` | 79,475 | 407 | 23 / 196 | 0 |
| `VV.Studio.SDS.md` | 64,094 | 649 | 13 / 128 | 3 Mermaid |
| `VV.Studio.md` | 83,870 | 647 | 14 / 315 | 1 bash |
| `VV.Studio.Acquisition.md` | 20,618 | 217 | 4 / 28 | 1 text |
| `VV.Studio.Imaging.md` | 15,375 | 189 | 4 / 18 | 1 PowerShell |
| `VV.Studio.Detector.md` | 7,587 | 95 | 1 / 6 | 1 PowerShell |
| `VV.Studio.Waveform.md` | 21,551 | 196 | 5 / 33 | 1 PowerShell |
| `VV.Studio.History.md` | 45,442 | 485 | 6 / 56 | 1 PowerShell |
| `VV.Tests.Results.md` | 31,691 | 514 | 6 / 383 | 0 |
| `VV.Tests.md` | 776,120 | 4,777 | 1 / 239 | 239 JSON metadata blocks |
| Latest `MANUAL.Studio.md` | 25,263 | 351 | 0 | 0 |

The catalog has **239 units**, split by the plan's assembly/family rule into **101 Studio** (34 Core, 21 Services,
7 Render, 39 UI) and **138 other** (90 engine, 29 cocotb configurations, 12 unittest classes, 7 checkers), rather
than approximately 86 / 120. The current catalog is byte-identical to `samples/testing/records/M3/catalog.md`, so
an exact M3 join is possible today. Pin the archived catalog for this STR to keep that join true after new tests
are added.

Part B is not exclusively engine verification: `ManualRecordsTests`, `StageZeroIsolationTests` and the manual
replay checker verify documentation or desktop tooling. Preserve the author-approved scope, but call that part
“Simulation engine and supporting verification tools” and retain each family's actual role.

### F2. STR must identify its historical subject and retain the failed result

`VV.Tests.Results.md:8–16` identifies **1,534 passed, 1 failed, 53 not executed**, formal collection provenance and
subject `161e74e770312aef37f16fa43ec396d76f1d0285`. This is not the review HEAD. “Formal” describes the repository's
collection provenance; it does not mean regulatory approval or successful verification.

The assembly table gives Studio **630 passed / 1 failed / 26 not executed**, and the remaining families
**904 / 0 / 27**. These sums reconcile to the report. `VV.Studio.History.md`'s final M3 section says that the
Compare timeout remains the selected result despite 20 passing repeats; `VV.Studio.md` records it as AN-18.
Neither the repeats nor earlier successful desktop records may replace or be added to the selected outcomes.
M3's known profile gaps and not-executed reasons must survive assembly, including absent RTL C# vectors and the
render/numerical opt-ins. History contains multiple subjects and dates, not one cumulative execution run.

The set therefore has three distinct identities: documentation source commit, M3 test subject, and the manual's
deployment fingerprint (`2e7e30ec32af…`). A single cover “source commit” cannot stand for all three.

### F3. Headless Edge supports the proposed print furniture on this host

Measured Edge **155.0.4283.62**, with `--headless=new`, `--no-pdf-header-footer`, an isolated temporary profile,
background networking/component updates/sync/extensions disabled, and `Start-Process -WindowStyle Hidden`.
All experiment files and profiles were under `%TEMP%/gcam-todo53/`. No Word or Studio process was launched.

`print-probe.html` used A4, 20 mm margins, `@top-center`, `@bottom-center`, `counter(page)`, `counter(pages)`,
`thead { display: table-header-group; }`, `tr { break-inside: avoid; }` and `break-before: page`. It contained
100 numbered table rows and a final forced-break section. The resulting **123,590-byte, five-page PDF** was
read through its Flate streams and ToUnicode maps using a temporary standard-library probe:

| PDF pages | Running header | Footer | Table header | Forced-break section |
|---|---|---|---|---|
| 1–4 | Present once per page | Correct `page 1 of 5` through `page 4 of 5` | Present once per page | Absent |
| 5 | Present once | `page 5 of 5` | Absent | Present |

The launched process exited with code 0 in about one second. Five 250 ms process samples all reported a zero
main-window handle. Together with headless mode and hidden startup this supports a windowless launch; it is
process observation, not an exhaustive enumeration of every child window. No focus or window automation was used.
This verifies print furniture and pagination on this Edge version, not final table geometry or all font glyphs.

### F4. Reuse helpers, but keep the manual's parser/writers unchanged

`manual_formats.py:163–215` deliberately refuses tables and fences. Both PDF `Layout.place` and `docx`
dispatch their existing block types and treat the remaining kind as a figure; adding parser tokens alone does
not add rendering. `test_manual_records.py:463–486` explicitly asserts refusal of tables and fences.

A temporary in-memory dispatch experiment delegated legacy input to the original parser and supplied synthetic
table/code tokens only for two probe strings. Legacy fixture PDF/DOCX bytes stayed identical; new tokens failed
in both writers (table: `TypeError`, code: `KeyError`), confirming that renderer changes are necessary as well.
An opt-in extension is technically possible, but a global parser extension changes an existing tested contract.

A second experiment regenerated PDF/DOCX **in memory** from each committed edition's existing Markdown,
figures and metadata through the unchanged legacy branch:

| Edition | PDF bytes match committed file | DOCX bytes match committed file | PDF pages |
|---|---|---|---:|
| `2026-10-15-1` | Yes | Yes | 18 |
| `2026-10-16-1` | Yes | Yes | 25 |
| `2026-10-17-1` | Yes | Yes | 25 |

This took 0.73 s and wrote no editions. It verifies the format writers against existing figures, **not** the
full `manual_records.py check --all` pipeline, which also re-derives figures and assembles retained records.
Sharing `esc`, `zip_bytes` and, if useful, `docx_runs` is sufficient; the new set needs its own block model,
styles, table/code rendering and OOXML parts. Standard-library `zipfile` is also adequate when deterministic
compressed bytes are not a requirement; the manual's fixed-Huffman compressor is optional reuse.

The font premise is correct: the committed Regular subset has 105 glyphs, and the inspected non-manual sources
contain **41 additional non-whitespace characters**. They include arrows, Greek letters, superscripts,
mathematical operators and U+0302 combining circumflex; µ and μ are distinct. The font folder contains glyph
JSON, extractor and licence, not the original font files. Edge avoids extending the hand-written PDF font/layout
engine. Text extraction alone does not prove that a fallback glyph is visibly correct.

### F5. The actual Markdown requires explicit policies beyond tables and code

- Tables have up to seven columns (results run table); some individual rows are very long: 1,998 characters in
  `VV.Studio.md:245` and 1,588 in `VV.Studio.SRS.md:369`. Use per-table column widths and wrapping. Keeping every
  row together can create large blank areas; permit splitting for a row taller than the available page body.
- `VV.Studio.SRS.md:112` contains an escaped pipe inside an inline-code regex. A naive `split('|')` corrupts it.
  Preserve inline code, backslashes and literal comparison operators when splitting cells.
- The three SDS Mermaid blocks (`:29`, `:175`, `:205`) are architecture/state/gesture diagrams. Printing their
  source code is possible but is a deliberate visual compromise, not rendered diagrams. No Mermaid renderer
  was found among the existing standard-library manual writers. Decide explicitly whether a labelled code
  listing is acceptable; diagram rendering would require additional work or a separately approved tool.
- Generated HTML comments in VVR/catalog should be omitted. Escape literal `<message>`, `<reason>`, `<counts>`,
  `<see cref="…"/>` and `<skipped />`; otherwise an HTML renderer can silently discard meaningful source text.
  Treat a fence's contents as literal text, including Mermaid's `<br/>`.
- The non-manual documents use flat unordered lists and wrapped continuations. No ordered-list items,
  footnotes, Markdown images, reference-style link definitions or autolinks were found. The manual alone has
  nested lists, 29 HTML image/caption pairs, and notice blockquotes; it remains on its existing writer.
- Heading checks must parse fences first: the PowerShell block at `VV.Studio.Waveform.md:51` contains
  `# Runner-reported…`, which is a code comment, not a document heading. Comparing raw heading regexes is wrong.

### F6. “Every document has a demo cover” conflicts with reusing the manual unchanged

The existing manual has its own title, edition, provenance, internal contents links and footer. It lacks the
plan's demonstration banner, GCAM-SW-UM control table and `page n of N` furniture. Its files are immutable editions
under the repository rules. Adopt an explicit exception: index it as an unchanged companion document, with the
demonstration designation in the set index. Do not silently prepend a cover, re-render it or change its revision.
Reference its existing PDF/DOCX rather than duplicate them; record their hashes, original edition and page count.

### F7. Link and section rewriting needs a source-aware map

The inspected Markdown links are local: VV/Design pages, code, retained evidence assets, and manual self-anchors.
No missing local paths or unresolved anchors into the inspected Markdown files were found by the source scan.
History is particularly significant: VVR contains **31 links to it**, so map History into STR, not to plain
unavailable text. Map all four VVR annex source paths into VVR, and both Results/catalog paths into their STR
locations. Source targets outside the set include product VV pages, Design documents, code and evidence PNG/JSON/CSV;
retain their descriptive labels/IDs as plain text, without inventing access to the omitted evidence.

Many target headings are unnumbered, including “Current test inventory”, annex sections and all dated History
sections. Therefore a blanket “GCAM-SW-xxx §n” has no existing `n` to substitute. Combining documents also creates
repeated heading names. Resolve `(source path, source anchor)` first, then assign unique destination bookmarks
and a displayed section label. File-only links should name the document, with no fabricated section number.

### F8. The checks need a feasible text oracle and a Word pagination policy

The repository's manual PDF extractor test is specific to its own Type 3 output; Edge emits different font maps.
The temporary probe could extract this Edge version's text using standard-library code, but it is not a general
PDF reader. Production PDF text checking needs either an explicitly limited, tested Edge-output reader or an
agreed reduction to HTML/DOCX content checks plus PDF structural/visual checks. There is no standard-library
general PDF text extractor to call automatically.

Headings alone are too weak: `VV.Tests.Results.md` has only its title as a heading, so almost its entire body could
be missing while that check passed. Check normalized body/cell/code text as well as headings against the selected
assembly, with deliberate omissions listed. Search actual PDF actions/annotations and OOXML relationship targets
for outgoing links; namespace URLs in XML are not hyperlinks. Hash outputs as well as all source/helper inputs.

An OOXML TOC field alone does not provide a populated contents page. Without a Word layout/update step, do not
claim verified Word page counts or identical PDF/Word pagination. A static, linked heading contents list without
page numbers supplies the same demo value with less machinery. PAGE/NUMPAGES can remain Word fields; record PDF
page counts explicitly as PDF counts. These are implementation choices, not measurements of Word in this turn.

### F9. Rough pagination is substantially larger than a small document pack

Temporary layouts used A4/20 mm margins, Arial 10 pt at 1.25 line height, 9 pt fixed-width tables with 4 pt padding,
8 pt wrapped code, repeated table headers and one scratch cover each. All source text was retained for SRS/SDS/VVR;
Mermaid remained code. STR was measured as Results + full History, with a separate appendix retaining every
catalog unit's purpose, oracle and tolerance verbatim while omitting JSON, source and gate paragraphs.

| Document / component | Measured scratch PDF pages | Planning allowance |
|---|---:|---|
| SRS | 33 | About 30–40 |
| SDS | 23 | About 20–30 |
| VVR including four annexes | 72 | About 65–80 |
| STR Results + History | 20 | Part of total below |
| STR catalog narrative appendix | 51 | Combined STR about 70–80 |
| Existing manual | 25 (retained record and writer replay agree) | 25 unchanged |

These are layout measurements, not deliverables or finished pagination. They exclude final contents/control
pages and tailored column widths. Combining the two STR probes removes one duplicate cover; splitting A/B adds
some headings. Equal-width tables inflate long identifier columns, particularly the VVR trace matrix. The
appendix contains 21,425 words even after the stated omissions: “per-class summary” needs an explicit selection
rule if a shorter STR is wanted. Do not silently truncate oracle/tolerance qualifications to hit a page target.

### F10. Rendering preserves existing source inconsistencies

For example, SRS `:25` says “Nothing is written to disk”, while SR-SEC-01 (`:206`) and SR-EXPORT-01…05 (`:384`
onward) permit exports. The VVR and annexes also retain historical failures and pending checks beside later dated
evidence. Formatting does not reconcile these statements. Keep this task content-preserving; the planner should
either schedule source corrections separately before freezing the edition or state that the set reproduces an
existing documentation baseline, including its recorded inconsistencies. No source corrections were made here.

## Proposals for the revised plan

1. **Keep the five-document scope and Edge/OOXML method.** Generate four documents and one Markdown index; reference
   the original UM as an expressly exempt companion. Identify the set as a demonstration of records, with the
   wider STR scope and three provenance identities visible. Use first/last source commits as “source history
   endpoints”, not an invented controlled revision/approval history; omit author names and local paths.
2. **Use a separate software-set parser and writers.** Reuse only stable helpers from the manual module; leave
   its rejection contract, output bytes and existing tests intact. Define the small supported Markdown dialect
   from F5 and fail on unsupported constructs. Use one parsed representation for HTML, OOXML and expected text.
3. **Assemble STR deterministically.** Part A: selected Studio results, failure/AN-18 reference, profile gaps and
   a separately labelled dated History annex. Part B: engine, RTL and supporting Python/checker results. Give each
   part its catalog appendix, joining exact unit IDs to M3 outcome counts. Preserve failure/not-executed details,
   gates and stated oracle limitations. Do not add History counts to M3 counts. Pin the M3 archive inputs as well
   as the human-readable report. Use full purpose/oracle/tolerance text initially; a shorter prose summary needs
   planner review because its selection can change meaning.
4. **Resolve links before rendering.** Build the complete source/anchor ownership map, use source-qualified
   bookmarks, and print document ID plus destination heading/annex label. Keep within-document anchor links live;
   cross-document references can be plain ID/section text. Strip only the hyperlink for omitted sources, preserving
   the label and identifiers. Test History links, file-only links, duplicate headings and annex destinations.
5. **Prefer static contents and proportionate checks.** Use the same populated linked contents in both formats,
   without page numbers; retain Word PAGE/NUMPAGES fields. Adopt either the restricted Edge text-reader route or
   the explicitly reduced PDF check in F8. Add block/cell text coverage, totals reconciliation, complete unit
   membership, input and output hashes, archive-subject identity, OOXML package/relationship checks and privacy
   checks. Cover metadata must not imply all tests passed. No byte-for-byte Edge replay requirement is needed.
6. **Make visual review selective but useful.** Review the cover, longest/widest table, a table continuation,
   the three SDS diagram listings (or agreed diagrams), code wrapping and mathematical/combining glyphs. Two
   arbitrary pages per document are insufficient to cover these risks; select pages by those features.
7. **Constrain later execution.** Record Edge version and print settings; isolate profiles/crash output in an
   explicit scratch directory and disable default browser headers/footers. Revise “never writes outside --out”
   to distinguish edition outputs from declared browser scratch files. Do not put browser profiles in a committed
   edition. Add new unittest classes under `samples/evidence/tests/test_*.py`, where `test_records.py:279–285`
   actually discovers them; add reviewed catalog entries and refresh discovery through its owner script. After
   implementation, run focused parser/writer checks and the full manual `check --all`; neither build/test
   collection nor fresh desktop evidence is needed to format this historical set.

## Review execution record

Read-only repository inspection began with a clean worktree at `0787f34`. Temporary scripts/artifacts are in
`%TEMP%/gcam-todo53/`: `inventory.py` / `inventory.json`, `make_probe.py` / `print-probe.*`,
`edge-window-observation.json`, `pdf_probe.py`, `layout_estimate.py` / the five layout specimens,
`compatibility.py`, and `manual_bytes.py`. Scripts ran with `python -B`; Edge launches were hidden and headless.
The PDF probe decodes the measured output's streams/font maps; it is intentionally scratch code, not a production
extractor. No installation, network transfer, Word automation, desktop test, build, numerical run, deletion,
process termination or git state change was performed. The full manual record replay was not run. Only this
review file was added to the repository; no implementation or generated deliverable was added.

No approval-requiring command is needed for the proposed method; there are no approval requests in this turn.
