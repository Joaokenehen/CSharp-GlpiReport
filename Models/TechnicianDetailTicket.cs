namespace RelatorioGLPIApp.Models;

public record TechnicianDetailTicket(
    int Id,
    string Title,
    string Type // "Plantão" ou "Normal"
);