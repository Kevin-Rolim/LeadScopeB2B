using System.ComponentModel.DataAnnotations;
using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace LeadScopeB2B.Controllers;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true), RequestSizeLimit(32768)]
public class ContaController(ContaService service, AuditoriaService auditoria, RecuperacaoAcessoService recuperacao) : Controller
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        base.OnActionExecuting(context);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var usuario = await service.AutenticarAsync(model.Email, model.Senha);
        if (usuario is null) { ModelState.AddModelError("", "E-mail ou senha inválidos, ou conta indisponível."); return View(model); }
        await auditoria.RegistrarAsync(usuario.Id, "LOGIN", "Acesso à interface.");
        await EntrarAsync(usuario);
        var destino = Acesso.Papel(usuario) == "ADMIN" ? Url.Action("Index", "Usuarios") : Acesso.Papel(usuario) == "OPERACAO" ? Url.Action("Index", "Leads") : Url.Action("Index", "Empresas");
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : destino!);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Cadastro() => User.Identity?.IsAuthenticated == true ? RedirectToAction(nameof(Perfil)) : View(new CadastroViewModel());

    [AllowAnonymous, HttpPost, EnableRateLimiting("cadastro")]
    public async Task<IActionResult> Cadastro(CadastroViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction(nameof(Perfil));
        if (!ModelState.IsValid) return View(model);
        try
        {
            await service.CadastrarAsync(model);
            TempData["Sucesso"] = "Cadastro realizado. Entre com seu e-mail e senha.";
            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex) when (ex is ValidationException or ConflitoException)
        {
            ModelState.AddModelError("", ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Perfil() => View(await service.ObterPerfilAsync(User.UsuarioId()));

    [HttpPost, EnableRateLimiting("conta")]
    public async Task<IActionResult> Perfil(PerfilViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var usuario = await service.AtualizarPerfilAsync(User.UsuarioId(), model);
                await EntrarAsync(usuario);
                TempData["Sucesso"] = "Perfil atualizado. Suas outras sessões foram encerradas.";
                return RedirectToAction(nameof(Perfil));
            }
            catch (Exception ex) when (ex is ValidationException or ConflitoException) { ModelState.AddModelError("", ex.Message); }
        }
        model.PerfilAcesso = (await service.ObterPerfilAsync(User.UsuarioId())).PerfilAcesso;
        return View(model);
    }

    [HttpGet]
    public IActionResult AlterarSenha() => View(new AlterarSenhaViewModel());

    [HttpPost, EnableRateLimiting("conta")]
    public async Task<IActionResult> AlterarSenha(AlterarSenhaViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        try
        {
            await service.AlterarSenhaAsync(User.UsuarioId(), model);
            await HttpContext.SignOutAsync();
            TempData["Sucesso"] = "Senha alterada. Entre novamente com a nova senha.";
            return RedirectToAction(nameof(Login));
        }
        catch (Exception ex) when (ex is ValidationException or ConflitoException) { ModelState.AddModelError("", ex.Message); return View(model); }
    }

    [AllowAnonymous, HttpGet]
    public IActionResult RecuperarAcesso()
    {
        ViewData["RecuperacaoDisponivel"] = recuperacao.Disponivel;
        return View(new RecuperarAcessoViewModel());
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("recuperacao")]
    public IActionResult RecuperarAcesso(RecuperarAcessoViewModel model)
    {
        ViewData["RecuperacaoDisponivel"] = recuperacao.Disponivel;
        if (!recuperacao.Disponivel) { Response.StatusCode = 503; return View(model); }
        if (!ModelState.IsValid) return View(model);
        try { recuperacao.Solicitar(model); return RedirectToAction(nameof(RecuperacaoSolicitada)); }
        catch (Exception ex) when (ex is ValidationException or ConflitoException) { ModelState.AddModelError("", ex.Message); return View(model); }
    }

    [AllowAnonymous, HttpGet]
    public IActionResult RecuperacaoSolicitada() => View();

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> RedefinirSenha(string? token)
    {
        ViewData["TokenValido"] = await recuperacao.TokenValidoAsync(token ?? "");
        return View(new RedefinirSenhaViewModel { Token = token is { Length: <= 4096 } ? token : "" });
    }

    [AllowAnonymous, HttpPost, EnableRateLimiting("redefinicao")]
    public async Task<IActionResult> RedefinirSenha(RedefinirSenhaViewModel model)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await recuperacao.RedefinirAsync(model);
                await HttpContext.SignOutAsync();
                TempData["Sucesso"] = "Senha redefinida. Entre com sua nova senha.";
                return RedirectToAction(nameof(Login));
            }
            catch (ValidationException ex) { ModelState.AddModelError("", ex.Message); }
        }
        ViewData["TokenValido"] = await recuperacao.TokenValidoAsync(model.Token);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Negado() { Response.StatusCode = 403; return View(); }

    private Task EntrarAsync(Usuarios usuario) => HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
        Acesso.Principal(usuario, CookieAuthenticationDefaults.AuthenticationScheme));
}
