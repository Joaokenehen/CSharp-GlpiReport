using System.Collections.Generic;
using RelatorioGLPIApp.Services;

namespace RelatorioGLPIApp.Models;

public class GlpiConnectionInfo
{
    public string Url { get; }
    public string AppToken { get; }
    public string SessionToken { get; }
    public IChamadoService ChamadoService { get; }
    public List<Chamado> InitialChamados { get; }

    public bool IsOffline { get; }

    public GlpiConnectionInfo(string url, string appToken, string sessionToken, IChamadoService chamadoService, List<Chamado> initialChamados, bool isOffline = false)
    {
        Url = url;
        AppToken = appToken;
        SessionToken = sessionToken;
        ChamadoService = chamadoService;
        InitialChamados = initialChamados;
        IsOffline = isOffline; // Salva o valor
    }
}

