using LeadScopeB2B.Models;
namespace LeadScopeB2B.Integration;

public sealed record RevisaoColeta(DadosColetados Dado, string? ValorAtual, bool Revisada);
