using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace RelatorioGLPIApp.Documents;

public class GeneralReportPdfDocument : IDocument
{
    private readonly GeneralReportPdfModel _model;

    public GeneralReportPdfDocument(GeneralReportPdfModel model)
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
                column.Item().Text("Relatório Geral de Chamados").Bold().FontSize(20);
                column.Item().Text($"Dados referentes ao dia: {System.DateTime.Now:dd/MM/yyyy}").FontSize(12);
            });
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(20).Column(column =>
        {
            column.Spacing(20);
            column.Item().Element(ComposeStatsTable);

            if (_model.MatrizStats.Any())
                column.Item().Element(c => ComposeDepartmentSection(c, "Demandas da Matriz", _model.MatrizPercentage, _model.MatrizStats));

            if (_model.AgenciasStats.Any())
                column.Item().Element(c => ComposeDepartmentSection(c, "Demandas das Agências", _model.AgenciasPercentage, _model.AgenciasStats));

            if (_model.FiliaisStats.Any())
                column.Item().Element(c => ComposeDepartmentSection(c, "Demandas das Filiais", _model.FiliaisPercentage, _model.FiliaisStats));
        });
    }

    void ComposeStatsTable(IContainer container)
    {
        container.Border(1).BorderColor("#CCC").Padding(10).Column(column =>
        {
            column.Item().PaddingBottom(10).Text("Resumo do Período").Bold().FontSize(14);

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(80);
                });

                ComposeStatRow(table, "Chamados Abertos no Período:", _model.TotalTicketsFound.ToString());
                ComposeStatRow(table, "Chamados Solucionados/Fechados:", _model.TotalSolved.ToString());
                ComposeStatRow(table, "Chamados em Expediente Normal:", _model.TotalBusinessHours.ToString());
                ComposeStatRow(table, "Chamados em Horário de Plantão:", _model.TotalOnDuty.ToString());
                ComposeStatRow(table, "Taxa de Resolução:", _model.TaxaResolucaoDia);
                ComposeStatRow(table, "Chamados Pendentes:", _model.TotalPending.ToString());
                ComposeStatRow(table, "Chamados Novos:", _model.TotalNew.ToString());
            });
        });
    }

    void ComposeStatRow(TableDescriptor table, string title, string value)
    {
        table.Cell().Text(title);
        table.Cell().AlignRight().Text(value).Bold();
    }

    void ComposeDepartmentSection(IContainer container, string title, string percentage, ICollection<DepartmentStat> stats)
    {
        container.Border(1).BorderColor("#CCC").Padding(10).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Text(title).Bold().FontSize(14);
                row.ConstantItem(100).AlignRight().Text(percentage).FontSize(12).FontColor("#666");
            });

            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.ConstantColumn(50); });
                table.Header(header => { header.Cell().Text("Setor").Bold(); header.Cell().AlignRight().Text("Chamados").Bold(); });
                foreach (var stat in stats) { table.Cell().Text(stat.DepartmentName); table.Cell().AlignRight().Text(stat.TicketCount).Bold(); }
            });
        });
    }
}