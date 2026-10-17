using System.ComponentModel.DataAnnotations;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace LeadScopeB2B.Security;

internal static class Erros
{
    public static (int Status, string Mensagem) Classificar(Exception ex) => ex switch
    {
        ValidationException => (400, ex.Message),
        KeyNotFoundException => (404, ex.Message),
        ConflitoException => (409, ex.Message),
        SqlException { Number: 2601 or 2627 } => (409, "Já existe um registro com estes identificadores. Verifique os registros removidos também."),
        SqlException { Number: 547 } => (409, "A operação conflita com os relacionamentos ou restrições do cadastro."),
        SqlException => (503, "O banco de dados está temporariamente indisponível. Tente novamente."),
        _ => (500, "Não foi possível concluir a operação.")
    };
}

public class ErroHandler(ILogger<ErroHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, message) = Erros.Classificar(exception);
        if (status >= 500) logger.LogError(exception, "Falha na requisição {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        if (context.Request.Path.StartsWithSegments("/api"))
            await Results.Problem(statusCode: status, title: message, extensions: new Dictionary<string, object?> { { "traceId", context.TraceIdentifier } }).ExecuteAsync(context);
        else
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync($"<!doctype html><html lang='pt-BR'><meta charset='utf-8'><title>LeadScopeB2B</title><h1>{System.Net.WebUtility.HtmlEncode(message)}</h1><p>Referência: {System.Net.WebUtility.HtmlEncode(context.TraceIdentifier)}</p><a href='/'>Voltar ao início</a></html>", ct);
        }
        return true;
    }
}

// Erros esperados de cadastro retornam ao formulário com as opções de seleção restauradas.
public class ErroFormularioFilter(LeadService leads, UsuarioService usuarios) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is null || context.Controller is not Controller controller || context.HttpContext.Request.Path.StartsWithSegments("/api")) return;
        var (status, message) = Erros.Classificar(executed.Exception);
        if (status is not (400 or 409)) return;
        var model = context.ActionArguments.Values.FirstOrDefault(v => v is EmpresaFormViewModel or PessoaFormViewModel or LeadFormViewModel or UsuarioCreateViewModel or UsuarioEditViewModel);
        if (model is null)
        {
            controller.TempData["Erro"] = message;
            var destino = controller.Url.Action("Index", context.ActionDescriptor.RouteValues["controller"]) ?? "/";
            var referer = context.HttpContext.Request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Authority == context.HttpContext.Request.Host.Value && controller.Url.IsLocalUrl(uri.PathAndQuery)) destino = uri.PathAndQuery;
            executed.Result = controller.LocalRedirect(destino);
            executed.ExceptionHandled = true;
            return;
        }
        if (model is LeadFormViewModel lead) await leads.PrepararFormularioAsync(lead);
        if (model is UsuarioCreateViewModel create) await usuarios.PrepararFormularioAsync(create);
        if (model is UsuarioEditViewModel edit) await usuarios.PrepararFormularioAsync(edit);
        controller.ModelState.AddModelError("", message);
        executed.Result = controller.View(context.ActionDescriptor.RouteValues["action"], model);
        executed.ExceptionHandled = true;
    }
}
