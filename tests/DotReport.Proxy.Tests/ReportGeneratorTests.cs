using DotReport.Client.Models;
using DotReport.Client.Services;
using FluentAssertions;

namespace DotReport.Proxy.Tests;

/// <summary>
/// Tests for QuestPDF ReportGenerator.
/// UAC 7.5: Sovereign output — no external API calls, all generation in-process.
/// </summary>
public sealed class ReportGeneratorTests
{
    private readonly ReportGenerator _sut = new();

    private static ReportDocument MakeDocument(int fieldCount = 3) =>
        new()
        {
            DocumentId     = "TEST-001",
            SourceFileName = "invoice_2026.pdf",
            SourceContent  = "Sample content",
            ProcessedBy    = ModelRole.Primary,
            ExtractedFields = Enumerable.Range(1, fieldCount)
                .Select(i => new ExtractedField
                {
                    Label      = $"Field {i}",
                    Value      = $"Value {i}",
                    Confidence = 0.80f + (i * 0.02f)
                }).ToList(),
            Sections = new Dictionary<ReportSection, string>
            {
                [ReportSection.Summary]    = "Document processed successfully.",
                [ReportSection.Analysis]   = "No anomalies detected."
            }
        };

    [Fact]
    public void Generate_ValidDocument_ReturnsPdfBytes()
    {
        var doc = MakeDocument();

        var pdf = _sut.Generate(doc);

        pdf.Should().NotBeNull();
        pdf.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_PdfStartsWithPdfMagicBytes()
    {
        var pdf = _sut.Generate(MakeDocument());

        // PDF magic bytes: %PDF
        pdf[0].Should().Be(0x25); // %
        pdf[1].Should().Be(0x50); // P
        pdf[2].Should().Be(0x44); // D
        pdf[3].Should().Be(0x46); // F
    }

    [Fact]
    public void Generate_DocumentWithNoFields_DoesNotThrow()
    {
        var doc = MakeDocument(fieldCount: 0);

        var act = () => _sut.Generate(doc);

        act.Should().NotThrow();
    }

    [Fact]
    public void Generate_DocumentWithMergedFlag_ProducesPdf()
    {
        var doc = MakeDocument();
        var merged = doc with { WasMergedOutput = true, ProcessedBy = ModelRole.Backup };

        var pdf = _sut.Generate(merged);

        pdf.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_LargeFieldSet_ProducesValidPdf()
    {
        var doc = MakeDocument(fieldCount: 50);

        var pdf = _sut.Generate(doc);

        pdf.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Generate_AllReportSections_ProducesValidPdf()
    {
        var doc = MakeDocument();
        doc.Sections[ReportSection.DataExtraction]  = "Raw fields extracted.";
        doc.Sections[ReportSection.Recommendations] = "Review field 3 manually.";

        var pdf = _sut.Generate(doc);

        pdf.Length.Should().BeGreaterThan(0);
    }
}
