using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Text.RegularExpressions;
using RelatorioGLPIApp.Models;
using RelatorioGLPIApp.Services; // <-- Precisamos disso para o Log

namespace RelatorioGLPIApp.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly List<Chamado> _todosOsChamados;
    private readonly ILogService _log; // 1. Nossa variável de log

    [ObservableProperty]
    private ObservableCollection<RelatorioItem> _relatorios;

    [ObservableProperty]
    private DateTimeOffset _dataSelecionada = DateTimeOffset.Now;

    public DashboardViewModel(List<Chamado> chamadosDoGlpi)
    {
        _log = new LogService(); // 2. Ligamos o motor de logs
        _todosOsChamados = chamadosDoGlpi;
        Relatorios = new ObservableCollection<RelatorioItem>();

        FiltrarPorData();
    }

    [RelayCommand]
    private void FiltrarPorData()
    {
        Relatorios.Clear();
        string dataAlvo = DataSelecionada.ToString("yyyy-MM-dd");

        _log.Info("Filtro", $"Filtrando chamados para a data: {dataAlvo}...");

        int chamadosEncontrados = 0;

        foreach (var chamado in _todosOsChamados)
        {
            if (chamado.DataCriacao.StartsWith(dataAlvo))
            {
                string descricaoLimpa = WebUtility.HtmlDecode(chamado.Descricao ?? "");
                descricaoLimpa = descricaoLimpa.Replace("&nbsp;", " ");
                descricaoLimpa = Regex.Replace(descricaoLimpa, "<.*?>", string.Empty).Trim();

                string arvoreEntidade = chamado.Entidade ?? "Matriz";
                arvoreEntidade = WebUtility.HtmlDecode(arvoreEntidade);
                string categoriaAutomatica = arvoreEntidade.Contains("Filiais") ? "Suporte Filiais" : "Suporte Matriz";

                string[] partesEntidade = arvoreEntidade.Split('>');
                string setor = partesEntidade[^1].Trim();

                string usuario = chamado.NomeUsuario ?? "Usuário";

                if (usuario.Contains('.'))
                {
                    // Troca o ponto por espaço (ex: "maria.negri" vira "maria negri")
                    usuario = usuario.Replace('.', ' ');

                    // Coloca as primeiras letras em maiúsculo (ex: "maria negri" vira "Maria Negri")
                    usuario = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(usuario);
                }

                Relatorios.Add(new RelatorioItem
                {
                    Categoria = categoriaAutomatica,
                    Titulo = $"~Chamado: {chamado.Id} – {setor} - {usuario}~",
                    Descricao = descricaoLimpa,
                    IsOrigemGlpi = true
                });

                chamadosEncontrados++;
            }
        }

        // 3. O Feedback no terminal!
        if (chamadosEncontrados > 0)
        {
            _log.Sucesso("Filtro", $"{chamadosEncontrados} chamados encontrados e adicionados na tela.");
        }
        else
        {
            _log.Info("Filtro", $"Nenhum chamado do dia {dataAlvo} estava entre os últimos {_todosOsChamados.Count} carregados do GLPI.");
        }
    }

    [RelayCommand]
    private void AdicionarItemManual()
    {
        Relatorios.Add(new RelatorioItem
        {
            Categoria = "Outras Atividades",
            Titulo = "~Nova Atividade~",
            Descricao = "Descreva o que foi feito aqui...",
            IsOrigemGlpi = false
        });
        _log.Info("Card", "Novo card manual adicionado na tela.");
    }

    [RelayCommand]
    private void RemoverItem(RelatorioItem itemParaRemover)
    {
        if (itemParaRemover != null)
        {
            Relatorios.Remove(itemParaRemover);
            _log.Info("Card", "Um card foi removido da listagem.");
        }
    }
}