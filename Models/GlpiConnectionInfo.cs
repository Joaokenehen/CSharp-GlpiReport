using System.Collections.Generic;
using RelatorioGLPIApp.Services;

namespace RelatorioGLPIApp.Models;

public record GlpiConnectionInfo(
    string Url,
    string AppToken,
    string SessionToken,
    IChamadoService ChamadoService,
    List<Chamado> InitialChamados
);
