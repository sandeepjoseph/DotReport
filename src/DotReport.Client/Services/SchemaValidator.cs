using System.Text.Json;
using System.Text.Json.Nodes;
using DotReport.Client.Models;

namespace DotReport.Client.Services;

/// <summary>
/// Schema Guard — validates AI output against the expected field extraction schema.
/// If Phi-4 returns malformed JSON, the Proxy rejects it and asks Qwen to "Repair" it.
/// From Test Report recommendation: "Schema Guard must remain strict."
/// </summary>
public sealed class SchemaValidator
{
    // Expected keys in every extracted field object
    private static readonly HashSet<string> RequiredFieldKeys =
        new(StringComparer.OrdinalIgnoreCase) { "label", "value", "confidence" };

    public ValidationResult ValidateFieldExtractionOutput(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return ValidationResult.Fail("Model returned empty output.");

        // Locate the first JSON array in the output
        var jsonStart = rawText.IndexOf('[');
        var jsonEnd   = rawText.LastIndexOf(']');

        if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
            return ValidationResult.Fail("No JSON array found in model output.");

        var jsonSlice = rawText[jsonStart..(jsonEnd + 1)];

        try
        {
            var node = JsonNode.Parse(jsonSlice);
            if (node is not JsonArray arr)
                return ValidationResult.Fail("Root element is not a JSON array.");

            var fields = new List<ExtractedField>();
            var hallucinations = new List<string>();

            foreach (var (item, idx) in arr.Select((x, i) => (x, i)))
            {
                if (item is not JsonObject obj)
                {
                    hallucinations.Add($"[{idx}] not an object");
                    continue;
                }

                // Verify required keys are present
                var missing = RequiredFieldKeys
                    .Where(k => !obj.ContainsKey(k))
                    .ToList();
                if (missing.Count > 0)
                {
                    hallucinations.Add($"[{idx}] missing keys: {string.Join(", ", missing)}");
                    continue;
                }

                var conf = obj["confidence"]?.GetValue<float>() ?? -1f;
                if (conf is < 0f or > 1f)
                {
                    hallucinations.Add($"[{idx}] confidence out of range: {conf}");
                    continue;
                }

                fields.Add(new ExtractedField
                {
                    Label      = obj["label"]!.GetValue<string>(),
                    Value      = obj["value"]!.GetValue<string>(),
                    Confidence = conf
                });
            }

            if (hallucinations.Count > 0)
                return ValidationResult.Partial(fields, hallucinations);

            return ValidationResult.Ok(fields);
        }
        catch (JsonException ex)
        {
            return ValidationResult.Fail($"JSON parse error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds a "Repair" prompt sent to the Backup model when Primary output is invalid.
    /// </summary>
    public static string BuildRepairPrompt(string brokenOutput, IReadOnlyList<string> issues) =>
        $$"""
        The following AI output has schema violations. Repair it into valid JSON.

        VIOLATIONS:
        {{string.Join("\n", issues.Select(i => $"  - {i}"))}}

        BROKEN OUTPUT:
        {{brokenOutput}}

        OUTPUT FORMAT (strict):
        [
          { "label": "FIELD_NAME", "value": "extracted value", "confidence": 0.95 },
          ...
        ]

        Return ONLY the JSON array. No explanation.
        """;
}

public sealed class ValidationResult
{
    public bool IsValid { get; private init; }
    public bool IsPartial { get; private init; }
    public IReadOnlyList<ExtractedField> Fields { get; private init; } = [];
    public IReadOnlyList<string> Violations { get; private init; } = [];
    public string? ErrorMessage { get; private init; }

    public static ValidationResult Ok(IReadOnlyList<ExtractedField> fields) =>
        new() { IsValid = true, Fields = fields };

    public static ValidationResult Partial(
        IReadOnlyList<ExtractedField> fields, IReadOnlyList<string> violations) =>
        new() { IsValid = false, IsPartial = true, Fields = fields, Violations = violations };

    public static ValidationResult Fail(string error) =>
        new() { IsValid = false, ErrorMessage = error };
}
