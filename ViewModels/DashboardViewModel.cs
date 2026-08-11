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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Platform.Storage;
using QuestPDF.Fluent;
using Xceed.Words.NET;
using System.IO;
using Xceed.Document.NET;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.Documents;
using RelatorioGLPIApp.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using MessageBox.Avalonia.Enums;



namespace RelatorioGLPIApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly string _url;
    private readonly string _appToken;
    private readonly string _sessionToken;
    private readonly IChamadoService _chamadoService;
    private readonly IReportStateService _reportStateService;

    private readonly ILogService _log;
    private List<Chamado> _todosOsChamados;

    [ObservableProperty]
    private ObservableCollection<RelatorioItem> _relatorios;

    [ObservableProperty]
    private DateTimeOffset _dataSelecionada = DateTimeOffset.Now;

    // NOVO: Campo para você digitar seu nome de técnico (Ex: joao.gustavo)
    [ObservableProperty]
    private string _usuarioTi = "";

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    [ObservableProperty]
    private bool _isNotificationVisible;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private int _totalItensRelatorio;

    [ObservableProperty]
    private int _totalItensSolucionados;

    [ObservableProperty]
    private int _totalChamadosAbertos;

    [ObservableProperty]
    private string _buscarChamadosButtonText = "Buscar Chamados";

    [ObservableProperty]
    private string _reportSaveName = "";

    [ObservableProperty]
    private ObservableCollection<string> _savedReports = new();

    public Action? OnLogoutRequested { get; set; }
    public Action<RelatorioItem>? OnItemAdded { get; set; }

    public DashboardViewModel(GlpiConnectionInfo connectionInfo)
    {
        _log = new LogService();

        _url = connectionInfo.Url;
        _appToken = connectionInfo.AppToken;
        _sessionToken = connectionInfo.SessionToken;
        _chamadoService = connectionInfo.ChamadoService;
        _reportStateService = new ReportStateService();
        _todosOsChamados = connectionInfo.InitialChamados;

        Relatorios = new ObservableCollection<RelatorioItem>();

        AplicarFiltrosNaLista();

        // Carrega a lista de relatórios salvos
        _ = LoadSavedReportsList();
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
        var inicioAlmocoPlantao = diaSelecionado.AddHours(11).AddMinutes(30); // 11:30 do dia atual
        var fimAlmocoPlantao = diaSelecionado.AddHours(13).AddMinutes(30);   // 13:30 do dia atual
        var inicioDiaNormal = fimPlantao;                            // Início do dia de trabalho
        var fimDiaNormal = diaSelecionado.AddHours(18);              // Fim do dia de trabalho

        _log.Info("Filtro", $"Filtrando chamados para o dia {diaSelecionado:dd/MM/yyyy}. Janela Plantão (Noite): {inicioPlantao:g} a {fimPlantao:g}. Janela Plantão (Almoço): {inicioAlmocoPlantao:g} a {fimAlmocoPlantao:g}.");

        int chamadosEncontrados = 0;
        int chamadosAbertosNoDia = 0;

        foreach (var chamado in _todosOsChamados)
        {
            // Helper para converter as datas do GLPI para um formato utilizável
            DateTime? ParseDate(string? dateStr) => DateTime.TryParse(dateStr, out var dt) ? dt : null;

            var dataCriacao = ParseDate(chamado.DataCriacao);
            var dataSolucao = ParseDate(chamado.DataSolucao);
            var dataFechamento = ParseDate(chamado.DataFechamento);
            var dataModificacao = ParseDate(chamado.DataModificacao);

            // Verifica se o chamado foi criado durante o dia de trabalho normal
            bool criadoNoDiaNormal = dataCriacao >= inicioDiaNormal && dataCriacao < fimDiaNormal;

            // 2. VERIFICAÇÃO DE RELEVÂNCIA (se o chamado pertence ao relatório de hoje)
            bool isPlantao = ((dataSolucao >= inicioPlantao && dataSolucao < fimPlantao) ||
                              (dataModificacao >= inicioPlantao && dataModificacao < fimPlantao)) ||
                             ((dataSolucao >= inicioAlmocoPlantao && dataSolucao < fimAlmocoPlantao) ||
                              (dataModificacao >= inicioAlmocoPlantao && dataModificacao < fimAlmocoPlantao));

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

            // Contabiliza o KPI de chamados abertos no dia
            if (criadoNoDiaNormal && (categoriaFinal == "Suporte Matriz" || categoriaFinal == "Suporte Filiais"))
            {
                chamadosAbertosNoDia++;
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

        TotalChamadosAbertos = chamadosAbertosNoDia;
        UpdateKpis();
    }

    [RelayCommand] // Este comando agora busca os dados mais recentes e aplica os filtros.
    private async Task BuscarChamados()
    {
        IsSearching = true;
        BuscarChamadosButtonText = "Buscando...";
        try
        {
            _log.Info("Busca", "Iniciando busca e atualização de chamados no GLPI...");
            _todosOsChamados = await _chamadoService.ObterChamadosAsync(_url, _appToken, _sessionToken);
            _log.Sucesso("Busca", $"{_todosOsChamados.Count} chamados sincronizados com o GLPI.");

            AplicarFiltrosNaLista();
        }
        finally
        {
            IsSearching = false;
            BuscarChamadosButtonText = "Buscar Chamados";
        }
    }

    [RelayCommand]
    private void AdicionarItemManualPorCategoria(string categoria)
    {
        if (string.IsNullOrEmpty(categoria)) return;

        var novoItem = new RelatorioItem
        {
            Categoria = categoria,
            Titulo = "~Nova Atividade~",
            Descricao = "Descreva o que foi feito aqui...",
            IsOrigemGlpi = false,
            StatusTag = "Manual",
            CorStatus = "#6F42C1" // Roxo para os manuais
        };

        Relatorios.Add(novoItem);
        OrdenarLista();
        UpdateKpis();
        OnItemAdded?.Invoke(novoItem);
    }

    [RelayCommand]
    private void RemoverItem(RelatorioItem itemParaRemover)
    {
        if (itemParaRemover != null && Relatorios.Remove(itemParaRemover))
            UpdateKpis();
    }

    private void OrdenarLista()
    {
        // ... (código existente)

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
    private void Sair()
    {
        OnLogoutRequested?.Invoke();
    }

    [RelayCommand]
    private void UseSavedReportName(string? reportFileName)
    {
        if (string.IsNullOrWhiteSpace(reportFileName)) return;

        // Remove the .json extension para preencher o campo de texto
        ReportSaveName = Path.GetFileNameWithoutExtension(reportFileName);
    }

    [RelayCommand]
    private async Task SaveState()
    {
        if (string.IsNullOrWhiteSpace(ReportSaveName))
        {
            await ShowNotificationAsync("Por favor, insira um nome para o relatório.");
            return;
        }

        bool shouldSave = true;
        if (await _reportStateService.ReportExists(ReportSaveName))
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard(
                "Arquivo Existente",
                $"Um relatório com o nome {ReportSaveName} já existe.\nDeseja substituí-lo?",
                ButtonEnum.YesNo,
                MsBox.Avalonia.Enums.Icon.Warning); // <-- Correção aqui

            var result = await messageBox.ShowAsync();
            shouldSave = result == ButtonResult.Yes;
        }

        if (shouldSave)
        {
            try
            {
                var state = new SavedReportState
                {
                    ReportDate = this.DataSelecionada,
                    TechnicianUsername = this.UsuarioTi,
                    Items = this.Relatorios.ToList()
                };

                // O nome do arquivo agora é sanitizado dentro do serviço
                var sanitizedName = string.Join("_", ReportSaveName.Split(Path.GetInvalidFileNameChars()));
                var finalFileName = string.IsNullOrWhiteSpace(sanitizedName) ? $"Relatorio_sem_nome_{DateTime.Now:yyyyMMddHHmmss}.json" : $"{sanitizedName}.json";

                await _reportStateService.SaveState(state, finalFileName);
                await ShowNotificationAsync($"Relatório '{ReportSaveName}' salvo com sucesso!");
                await LoadSavedReportsList(); // Atualiza a lista no menu
                ReportSaveName = ""; // Limpa o campo após salvar
            }
            catch (Exception ex)
            {
                _log.Erro("SaveState", $"Falha ao salvar relatório: {ex.Message}");
                await ShowNotificationAsync("Erro ao salvar o relatório.");
            }
        }
    }

    [RelayCommand]
    private async Task LoadState(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return;

        var state = await _reportStateService.LoadState(reportId);
        if (state != null)
        {
            DataSelecionada = state.ReportDate;
            UsuarioTi = state.TechnicianUsername;

            Relatorios.Clear();
            foreach (var item in state.Items)
            {
                Relatorios.Add(item);
            }

            UpdateKpis();
            await ShowNotificationAsync($"Relatório '{reportId}' carregado!");
        }
    }

    [RelayCommand]
    private async Task CopiarRelatorio()
    {
        string relatorioFinal = GerarTextoDoRelatorio();

        var topLevel = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (topLevel?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(relatorioFinal); // Agora SetTextAsync deve funcionar!
            _log.Sucesso("Relatório", "Relatório formatado copiado para a área de transferência!"); // Log interno
            await ShowNotificationAsync("Copiado para a área de transferência!"); // Notificação para o usuário
        }
        else
        {
            _log.Erro("Relatório", "Não foi possível acessar a área de transferência.");
            await ShowNotificationAsync("Erro ao copiar para a área de transferência!");
        }
    }

    [RelayCommand]
    private async Task DeleteSavedReport(string? reportId)
    {
        if (string.IsNullOrWhiteSpace(reportId)) return;

        var messageBox = MessageBoxManager.GetMessageBoxStandard(
            "Excluir Relatório",
            $"Tem certeza que deseja excluir o relatório '{reportId}'?",
            ButtonEnum.YesNo,
            Icon.Warning);

        var result = await messageBox.ShowAsync();
        if (result == ButtonResult.Yes)
        {
            await _reportStateService.Delete(reportId);
            await LoadSavedReportsList();
            await ShowNotificationAsync($"Relatório '{reportId}' excluído com sucesso!");
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

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório em PDF",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[] { new FilePickerFileType("Arquivos PDF") { Patterns = new[] { "*.pdf" } } }
        });

        if (file is not null)
        {
            try
            {
                // Usar um stream para escrever o arquivo é mais robusto
                await using (var stream = await file.OpenWriteAsync())
                {
                    var model = new RelatorioPdfModel(UsuarioTi, DataSelecionada, Relatorios.ToList(), TotalItensRelatorio, TotalItensSolucionados, TotalChamadosAbertos);
                    var document = new RelatorioPdfDocument(model);
                    document.GeneratePdf(stream);
                }
                _log.Sucesso("Exportar", $"Relatório salvo com sucesso em: {file.Name}");
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

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar Relatório em Word",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new[] { new FilePickerFileType("Documentos do Word") { Patterns = new[] { "*.docx" } } }
        });

        if (file is not null)
        {
            try
            {
                // Usar um stream para escrever o arquivo é mais robusto
                await using (var stream = await file.OpenWriteAsync())
                {
                    using (var document = DocX.Create(stream))
                    {
                        GerarConteudoWord(document);
                        document.Save(); // Salva no stream com o qual foi criado
                    }
                }
                _log.Sucesso("Exportar", $"Relatório salvo com sucesso em: {file.Name}");
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
        sb.AppendLine($"*Abertos: {TotalChamadosAbertos} | Solucionados: {TotalItensSolucionados} | Total: {TotalItensRelatorio}*");
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

        var kpiParagraph = document.InsertParagraph($"Abertos: {TotalChamadosAbertos} | Solucionados: {TotalItensSolucionados} | Total: {TotalItensRelatorio}");
        kpiParagraph.FontSize(11).Italic().Alignment = Alignment.center;
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

    private async Task ShowNotificationAsync(string message, int durationMs = 3000)
    {
        NotificationMessage = message;
        IsNotificationVisible = true;
        await Task.Delay(durationMs);
        IsNotificationVisible = false;
    }

    private void UpdateKpis()
    {
        TotalItensRelatorio = Relatorios.Count;
        TotalItensSolucionados = Relatorios.Count(r => r.StatusTag == "Solucionado" || r.StatusTag == "Fechado");
    }

    private async Task LoadSavedReportsList()
    {
        var reports = await _reportStateService.GetSavedReportIds();
        SavedReports.Clear();
        foreach (var report in reports.OrderByDescending(r => r))
            SavedReports.Add(report);
    }
}