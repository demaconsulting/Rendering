## WeasyPrint Integration Design

### Purpose

WeasyPrint (`demaconsulting.weasyprinttool`) converts the HTML documents produced by Pandoc into
archival PDF/A files. It is the final rendering stage of the document-generation pipeline, producing the
release PDF artifacts for the build notes, code quality, code review plan and report, design,
verification, requirements, and user guide collections.

### Features Used

- **HTML-to-PDF conversion** (`dotnet weasyprint {input.html} {output.pdf}`) — renders a Pandoc-produced
  HTML document to PDF.
- **PDF/A variant selection** (`--pdf-variant pdf/a-3u`) — produces an archival PDF/A-3u document
  suitable for compliance retention.

### Integration Pattern

WeasyPrint is installed as a .NET local tool (a DemaConsulting distribution of WeasyPrint) via
`.config/dotnet-tools.json` and invoked as `dotnet weasyprint` from `.github/workflows/build.yaml`, once
per document collection after Pandoc has produced the HTML. Its input is the collection's generated HTML
file; its output is the archival PDF written into `docs/generated/` (for example
`docs/generated/Rendering Build Notes.pdf`). WeasyPrint has no self-validation suite in this pipeline;
its correct operation is verified indirectly by FileAssert assertions on the generated PDFs. WeasyPrint
is not referenced by the delivered Rendering packages.
