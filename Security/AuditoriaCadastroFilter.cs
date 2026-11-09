using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace LeadScopeB2B.Security;

// Registra mutações MVC concluídas. Revisão, exportação e conta possuem eventos próprios.
public sealed class AuditoriaCadastroFilter(AuditoriaService auditoria) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var result = await next();
        var controller = context.ActionDescriptor.RouteValues["controller"];
        if (HttpMethods.IsPost(context.HttpContext.Request.Method) && controller is "Empresas" or "Pessoas" or "Leads" or "Usuarios"
            && result.Exception is null && result.Result is RedirectToActionResult && context.HttpContext.User.Identity?.IsAuthenticated == true)
        {
            var action = context.ActionDescriptor.RouteValues["action"];
            if (action != "Revisar")
                await auditoria.RegistrarAsync(context.HttpContext.User.UsuarioId(), "CADASTRO_" + action?.ToUpperInvariant(), $"{controller}: operação concluída.");
        }
    }
}
