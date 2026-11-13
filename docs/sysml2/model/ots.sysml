package OtsDependencies {
    private import SoftwareItemMetadata::*;

    doc /* Off-the-shelf (OTS) items used by the Rendering libraries: the CanvasNet runtime dependency plus build, test, and compliance tooling. */

    part def BuildMark {
        doc /* OTS: BuildMark tool that generates build notes. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/buildmark.md");
            verificationFiles = ("docs/verification/ots/buildmark.md");
            requirementFiles = ("docs/reqstream/ots/buildmark.yaml");
        }
    }

    part def CanvasNet {
        doc /* OTS: CanvasNet pure managed raster graphics library (surface drawing, text, and PNG/JPEG encoding) linked into the Rendering.CanvasNet package. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/canvasnet.md");
            verificationFiles = ("docs/verification/ots/canvasnet.md");
            requirementFiles = ("docs/reqstream/ots/canvasnet.yaml");
        }
    }

    part def FileAssert {
        doc /* OTS: FileAssert tool that asserts properties of generated files. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/fileassert.md");
            verificationFiles = ("docs/verification/ots/fileassert.md");
            requirementFiles = ("docs/reqstream/ots/fileassert.yaml");
        }
    }

    part def Pandoc {
        doc /* OTS: Pandoc document converter used to compile documentation to HTML. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/pandoc.md");
            verificationFiles = ("docs/verification/ots/pandoc.md");
            requirementFiles = ("docs/reqstream/ots/pandoc.yaml");
        }
    }

    part def ReqStream {
        doc /* OTS: ReqStream requirements trace and enforcement tool. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reqstream.md");
            verificationFiles = ("docs/verification/ots/reqstream.md");
            requirementFiles = ("docs/reqstream/ots/reqstream.yaml");
        }
    }

    part def ReviewMark {
        doc /* OTS: ReviewMark file-review planning and evidence tool. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/reviewmark.md");
            verificationFiles = ("docs/verification/ots/reviewmark.md");
            requirementFiles = ("docs/reqstream/ots/reviewmark.yaml");
        }
    }

    part def SarifMark {
        doc /* OTS: SarifMark tool that converts SARIF analysis results into reports. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sarifmark.md");
            verificationFiles = ("docs/verification/ots/sarifmark.md");
            requirementFiles = ("docs/reqstream/ots/sarifmark.yaml");
        }
    }

    part def SonarMark {
        doc /* OTS: SonarMark tool that reports SonarCloud quality-gate results. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sonarmark.md");
            verificationFiles = ("docs/verification/ots/sonarmark.md");
            requirementFiles = ("docs/reqstream/ots/sonarmark.yaml");
        }
    }

    part def SysML2Tools {
        doc /* OTS: SysML2Tools linter, query engine, and renderer for the architecture model. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/sysml2tools.md");
            verificationFiles = ("docs/verification/ots/sysml2tools.md");
            requirementFiles = ("docs/reqstream/ots/sysml2tools.yaml");
        }
    }

    part def VersionMark {
        doc /* OTS: VersionMark tool that captures tool versions used in a build. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/versionmark.md");
            verificationFiles = ("docs/verification/ots/versionmark.md");
            requirementFiles = ("docs/reqstream/ots/versionmark.yaml");
        }
    }

    part def WeasyPrint {
        doc /* OTS: WeasyPrint HTML-to-PDF renderer for compliance documents. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/weasyprint.md");
            verificationFiles = ("docs/verification/ots/weasyprint.md");
            requirementFiles = ("docs/reqstream/ots/weasyprint.yaml");
        }
    }

    part def XUnit {
        doc /* OTS: xUnit test framework used to run the test suites. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/xunit.md");
            verificationFiles = ("docs/verification/ots/xunit.md");
            requirementFiles = ("docs/reqstream/ots/xunit.yaml");
        }
    }
}
