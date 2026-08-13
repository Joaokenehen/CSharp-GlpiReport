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
    public Action? OnNavigateToGeneralReportsRequested { get; set; }
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
        // Em vez de limpar tudo, removemos apenas os itens que vieram do GLPI,
        // preservando os itens adicionados manualmente.
        var itensDoGlpi = Relatorios.Where(r => r.IsOrigemGlpi).ToList();
        foreach (var item in itensDoGlpi)
        {
            Relatorios.Remove(item);
        }
        _log.Info("Filtro", $"Iniciando filtragem para o dia selecionado (local): {DataSelecionada:dd/MM/yyyy HH:mm:ss zzz}");

        // 1. DEFINIÇÃO DAS JANELAS DE TEMPO (todas em UTC para comparação consistente)
        // Usamos DataSelecionada (que é local) como base para definir os horários locais
        var diaSelecionadoLocal = DataSelecionada.Date; // Ex: 2026-08-11 00:00:00 -03:00

        DateTimeOffset inicioPlantaoLocal;

        // Lógica de Plantão de Fim de Semana: Se hoje for segunda-feira, o plantão começa na sexta anterior.
        if (DataSelecionada.DayOfWeek == DayOfWeek.Monday) // Usamos DataSelecionada para o DayOfWeek, que é local
        {
            inicioPlantaoLocal = diaSelecionadoLocal.AddDays(-3).AddHours(18); // Sexta-feira, 18:00 local
        }
        else
        {
            inicioPlantaoLocal = diaSelecionadoLocal.AddDays(-1).AddHours(18); // Dia anterior, 18:00 local
        }

        var fimPlantaoLocal = diaSelecionadoLocal.AddHours(7).AddMinutes(30);   // 07:30 do dia atual local
        var inicioAlmocoPlantaoLocal = diaSelecionadoLocal.AddHours(11).AddMinutes(30); // 11:30 do dia atual local
        var fimAlmocoPlantaoLocal = diaSelecionadoLocal.AddHours(13).AddMinutes(30);   // 13:30 do dia atual local
        var inicioDiaNormalLocal = fimPlantaoLocal;                            // Início do dia de trabalho local
        var fimDiaNormalLocal = diaSelecionadoLocal.AddHours(18);              // Fim do dia de trabalho local
        var inicioPlantaoUtc = inicioPlantaoLocal.ToUniversalTime();
        var fimPlantaoUtc = fimPlantaoLocal.ToUniversalTime();
        var inicioAlmocoPlantaoUtc = inicioAlmocoPlantaoLocal.ToUniversalTime();
        var fimAlmocoPlantaoUtc = fimAlmocoPlantaoLocal.ToUniversalTime();
        var inicioDiaNormalUtc = inicioDiaNormalLocal.ToUniversalTime();
        var fimDiaNormalUtc = fimDiaNormalLocal.ToUniversalTime();

        _log.Info("Filtro", $"Janelas de tempo (UTC):");
        _log.Info("Filtro", $"  Plantão (Noite): {inicioPlantaoUtc:g} a {fimPlantaoUtc:g}");
        _log.Info("Filtro", $"  Plantão (Almoço): {inicioAlmocoPlantaoUtc:g} a {fimAlmocoPlantaoUtc:g}");
        _log.Info("Filtro", $"  Dia Normal: {inicioDiaNormalUtc:g} a {fimDiaNormalUtc:g}");

        int chamadosEncontrados = 0;
        int chamadosAbertosNoDia = 0;

        foreach (var chamado in _todosOsChamados)
        {
            // Helper para converter as datas do GLPI para UTC
            DateTime? ParseDate(string? dateStr)
            {
                if (string.IsNullOrWhiteSpace(dateStr)) return null;
                // A API do GLPI geralmente retorna datas em horário local sem indicação de fuso.
                // Assumimos que a string é local e convertemos para UTC para comparação consistente.
                if (DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out var dt)) return dt.ToUniversalTime();
                return null;
            }

            var dataCriacao = ParseDate(chamado.DataCriacao);
            var dataSolucao = ParseDate(chamado.DataSolucao);
            var dataFechamento = ParseDate(chamado.DataFechamento);
            var dataModificacao = ParseDate(chamado.DataModificacao);
            var dataAtribuicao = ParseDate(chamado.DataAtribuicao); // NOVO: Data de atribuição

            // Verifica se o chamado foi criado durante o dia de trabalho normal
            bool criadoNoDiaNormal = dataCriacao >= inicioDiaNormalUtc && dataCriacao < fimDiaNormalUtc;

            // 2. VERIFICAÇÃO DE RELEVÂNCIA (se o chamado pertence ao relatório de hoje)
            bool isPlantao = false;
            // Se o chamado foi CRIADO no horário de plantão, ele é de plantão.
            if (dataCriacao.HasValue)
            {
                if ((dataCriacao.Value >= inicioPlantaoUtc && dataCriacao.Value < fimPlantaoUtc) ||
                    (dataCriacao.Value >= inicioAlmocoPlantaoUtc && dataCriacao.Value < fimAlmocoPlantaoUtc))
                {
                    isPlantao = true;
                }
            }

            // Verifica por data de solução
            if (!isPlantao && dataSolucao.HasValue)
            {
                if ((dataSolucao.Value >= inicioPlantaoUtc && dataSolucao.Value < fimPlantaoUtc) ||
                    (dataSolucao.Value >= inicioAlmocoPlantaoUtc && dataSolucao.Value < fimAlmocoPlantaoUtc))
                {
                    isPlantao = true;
                }
            }
            // Se ainda não for plantão, verifica por data de modificação
            if (!isPlantao && dataModificacao.HasValue)
            {
                if ((dataModificacao.Value >= inicioPlantaoUtc && dataModificacao.Value < fimPlantaoUtc) ||
                    (dataModificacao.Value >= inicioAlmocoPlantaoUtc && dataModificacao.Value < fimAlmocoPlantaoUtc))
                {
                    isPlantao = true;
                }
            }
            // Se ainda não for plantão, verifica por data de atribuição (se houver)
            if (!isPlantao && dataAtribuicao.HasValue)
            {
                if ((dataAtribuicao.Value >= inicioPlantaoUtc && dataAtribuicao.Value < fimPlantaoUtc) ||
                    (dataAtribuicao.Value >= inicioAlmocoPlantaoUtc && dataAtribuicao.Value < fimAlmocoPlantaoUtc))
                {
                    isPlantao = true;
                }
            }

            bool isDiaNormal = false;
            if (dataCriacao.HasValue && (dataCriacao.Value >= inicioDiaNormalUtc && dataCriacao.Value < fimDiaNormalUtc)) isDiaNormal = true;
            if (!isDiaNormal && dataSolucao.HasValue && (dataSolucao.Value >= inicioDiaNormalUtc && dataSolucao.Value < fimDiaNormalUtc)) isDiaNormal = true;
            if (!isDiaNormal && dataFechamento.HasValue && (dataFechamento.Value >= inicioDiaNormalUtc && dataFechamento.Value < fimDiaNormalUtc)) isDiaNormal = true;

            _log.Info("Debug", $"Chamado {chamado.Id}:");
            _log.Info("Debug", $"  Data Criação (UTC): {dataCriacao:g}");
            _log.Info("Debug", $"  Data Solução (UTC): {dataSolucao:g}");
            _log.Info("Debug", $"  Data Modificação (UTC): {dataModificacao:g}");
            _log.Info("Debug", $"  Data Atribuição (UTC): {dataAtribuicao:g}"); // NOVO: Log da data de atribuição
            _log.Info("Debug", $"  Criado no Dia Normal: {criadoNoDiaNormal}");
            _log.Info("Debug", $"  É Plantão: {isPlantao}");
            _log.Info("Debug", $"  É Dia Normal: {isDiaNormal}");

            if (!isPlantao && !isDiaNormal) continue;

            _log.Info("Debug", $"-> Chamado {chamado.Id} é relevante para o relatório.");

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

            // REGRA DE NEGÓCIO: Se o setor for "Setor", renomeia para "Arrecadação".
            if (setor.Equals("Setor", StringComparison.OrdinalIgnoreCase))
            {
                setor = "Arrecadação";
            }

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
    private void NavigateToGeneralReports()
    {
        OnNavigateToGeneralReportsRequested?.Invoke();
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
                Icon.Warning);

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
            await _reportStateService.DeleteState(reportId);
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