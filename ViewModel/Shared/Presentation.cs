namespace LeadScopeB2B.ViewModels.Shared;

public record PageHeader(string Title, string Description);
public record EmptyState(string Title, string Description, string Icon = "search-x");
public static class Presentation
{
    public static string Status(string? value) => value switch
    {
        "PENDENTE" => "Em verificação", "VALIDADA" => "Ativa", "INATIVA" => "Inativa",
        "VALIDADO" => "Validado", "BLOQUEADO" => "Bloqueado", "NOVO" => "Novo",
        "EM_ANALISE" => "Em análise", "QUALIFICADO" => "Qualificado", "REJEITADO" => "Rejeitado",
        "ATIVO" => "Ativo", "INATIVO" => "Inativo", "INCERTO" => "Incerto", _ => value ?? "—"
    };
    public static string Tone(string? value) => value switch
    {
        "VALIDADA" or "VALIDADO" or "QUALIFICADO" or "ATIVO" => "success",
        "EM_ANALISE" or "INCERTO" or "BLOQUEADO" => "warning",
        "REJEITADO" or "REMOVIDO" => "danger", "PENDENTE" or "NOVO" => "info", _ => "neutral"
    };
}
