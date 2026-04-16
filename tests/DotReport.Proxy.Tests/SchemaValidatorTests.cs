using DotReport.Client.Services;
using FluentAssertions;

namespace DotReport.Proxy.Tests;

/// <summary>
/// Tests for the SchemaValidator (Schema Guard).
/// Validates that hallucinated / malformed AI output is caught and flagged
/// before it reaches the PDF report. Test Report Section 6.
/// </summary>
public sealed class SchemaValidatorTests
{
    private readonly SchemaValidator _sut = new();

    // ── Valid JSON → clean result ─────────────────────────────────────────
    [Fact]
    public void Validate_ValidJsonArray_ReturnsOkWithFields()
    {
        var input = """
            [
              { "label": "Invoice Number", "value": "INV-2026-001", "confidence": 0.97 },
              { "label": "Total Amount",   "value": "$12,400.00",    "confidence": 0.91 }
            ]
            """;

        var result = _sut.ValidateFieldExtractionOutput(input);

        result.IsValid.Should().BeTrue();
        result.Fields.Should().HaveCount(2);
        result.Fields[0].Label.Should().Be("Invoice Number");
        result.Fields[1].Confidence.Should().Be(0.91f);
    }

    // ── Model wraps JSON in prose — should still extract ──────────────────
    [Fact]
    public void Validate_JsonEmbeddedInProse_ExtractsJsonArray()
    {
        var input = """
            Here are the extracted fields:
            [
              { "label": "Date", "value": "2026-01-15", "confidence": 0.88 }
            ]
            That's all.
            """;

        var result = _sut.ValidateFieldExtractionOutput(input);

        result.IsValid.Should().BeTrue();
        result.Fields.Should().HaveCount(1);
    }

    // ── Missing required key → partial result ────────────────────────────
    [Fact]
    public void Validate_MissingConfidenceKey_ReturnsPartialWithViolation()
    {
        var input = """
            [
              { "label": "PO Number", "value": "PO-9876" }
            ]
            """;

        var result = _sut.ValidateFieldExtractionOutput(input);

        result.IsValid.Should().BeFalse();
        result.IsPartial.Should().BeTrue();
        result.Violations.Should().Contain(v => v.Contains("confidence"));
    }

    // ── Confidence out of range → violation ───────────────────────────────
    [Fact]
    public void Validate_ConfidenceOutOfRange_ReturnsViolation()
    {
        var input = """
            [
              { "label": "Vendor", "value": "Acme Corp", "confidence": 1.5 }
            ]
            """;

        var result = _sut.ValidateFieldExtractionOutput(input);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().Contain(v => v.Contains("confidence out of range"));
    }

    // ── Completely broken JSON → fail ──────────────────────────────────────
    [Fact]
    public void Validate_BrokenJson_ReturnsFail()
    {
        var result = _sut.ValidateFieldExtractionOutput("{ this is not json at all }");

        result.IsValid.Should().BeFalse();
        result.IsPartial.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    // ── Empty output → fail ───────────────────────────────────────────────
    [Fact]
    public void Validate_EmptyOutput_ReturnsFail()
    {
        var result = _sut.ValidateFieldExtractionOutput(string.Empty);

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    // ── No JSON array in response → fail ─────────────────────────────────
    [Fact]
    public void Validate_NoJsonArray_ReturnsFail()
    {
        var result = _sut.ValidateFieldExtractionOutput("The document contains vendor data.");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No JSON array");
    }

    // ── Repair prompt is generated correctly ──────────────────────────────
    [Fact]
    public void BuildRepairPrompt_ContainsBrokenOutputAndViolations()
    {
        var broken  = "{ bad json }";
        var issues  = new List<string> { "[0] missing keys: confidence" };

        var prompt = SchemaValidator.BuildRepairPrompt(broken, issues);

        prompt.Should().Contain(broken);
        prompt.Should().Contain("confidence");
        prompt.Should().Contain("VIOLATIONS");
        prompt.Should().Contain("OUTPUT FORMAT");
    }

    // ── Valid array but all items are non-objects ──────────────────────────
    [Fact]
    public void Validate_ArrayOfPrimitives_ReturnsPartialWithViolations()
    {
        var input = """["string1", "string2"]""";

        var result = _sut.ValidateFieldExtractionOutput(input);

        result.IsValid.Should().BeFalse();
        result.Violations.Should().HaveCount(2);
    }
}
