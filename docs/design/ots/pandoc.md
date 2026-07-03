## Pandoc Integration Design

### Purpose

Pandoc (`DemaConsulting.PandocTool`) converts the repository's Markdown documentation collections into
HTML as the first stage of the document-generation pipeline. It is used to compile the build notes, code
quality, code review plan and report, design, verification, requirements, and user guide collections
into single HTML documents that WeasyPrint then renders to PDF.

### Features Used

- **Defaults-driven conversion** (`--defaults docs/{collection}/definition.yaml`) — reads each
  collection's Pandoc build definition (input files, resource paths, template, table of contents, and
  section numbering) and concatenates the listed Markdown files into one HTML document.
- **Mermaid filtering** (`--filter node_modules/.bin/mermaid-filter.cmd`) — renders embedded Mermaid
  diagrams during conversion.
- **Metadata injection** (`--metadata version=...`, `--metadata date=...`) and HTML output
  (`--output docs/{collection}/generated/{collection}.html`).

### Integration Pattern

Pandoc is installed as a .NET local tool (a DemaConsulting distribution of Pandoc) via
`.config/dotnet-tools.json` and invoked as `dotnet pandoc` from `.github/workflows/build.yaml`, once per
document collection. Its input is a collection's `definition.yaml` manifest and the checked-in Markdown
source files; its output HTML is written to the collection's `generated/` folder and passed to
WeasyPrint. Because the heading-depth rule keeps every file's top heading aligned to its folder depth,
Pandoc can concatenate the files in `definition.yaml` order into a coherent outline. Pandoc has no
self-validation suite in this pipeline; its correct operation is verified indirectly by FileAssert
assertions on the generated HTML. Pandoc is not referenced by the delivered Rendering packages.
