using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.Services;

namespace RelatorioGLPIApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly List<Chamado> _todosOsChamados;
    private readonly ILogService _log;

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

    public DashboardViewModel(List<Chamado> chamadosDoGlpi)
    {
        _log = new LogService();
        _todosOsChamados = chamadosDoGlpi;
        Relatorios = new ObservableCollection<RelatorioItem>();

        FiltrarPorData();
    }

    [RelayCommand]
    private void FiltrarPorData()
    {
        Relatorios.Clear();
        string dataAlvo = DataSelecionada.ToString("yyyy-MM-dd");
        _log.Info("Filtro", $"Buscando chamados para a data: {dataAlvo}...");

        int chamadosEncontrados = 0;

        foreach (var chamado in _todosOsChamados)
        {
            // 1. FILTRO DE DATA
            if (!chamado.DataCriacao.StartsWith(dataAlvo)) continue;

            // Se passou da data, vamos "espiar" o que o GLPI mandou:
            _log.Info("Debug", $"Chamado {chamado.Id} é de hoje. Status: {chamado.Status} | Técnico no GLPI: '{chamado.TecnicoAtribuido}'");

            // 2. FILTRO DE STATUS
            if (chamado.Status < 2 || chamado.Status > 6)
            {
                _log.Info("Debug", $"-> Chamado {chamado.Id} ignorado pois o Status é {chamado.Status}");
                continue;
            }

            // 3. FILTRO DE USUÁRIO TI
            if (!string.IsNullOrWhiteSpace(UsuarioTi))
            {
                string tecnico = chamado.TecnicoAtribuido ?? "";

                if (!tecnico.ToLower().Contains(UsuarioTi.ToLower()))
                {
                    _log.Info("Debug", $"-> Chamado {chamado.Id} ignorado pelo Técnico. Você digitou '{UsuarioTi}', mas o GLPI mandou '{tecnico}'");
                    continue;
                }
            }

            string descricaoLimpa = WebUtility.HtmlDecode(chamado.Descricao ?? "");
            descricaoLimpa = descricaoLimpa.Replace("&nbsp;", " ");
            descricaoLimpa = Regex.Replace(descricaoLimpa, "<.*?>", string.Empty).Trim();

            string arvoreEntidade = WebUtility.HtmlDecode(chamado.Entidade ?? "Matriz");
            string categoriaAutomatica = arvoreEntidade.Contains("Filiais") ? "Suporte Filiais" : "Suporte Matriz";

            string[] partesEntidade = arvoreEntidade.Split('>');
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

            Relatorios.Add(new RelatorioItem
            {
                Categoria = categoriaAutomatica,
                Titulo = $"~Chamado: {chamado.Id} – {setor} - {usuario}~",
                Descricao = descricaoLimpa,
                IsOrigemGlpi = true,
                StatusTag = tagTexto,
                CorStatus = corFundo
            });

            chamadosEncontrados++;
        }

        OrdenarLista();

        if (chamadosEncontrados > 0)
            _log.Sucesso("Filtro", $"{chamadosEncontrados} chamados aprovados nos filtros.");
        else
            _log.Info("Filtro", "Nenhum chamado passou.");
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
}