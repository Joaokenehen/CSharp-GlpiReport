using RelatorioGLPIApp.Models;

namespace RelatorioGLPIApp.Services
{
    public interface IOnDutyChecker
    {
        bool IsTicketOnDuty(Chamado ticket, bool filterForToday);
    }
}