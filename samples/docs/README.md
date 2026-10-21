# Software document demonstration set

`software_set.py` generates four English documents (SRS, SDS, VVR and STR) as HTML,
PDF and Word, plus the HTML coverage inputs, a Markdown index and generation record.
The existing manual is an unchanged indexed companion. This is a demonstration,
not a submission, approval or claim that verification passed.

```powershell
python -B samples/docs/software_set.py generate --edition-id 2026-10-20-1 --out <empty-edition-folder>
python -B samples/docs/software_set.py check --edition <edition-folder>
python -B samples/docs/software_set.py review --edition <edition-folder> --out <temporary-review-folder>
```

`--edition-id YYYY-MM-DD-n` is required; its date is the default document date
(`--date YYYY-MM-DD` can override it). Generation refuses uncommitted changes
unless `--draft` is supplied. Draft covers and running headers say DRAFT; the
source-tree line states either "uncommitted changes present" or "clean at" the
documentation commit. This source status is captured before writing output.
A generator-owned output folder can be regenerated; an unrelated nonempty folder is refused. Browser jobs
run sequentially, hidden, with `--headless=new` and isolated profiles under the
temporary `gcam-todo53` folder. Their scratch files are retained for inspection and
are never edition artifacts. A timed-out process is left running and reported,
never killed automatically. No Word automation, application launch or download is
needed. Python uses the standard library; Edge must already be installed.

The source dialect is deliberately small: headings of levels 1–4, paragraphs,
flat `-` lists with wrapped continuations, pipe tables (alignment markers, escaped
pipes, missing trailing cells padded empty as in GFM), triple-backtick fences,
inline code, bold/italic, and inline links. HTML comments are dropped outside
literal code. Angle-bracket placeholders and HTML-looking text are escaped, not
executed. Top-level HTML elements, images, blockquotes, nested/ordered/task lists,
footnotes, reference links/definitions, setext headings, strikethrough, tilde
fences, malformed delimiters and extra table cells are refused. Mathematical
literal asterisks such as `H*(10)`, `xoshiro256**` and `2 * x` are preserved.

Mermaid fences are rendered using only the hash-pinned local vendor file.
HTML/PDF use the resulting SVG; DOCX embeds a PNG drawn from the same SVG by Edge.
The source listing is omitted from the formatted document. Each rendered diagram
carries its source hash, checked against the repository's Mermaid block. Diagrams
fill the text width while retaining their aspect ratio, with a 120 mm height cap.
Render failures stop generation. Links are resolved by source path
and heading anchor. Same-document links use unique bookmarks; cross-document
references append document ID and heading text. Omitted references retain labels
as plain text. The source manual and its writer are never modified.

STR pins the M3 archive and reuses the existing test-record validator, including
runner artifact hashes and exact selected cases. Its 101 Studio units and 138
engine/support units retain full purpose, oracle, tolerance and gate statements.
The Compare failure, unexecuted reasons and profile gaps remain visible. History
is a separate dated annex and never adds to M3 totals. Its 11 machine worktree
paths are replaced by `[local path omitted]` in the generated model, with counts
recorded; source Markdown remains unchanged. Today's catalog may have more tests
than the historical STR; documentation-tool tests do not become M3 evidence.

`check` reassembles the model, verifies all input/output hashes and membership,
compares every displayed paragraph/list item/table cell/code block in order against HTML and DOCX,
validates OOXML parts/relationships/bookmarks/images and page fields, and checks
PDF structure, page count, cover text and privacy. The limited PDF reader accepts
the measured Edge/Skia conventional xref output with direct-length Flate streams,
Type 0/3 ToUnicode mappings and UTF-16 ActualText; it refuses encryption, object
streams, unsupported filters and outgoing actions. It is not a general PDF reader.
All-empty source table headers are omitted in both formats; the source columns
are retained. STR result tables are grouped by assembly/family with short class
names and fixed 22 mm numeric columns whose headers do not wrap inside words.
PDF bytes and Word pagination need not be reproducible. Static linked contents
are populated without Word; PAGE/NUMPAGES fields update in Word when laid out.

`review` produces labelled HTML feature screenshots, not rasterised PDF pages.
It includes covers, wide/long tables, subsequent rows, diagrams, code and glyph
samples. The planner must inspect final PDFs for actual page breaks and repeated
headers. The record identifies the documentation commit, exact uncommitted input
hashes, selected M3 subject and companion manual deployment separately. Public
outputs contain no local paths or process-account names.

Unit tests live in `samples/evidence/tests/test_software_set.py` and are registered
in `docs/VV.Tests.md`. `vendor/mermaid-LICENSE.txt` accompanies Mermaid 11.4.1;
the vendored script is never fetched by the generator.

Commit exactly the fourteen edition files: `GCAM-SW-{SRS,SDS,VVR,STR}.{pdf,docx,html}`,
`INDEX.md` and `generation-record.json`. The four HTML files are required by
`check` for independent segment coverage and embedded SVG checks. Diagrams are
embedded (SVG in HTML/PDF and PNG in DOCX); no separate figures, parsed model,
ownership marker or browser scratch is needed. `check` reconstructs the model
from the hash-pinned repository inputs. Commit the code first, then generate from
the clean tree without `--draft`; generation itself does not commit anything.
