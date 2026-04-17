using DotReport.Client.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DotReport.Client.Services;

/// <summary>
/// Generates branded PDF reports from processed ReportDocument data using QuestPDF.
/// Maps AI-extracted fields to a structured, professional document. Phase 4 — Integration.
/// </summary>
public sealed class ReportGenerator
{
    static ReportGenerator()
    {
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }
        catch
        {
            // QuestPDF native initializer is not supported in all WASM environments.
            // PDF export will degrade gracefully if called; startup is never blocked.
        }
    }

    public byte[] Generate(ReportDocument doc)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Courier New").FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(content => ComposeContent(content, doc));
                page.Footer().Element(ComposeFooter);
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().BorderBottom(2).BorderColor("#1a1a1a").PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("DOTREPORT / EDGECORE")
                        .FontSize(16).Bold().FontColor("#1a1a1a").LetterSpacing(0.1f);
                    c.Item().Text("NET-FORGE DOCUMENT ANALYSIS SYSTEM")
                        .FontSize(7).FontColor("#666666").LetterSpacing(0.15f);
                });
                row.ConstantItem(80).AlignRight().Column(c =>
                {
                    c.Item().Text($"REV-2026-A").FontSize(7).FontColor("#999999");
                    c.Item().Text(DateTime.UtcNow.ToString("yyyy-MM-dd")).FontSize(7).FontColor("#999999");
                });
            });
            col.Item().Height(8);
        });
    }

    private static void ComposeContent(IContainer container, ReportDocument doc)
    {
        container.Column(col =>
        {
            // Document metadata block
            col.Item().BorderLeft(3).BorderColor("#333333").PaddingLeft(10).PaddingVertical(6).Column(meta =>
            {
                meta.Item().Text($"SOURCE FILE: {doc.SourceFileName}").FontSize(9).Bold();
                meta.Item().Text($"DOCUMENT ID: {doc.DocumentId}").FontSize(8).FontColor("#666666");
                meta.Item().Text($"PROCESSED: {doc.ProcessedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor("#666666");
                meta.Item().Text($"ENGINE: {doc.ProcessedBy}{(doc.WasMergedOutput ? " [MERGED]" : "")}").FontSize(8).FontColor("#666666");
            });

            col.Item().Height(16);

            // Extracted fields table
            if (doc.ExtractedFields.Count > 0)
            {
                col.Item().Text("EXTRACTED FIELDS").FontSize(9).Bold().LetterSpacing(0.1f);
                col.Item().Height(4);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(4);
                        cols.ConstantColumn(50);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background("#1a1a1a").Padding(4)
                            .Text("FIELD").FontSize(8).Bold().FontColor(Colors.White);
                        header.Cell().Background("#1a1a1a").Padding(4)
                            .Text("VALUE").FontSize(8).Bold().FontColor(Colors.White);
                        header.Cell().Background("#1a1a1a").Padding(4)
                            .Text("CONF.").FontSize(8).Bold().FontColor(Colors.White);
                    });

                    foreach (var (field, idx) in doc.ExtractedFields.Select((f, i) => (f, i)))
                    {
                        var bg = idx % 2 == 0 ? "#ffffff" : "#f5f5f5";
                        table.Cell().Background(bg).Padding(4).Text(field.Label.ToUpper()).FontSize(8);
                        table.Cell().Background(bg).Padding(4).Text(field.Value).FontSize(8);
                        table.Cell().Background(bg).Padding(4)
                            .Text($"{field.Confidence:P0}").FontSize(8).FontColor(
                                field.Confidence >= 0.8f ? "#1a7a1a" : "#cc4400");
                    }
                });

                col.Item().Height(16);
            }

            // Report sections
            foreach (var (section, text) in doc.Sections)
            {
                col.Item().Column(s =>
                {
                    s.Item().Text(section.ToString().ToUpper()).FontSize(9).Bold().LetterSpacing(0.08f);
                    s.Item().Height(4);
                    s.Item().BorderLeft(1).BorderColor("#cccccc").PaddingLeft(8)
                        .Text(text).FontSize(9).LineHeight(1.5f);
                    s.Item().Height(12);
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor("#cccccc").PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text("ALL INFERENCE EXECUTED CLIENT-SIDE — NO DATA TRANSMITTED")
                .FontSize(7).FontColor("#999999").LetterSpacing(0.05f);
            row.ConstantItem(60).AlignRight()
                .Text(text =>
                {
                    text.Span("PG ").FontSize(7).FontColor("#999999");
                    text.CurrentPageNumber().FontSize(7).FontColor("#999999");
                    text.Span(" / ").FontSize(7).FontColor("#999999");
                    text.TotalPages().FontSize(7).FontColor("#999999");
                });
        });
    }
}
