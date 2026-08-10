using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Documents;

public class RelatorioPdfDocument : IDocument
{
    private readonly RelatorioPdfModel _model;

    public RelatorioPdfDocument(RelatorioPdfModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Margin(50);

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

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text($"Relatório {_model.NomeTecnico} – {_model.DataRelatorio:dd/MM/yyyy}")
                .SemiBold().FontSize(16);

            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private void ComposeContent(IContainer container)
    {
        var categoriasRelatorio = new System.Collections.Generic.Dictionary<string, string>
        {
            { "Suporte Matriz", "1. Suporte Matriz" },
            { "Suporte Filiais", "2. Suporte Filiais" },
            { "Saída e Entrada", "3. Saída/Entrada - Estoque" },
            { "Outras Atividades", "4. Outras Atividades" },
            { "Plantão", "5. Plantão" }
        };

        container.PaddingTop(20).Column(column =>
        {
            foreach (var kvp in categoriasRelatorio)
            {
                string categoriaViewModel = kvp.Key;
                string tituloCategoriaRelatorio = kvp.Value;

                column.Item().Text(tituloCategoriaRelatorio).SemiBold().FontSize(14);

                var itensDaCategoria = _model.Itens.Where(r => r.Categoria == categoriaViewModel).ToList();

                if (itensDaCategoria.Any())
                {
                    char letraItem = 'a';
                    foreach (var item in itensDaCategoria)
                    {
                        column.Item().PaddingLeft(20).Column(itemColumn =>
                        {
                            itemColumn.Item().PaddingTop(5).Text($"{letraItem}. {item.Titulo}");
                            itemColumn.Item().PaddingLeft(15).Text(item.Descricao).FontSize(10).FontColor(Colors.Grey.Darken2);
                        });
                        letraItem++;
                    }
                }
                else
                {
                    column.Item().PaddingLeft(20).Text("- Nada consta").Italic().FontSize(10).FontColor(Colors.Grey.Darken1);
                }

                column.Item().PaddingVertical(10);
            }
        });
    }
}
