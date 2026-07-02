# gallery.ps1
#
# PURPOSE:
#   Regenerates the committed rendering gallery under docs/gallery/. The gallery
#   showcase lives in the DemaConsulting.Rendering.Gallery test project; its facts
#   render curated diagrams to files and assert the results are valid images.
#
#   On a normal build (build.ps1 / CI) those facts write to a throwaway directory
#   and simply assert. This script points the RENDERING_GALLERY_DIR environment
#   variable at the committed docs/gallery/ folder and runs ONLY the gallery
#   project, so the same facts (re)write the committed SVG/PNG images and the
#   browsable docs/gallery/gallery.md index.
#
# USAGE:
#   pwsh ./gallery.ps1

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$galleryDir = Join-Path $repoRoot 'docs/gallery'
$galleryProject = Join-Path $repoRoot 'test/DemaConsulting.Rendering.Gallery/DemaConsulting.Rendering.Gallery.csproj'

New-Item -ItemType Directory -Force -Path $galleryDir | Out-Null

Write-Host "Regenerating gallery into $galleryDir ..."
$env:RENDERING_GALLERY_DIR = $galleryDir
try {
    # Generate from a single target framework: the gallery output is deterministic across TFMs, and
    # running all frameworks in parallel would race on the shared docs/gallery output files.
    dotnet test $galleryProject --configuration Release --framework net8.0
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Remove-Item Env:\RENDERING_GALLERY_DIR -ErrorAction SilentlyContinue
}

Write-Host "Gallery regenerated successfully!"
