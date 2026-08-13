using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RelatorioGLPIApp.Documents
{
    public class TechnicianReportPdfDocument : IDocument
    {
        private readonly TechnicianReportPdfModel _model;

        public TechnicianReportPdfDocument(TechnicianReportPdfModel model)
        {
            _model = model;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }

        void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Relatório de Produtividade de Técnicos").Bold().FontSize(20);
                    column.Item().Text($"Gerado em: {DateTime.Now:dd/MM/yyyy}").FontSize(12);
                });
            });
        }

        void ComposeContent(IContainer container)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(20);
                if (_model.TechnicianStats.Any())
                    column.Item().Element(c => ComposeTechnicianSection(c, "Produtividade por Técnico", _model.TechnicianStats));
            });
        }

        void ComposeTechnicianSection(IContainer container, string title, ICollection<TechnicianStat> stats)
        {
            container.Border(1).BorderColor("#CCC").Padding(10).Column(column =>
            {
                column.Item().Text(title).Bold().FontSize(14);

                column.Item().PaddingTop(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(); // Name
                        columns.ConstantColumn(70); // Solved
                        columns.ConstantColumn(70); // Rate
                        columns.ConstantColumn(70); // On Duty
                        columns.ConstantColumn(70); // Avg Time
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Técnico").Bold();
                        header.Cell().AlignCenter().Text("Soluc.").Bold();
                        header.Cell().AlignCenter().Text("Taxa Res.").Bold();
                        header.Cell().AlignCenter().Text("Plantão").Bold();
                        header.Cell().AlignCenter().Text("T. Médio").Bold();
                    });

                    foreach (var stat in stats)
                    {
                        table.Cell().Text(stat.FormattedTechnicianName);
                        table.Cell().AlignCenter().Text(stat.SolvedCount.ToString());
                        table.Cell().AlignCenter().Text(stat.ResolutionRate);
                        table.Cell().AlignCenter().Text(stat.OnDutySolvedCount.ToString());
                        table.Cell().AlignCenter().Text(stat.AverageSolveTime);
                    }
                });
            });
        }
    }
}