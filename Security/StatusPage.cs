using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
namespace LeadScopeB2B.Security;

public record StatusPageModel(int Code, string Title, string Message, string? Reference = null);
public static class StatusPage
{
    public static async Task RenderAsync(HttpContext context, int code, string? message = null)
    {
        var title = code switch { 400 => "Dados inválidos", 401 => "Entre para continuar", 403 => "Acesso negado", 404 => "Recurso não encontrado", 405 => "Operação não permitida", 409 => "Não foi possível salvar", 429 => "Muitas tentativas", 503 => "Serviço indisponível", _ => "Falha na operação" };
        message ??= code switch { 404 => "A página ou registro solicitado não está disponível. Confira o endereço ou volte ao início.", 429 => "Aguarde alguns minutos antes de tentar novamente.", 403 => "Seu perfil não permite esta operação.", _ => "Não foi possível concluir a operação. Tente novamente ou entre em contato com o administrador." };
        var result = new ViewResult { ViewName = "~/Views/Shared/Status.cshtml", StatusCode = code, ViewData = new ViewDataDictionary<StatusPageModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = new(code, title, message, code >= 500 ? context.TraceIdentifier : null) } };
        // Missing routes have no endpoint; MVC needs endpoint routing for URL generation.
        var endpoint = context.GetEndpoint();
        if (endpoint is null) context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "Status page"));
        try { await result.ExecuteResultAsync(new ActionContext(context, context.GetRouteData(), new ActionDescriptor())); }
        finally { context.SetEndpoint(endpoint); }
    }
}
