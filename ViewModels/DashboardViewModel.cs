using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xceed.Words.NET;
using System.IO;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.Documents;
using RelatorioGLPIApp.Services;

namespace RelatorioGLPIApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    // Armazena os dados da conexão para poder atualizar
    private readonly string _url;
    private readonly string _appToken;
    private readonly string _sessionToken;
    private readonly IChamadoService _chamadoService;

    private readonly ILogService _log;
    private List<Chamado> _todosOsChamados;

    [ObservableProperty]
    private ObservableCollection<RelatorioItem> _relatorios;

    [ObservableProperty]
    private DateTimeOffset _dataSelecionada = DateTimeOffset.Now;

    // NOVO: Campo para você digitar seu nome de técnico (Ex: joao.gustavo)
    [ObservableProperty]
    private string _usuarioTi = "";

    public ObservableCollection<string> CategoriasDisponiveis { get; } = new()
    {
        "Suporte Matriz",
        "Suporte Filiais",
        "Saída e Entrada",
        "Outras Atividades",
        "Plantão"
    };

    [ObservableProperty]
    private string _categoriaSelecionadaNova = "Outras Atividades";

    public DashboardViewModel(GlpiConnectionInfo connectionInfo)
    {
        _log = new LogService();

        _url = connectionInfo.Url;
        _appToken = connectionInfo.AppToken;
        _sessionToken = connectionInfo.SessionToken;
        _chamadoService = connectionInfo.ChamadoService;
        _todosOsChamados = connectionInfo.InitialChamados;

        Relatorios = new ObservableCollection<RelatorioItem>();

        AplicarFiltrosNaLista();
    }

    private void AplicarFiltrosNaLista()
    {
        Relatorios.Clear();

        // 1. DEFINIÇÃO DAS JANELAS DE TEMPO
        var diaSelecionado = DataSelecionada.Date;
        DateTime inicioPlantao;

        // Lógica de Plantão de Fim de Semana: Se hoje for segunda-feira, o plantão começa na sexta anterior.
        if (diaSelecionado.DayOfWeek == DayOfWeek.Monday)
        {
            inicioPlantao = diaSelecionado.AddDays(-3).AddHours(18); // Sexta-feira, 18:00
        }
        else
        {
            inicioPlantao = diaSelecionado.AddDays(-1).AddHours(18); // Dia anterior, 18:00
        }

        var fimPlantao = diaSelecionado.AddHours(7).AddMinutes(30);   // 07:30 do dia atual
        var inicioDiaNormal = fimPlantao;                            // Início do dia de trabalho
        var fimDiaNormal = diaSelecionado.AddHours(18);              // Fim do dia de trabalho

        _log.Info("Filtro", $"Filtrando chamados para o dia {diaSelecionado:dd/MM/yyyy}. Janela Plantão: {inicioPlantao:g} a {fimPlantao:g}.");

        int chamadosEncontrados = 0;

        foreach (var chamado in _todosOsChamados)
        {
            // Helper para converter as datas do GLPI para um formato utilizável
            DateTime? ParseDate(string? dateStr) => DateTime.TryParse(dateStr, out var dt) ? dt : null;

            var dataCriacao = ParseDate(chamado.DataCriacao);
            var dataSolucao = ParseDate(chamado.DataSolucao);
            var dataFechamento = ParseDate(chamado.DataFechamento);
            var dataModificacao = ParseDate(chamado.DataModificacao);

            // 2. VERIFICAÇÃO DE RELEVÂNCIA (se o chamado pertence ao relatório de hoje)
            bool isPlantao = (dataSolucao >= inicioPlantao && dataSolucao < fimPlantao) ||
                             (dataModificacao >= inicioPlantao && dataModificacao < fimPlantao);

            bool isDiaNormal = (dataCriacao >= inicioDiaNormal && dataCriacao < fimDiaNormal) ||
                               (dataSolucao >= inicioDiaNormal && dataSolucao < fimDiaNormal) ||
                               (dataFechamento >= inicioDiaNormal && dataFechamento < fimDiaNormal);

            if (!isPlantao && !isDiaNormal) continue;

            // Se passou, o chamado é relevante. Agora aplicamos os outros filtros.
            _log.Info("Debug", $"Chamado {chamado.Id} é relevante. Plantão: {isPlantao}, Dia Normal: {isDiaNormal}.");

            // 3. FILTRO DE STATUS (mesma lógica de antes)
            if (chamado.Status < 1 || chamado.Status > 6)
            {
                _log.Info("Debug", $"-> Chamado {chamado.Id} ignorado pois o Status é {chamado.Status}");
                continue;
            }

            // 4. FILTRO DE USUÁRIO TI (mesma lógica de antes)
            if (!string.IsNullOrWhiteSpace(UsuarioTi))
            {
                string tecnico = chamado.TecnicoAtribuido ?? "";
                if (!tecnico.ToLower().Contains(UsuarioTi.ToLower()))
                {
                    _log.Info("Debug", $"-> Chamado {chamado.Id} ignorado pelo Técnico. Você digitou '{UsuarioTi}', mas o GLPI mandou '{tecnico}'");
                    continue;
                }
            }

            // 5. PREPARAÇÃO DOS DADOS PARA EXIBIÇÃO (mesma lógica de antes)
            string descricaoLimpa = WebUtility.HtmlDecode(chamado.Descricao ?? "");
            descricaoLimpa = descricaoLimpa.Replace("&nbsp;", " ");
            descricaoLimpa = Regex.Replace(descricaoLimpa, "<.*?>", string.Empty).Trim();

            // NOVA LÓGICA DE CATEGORIZAÇÃO
            string categoriaFinal;
            if (isPlantao)
            {
                categoriaFinal = "Plantão";
            }
            else
            {
                string arvoreEntidade = WebUtility.HtmlDecode(chamado.Entidade ?? "Matriz");
                categoriaFinal = arvoreEntidade.Contains("Filiais") ? "Suporte Filiais" : "Suporte Matriz";
            }

            string[] partesEntidade = WebUtility.HtmlDecode(chamado.Entidade ?? "Matriz").Split('>');
            string setor = partesEntidade[^1].Trim();

            string usuario = chamado.NomeUsuario ?? "Usuário";
            if (usuario.Contains('.'))
            {
                usuario = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(usuario.Replace('.', ' '));
            }

            string tagTexto = "Em Atendimento";
            string corFundo = "#0D6EFD";

            if (chamado.Status == 5) { tagTexto = "Solucionado"; corFundo = "#198754"; }
            else if (chamado.Status == 6) { tagTexto = "Fechado"; corFundo = "#212529"; }
            else if (chamado.Status == 4) { tagTexto = "Pendente"; corFundo = "#FD7E14"; }

            string nomeTecnico;
            if (string.IsNullOrWhiteSpace(chamado.TecnicoAtribuido))
            {
                nomeTecnico = "Não atribuído";
            }
            else
            {
                // Formata cada nome de técnico individualmente
                var nomes = chamado.TecnicoAtribuido.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(n => n.Trim())
                                                    .Select(n => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(n.Replace('.', ' ')));
                nomeTecnico = string.Join(", ", nomes);
            }

            string titulo;
            if (categoriaFinal == "Suporte Filiais")
            {
                titulo = $"~Chamado: {chamado.Id} – {setor}~";
            }
            else
            {
                titulo = $"~Chamado: {chamado.Id} – {setor} - {usuario}~";
            }

            Relatorios.Add(new RelatorioItem
            {
                Categoria = categoriaFinal,
                Titulo = titulo,
                Descricao = descricaoLimpa,
                IsOrigemGlpi = true,
                StatusTag = tagTexto,
                CorStatus = corFundo,
                Tecnico = $"Téc: {nomeTecnico}"
            });

            chamadosEncontrados++;
        }

        OrdenarLista();

        if (chamadosEncontrados > 0)
            _log.Sucesso("Filtro", $"{chamadosEncontrados} chamados aprovados nos filtros.");
        else
            _log.Info("Filtro", "Nenhum chamado passou.");
    }

    [RelayCommand] // Este comando agora busca os dados mais recentes e aplica os filtros.
    private async Task BuscarChamados()
    {
        _log.Info("Busca", "Iniciando busca e atualização de chamados no GLPI...");
        _todosOsChamados = await _chamadoService.ObterChamadosAsync(_url, _appToken, _sessionToken);
        _log.Sucesso("Busca", $"{_todosOsChamados.Count} chamados sincronizados com o GLPI.");

        AplicarFiltrosNaLista();
    }

    [RelayCommand]
    private void AdicionarItemManual()
    {
        Relatorios.Add(new RelatorioItem
        {
            Categoria = CategoriaSelecionadaNova,
            Titulo = "~Nova Atividade~",
            Descricao = "Descreva o que foi feito aqui...",
            IsOrigemGlpi = false,
            StatusTag = "Manual",
            CorStatus = "#6F42C1" // Roxo para os manuais
        });
        OrdenarLista();
    }

    [RelayCommand]
    private void RemoverItem(RelatorioItem itemParaRemover)
    {
        if (itemParaRemover != null) Relatorios.Remove(itemParaRemover);
    }

    private void OrdenarLista()
    {
        var ordem = new Dictionary<string, int>
        {
            { "Suporte Matriz", 1 }, { "Suporte Filiais", 2 },
            { "Saída e Entrada", 3 }, { "Outras Atividades", 4 }, { "Plantão", 5 }
        };

        var listaOrdenada = Relatorios.OrderBy(x => ordem.TryGetValue(x.Categoria ?? "", out int peso) ? peso : 99).ToList();
        Relatorios.Clear();
        foreach (var item in listaOrdenada) Relatorios.Add(item);
    }

    [RelayCommand]
    private void CopiarRelatorio()
    {
        string relatorioFinal = GerarTextoDoRelatorio();

        var topLevel = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel?.Clipboard is { } clipboard)
        {
            // Usando SetText síncrono como fallback para versões mais antigas do Avalonia
            clipboard.SetText(relatorioFinal);
            _log.Sucesso("Relatório", "Relatório formatado copiado para a área de transferência!");
        }
        else
        {
            _log.Erro("Relatório", "Não foi possível acessar a área de transferência.");
        }
    }

    [RelayCommand]
    private async Task ExportarPdf()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            _log.Erro("Exportar", "Não foi possível obter a janela principal para abrir o diálogo de salvamento.");
            return;
        }

        var suggestedFileName = $"Relatorio_{UsuarioTi.Replace('.', '_')}_{DataSelecionada:yyyy_MM_dd}.pdf";

        var file = await topLevel.Storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório em PDF",
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = "pdf",
            FileTypeChoices = new[] { new FilePickerFileType("Arquivos PDF") { Patterns = new[] { "*.pdf" } } }
        });

        if (file != null && file.Path.LocalPath != null)
        {
            try
            {
                var model = new RelatorioPdfModel(UsuarioTi, DataSelecionada, Relatorios.ToList());
                var document = new RelatorioPdfDocument(model);
                document.GeneratePdf(file.Path.LocalPath);
                _log.Sucesso("Exportar", $"Relatório salvo com sucesso em: {file.Path.LocalPath}");
            }
            catch (Exception ex)
            {
                _log.Erro("Exportar PDF", $"Ocorreu um erro ao gerar o PDF: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ExportarWord()
    {
        var topLevel = GetTopLevel();
        if (topLevel == null)
        {
            _log.Erro("Exportar", "Não foi possível obter a janela principal para abrir o diálogo de salvamento.");
            return;
        }

        var suggestedFileName = $"Relatorio_{UsuarioTi.Replace('.', '_')}_{DataSelecionada:yyyy_MM_dd}.docx";

        var file = await topLevel.Storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório em Word",
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = "docx",
            FileTypeChoices = new[] { new FilePickerFileType("Documentos do Word") { Patterns = new[] { "*.docx" } } }
        });

        if (file != null && file.Path.LocalPath != null)
        {
            try
            {
                using (var document = DocX.Create(file.Path.LocalPath))
                {
                    GerarConteudoWord(document);
                    document.Save();
                }
                _log.Sucesso("Exportar", $"Relatório salvo com sucesso em: {file.Path.LocalPath}");
            }
            catch (Exception ex)
            {
                _log.Erro("Exportar Word", $"Ocorreu um erro ao gerar o documento Word: {ex.Message}");
            }
        }
    }

    private Avalonia.Controls.TopLevel? GetTopLevel()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }

    private string GerarTextoDoRelatorio()
    {
        var sb = new StringBuilder();

        // 1. Título Principal
        string nomeTecnico = "Técnico";
        if (!string.IsNullOrWhiteSpace(UsuarioTi))
        {
            nomeTecnico = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(UsuarioTi.Replace('.', ' '));
        }
        sb.AppendLine($"*Relatório {nomeTecnico} – {DataSelecionada:dd/MM/yyyy}*");
        sb.AppendLine();

        // 2. Mapeamento de categorias para o texto do relatório
        var categoriasRelatorio = new Dictionary<string, string>
        {
            { "Suporte Matriz", "1.\tSuporte Matriz" },
            { "Suporte Filiais", "2.\tSuporte Filiais" },
            { "Saída e Entrada", "3.\tSaída/Entrada - Estoque" },
            { "Outras Atividades", "4.\tOutras Atividades" },
            { "Plantão", "5.\tPlantão" }
        };

        // 3. Iterar sobre as categorias e construir o relatório
        foreach (var kvp in categoriasRelatorio)
        {
            string categoriaViewModel = kvp.Key;
            string tituloCategoriaRelatorio = kvp.Value;

            sb.AppendLine(tituloCategoriaRelatorio);

            var itensDaCategoria = Relatorios.Where(r => r.Categoria == categoriaViewModel).ToList();

            if (itensDaCategoria.Any())
            {
                char letraItem = 'a';
                foreach (var item in itensDaCategoria)
                {
                    sb.AppendLine($"\t{letraItem}.\t{item.Titulo}");
                    sb.AppendLine(item.Descricao);
                    sb.AppendLine();
                    letraItem++;
                }
            }
            else
            {
                sb.AppendLine("\t-\tNada consta");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    private void GerarConteudoWord(DocX document)
    {
        // 1. Título Principal
        string nomeTecnico = "Técnico";
        if (!string.IsNullOrWhiteSpace(UsuarioTi))
        {
            nomeTecnico = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(UsuarioTi.Replace('.', ' '));
        }
        var tituloPrincipal = document.InsertParagraph($"Relatório {nomeTecnico} – {DataSelecionada:dd/MM/yyyy}");
        tituloPrincipal.Bold().FontSize(16);
        tituloPrincipal.Alignment = Alignment.center;
        document.InsertParagraph(""); // Linha em branco

        // 2. Mapeamento de categorias
        var categoriasRelatorio = new Dictionary<string, string>
        {
            { "Suporte Matriz", "1. Suporte Matriz" },
            { "Suporte Filiais", "2. Suporte Filiais" },
            { "Saída e Entrada", "3. Saída/Entrada - Estoque" },
            { "Outras Atividades", "4. Outras Atividades" },
            { "Plantão", "5. Plantão" }
        };

        // 3. Iterar sobre as categorias
        foreach (var kvp in categoriasRelatorio)
        {
            string categoriaViewModel = kvp.Key;
            string tituloCategoriaRelatorio = kvp.Value;

            document.InsertParagraph(tituloCategoriaRelatorio).Bold().FontSize(14);

            var itensDaCategoria = Relatorios.Where(r => r.Categoria == categoriaViewModel).ToList();

            if (itensDaCategoria.Any())
            {
                char letraItem = 'a';
                foreach (var item in itensDaCategoria)
                {
                    document.InsertParagraph($"{letraItem}. {item.Titulo}").IndentationBefore = 20f;
                    document.InsertParagraph(item.Descricao).IndentationBefore = 40f;
                    document.InsertParagraph("");
                    letraItem++;
                }
            }
            else
            {
                document.InsertParagraph("- Nada consta").IndentationBefore = 20f;
                document.InsertParagraph("");
            }
        }
    }
}