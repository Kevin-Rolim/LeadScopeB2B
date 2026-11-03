using LeadScopeB2B.Integration.Universal;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.ViewModels.Empresas;
namespace LeadScopeB2B.Services;

// Liga o cadastro ao executor existente; não cria transporte, credenciais ou agendador próprios.
public sealed class EnriquecimentoEmpresaService(IntegracaoArquivoRepository arquivos, IntegracaoRepository dados, IExecutorApi executor)
{
    public async Task<EnriquecimentoEmpresaViewModel> ObterAsync(int empresaId, CancellationToken ct)
    {
        var destino = await dados.ObterDestinoIntegracaoAsync(empresaId, null, ct);
        var model = new EnriquecimentoEmpresaViewModel { Evidencias = await dados.EvidenciasEmpresaAsync(empresaId, ct) };
        if (destino is null) { model.Estado = "Empresa indisponível para enriquecimento"; return model; }
        var state = await arquivos.LerAsync(ct);
        foreach (var api in state.Integracoes.Where(a=>a.Habilitada && a.Contrato.Plano.UsoAutorizado))
        foreach (var op in api.Contrato.Operacoes.Where(o=>o.Habilitada && o.Revisada && o.Incompatibilidade is null && o.ConsultaCota is null && !ValidacaoContrato.Escrita(o) && o.Mapeamentos.Count>0 && o.Mapeamentos.All(m=>m.Destino.StartsWith("Empresa.", StringComparison.Ordinal))))
        {
            string? bloqueio = null;
            if (op.Paginacao.Tipo != "none") bloqueio = "Operação paginada não permite associar evidências a uma única empresa.";
            if (op.Parametros.Any(p=>p.CampoOrigem.StartsWith("Pessoa.",StringComparison.Ordinal))) bloqueio = "Operação exige dados de pessoa.";
            foreach (var p in op.Parametros)
            {
                var value = p.CampoOrigem != "" ? destino.Campos.GetValueOrDefault(p.CampoOrigem) : op.Rotina.Parametros.GetValueOrDefault(p.Nome) ?? p.Padrao;
                if (string.IsNullOrWhiteSpace(value) && p.Obrigatorio) { bloqueio = $"Parâmetro necessário indisponível no cadastro: {p.Nome}. Configure uma origem ou valor fixo na operação."; break; }
                if (string.IsNullOrWhiteSpace(value)) continue;
                try { ValidacaoContrato.ValidarParametro(p,value); }
                catch (Exception ex) when (ex is IntegracaoException or System.ComponentModel.DataAnnotations.ValidationException or System.Text.Json.JsonException) { bloqueio = $"Parâmetro inválido: {p.Nome}. Confira o cadastro e o contrato."; break; }
            }
            if (api.FonteDadosId<=0 || !await dados.FonteIntegracaoAtivaAsync(api.FonteDadosId,ct)) bloqueio="Fonte de evidências não configurada ou inativa. Reconecte a integração.";
            var unknown = op.Custo.PorRequisicao is null || op.Custo.PorItem is null;
            model.Operacoes.Add(new(api.Id, op.Id, api.Contrato.Provedor+" · "+op.Nome,unknown,!unknown && (op.Custo.PorRequisicao>0 || op.Custo.PorItem>0),bloqueio));
        }
        model.UltimaExecucao = state.Historico.Where(h=>h.EmpresaId==empresaId).OrderByDescending(h=>h.Data).FirstOrDefault();
        var last = model.UltimaExecucao;
        model.Estado = last is { Codigo: not ("CONCLUIDA" or "SEM_NOVIDADES") } ? EstadoErro(last.Codigo)
            : model.Evidencias.Any(e=>e.Estado=="Pendente") ? "Consulta concluída com evidências pendentes"
            : model.Evidencias.Any(e=>e.Estado=="Aprovada") ? "Dados aprovados e aplicados"
            : last?.Codigo=="SEM_NOVIDADES" ? "Nenhuma informação nova encontrada"
            : model.Operacoes.Count==0 ? "Integração não configurada para enriquecimento" : "Ainda não consultada";
        return model;
    }
    public async Task<ResultadoApi> EnriquecerAsync(int empresaId, Guid integracaoId, string operacaoId, bool autorizarConsumo, int usuarioId, CancellationToken ct)
    {
        if (!await dados.UsuarioPodeIntegrarAsync(usuarioId, ct)) throw new IntegracaoException("SEM_PERMISSAO","Somente administradores podem executar consultas externas.");
        async Task<ResultadoApi> FalhaAsync(string codigo, string mensagem)
        {
            var result=new ResultadoApi{Codigo=codigo,Mensagem=mensagem};
            await arquivos.AlterarAsync(s=>{s.Historico.Add(new(){Id=result.ExecucaoId,EmpresaId=empresaId,IntegracaoId=integracaoId,OperacaoId=operacaoId,UsuarioId=usuarioId,Codigo=codigo});if(s.Historico.Count>2000)s.Historico.RemoveRange(0,s.Historico.Count-2000);return true;},ct);
            return result;
        }
        var model=await ObterAsync(empresaId,ct);
        var choice=model.Operacoes.SingleOrDefault(o=>o.IntegracaoId==integracaoId && o.OperacaoId==operacaoId);
        if (choice is null) return await FalhaAsync("DESABILITADA", "Nenhuma operação habilitada e compatível está disponível. Confira a integração.");
        if (choice.Bloqueio is not null) return await FalhaAsync("PARAMETRO_INVALIDO",choice.Bloqueio);
        var api=(await arquivos.LerAsync(ct)).Integracoes.Single(a=>a.Id==integracaoId);
        var op=api.Contrato.Operacoes.Single(o=>o.Id==operacaoId);
        return await executor.ExecutarAsync(new(){EmpresaId=empresaId,IntegracaoId=integracaoId,OperacaoId=operacaoId,UsuarioId=usuarioId,
            PersistirEvidencias=true,AutorizarCusto=autorizarConsumo,AutorizarCustoDesconhecido=autorizarConsumo,Parametros=new(op.Rotina.Parametros)},ct);
    }
    private static string EstadoErro(string code)=>code switch {
        "RATE_LIMIT" or "RETRY_AFTER" or "COTA_LOCAL" or "COTA_PROVEDOR" or "JANELA_PROVEDOR" or "LIMITE_DOCUMENTADO"=>"Consulta bloqueada por limite ou cota",
        "CREDENCIAL_INVALIDA" or "CREDENCIAL" or "TOKEN_EXPIRADO_OU_INVALIDO"=>"Erro de autenticação",
        "REDE" or "TIMEOUT" or "TIMEOUT_DNS" or "DNS" or "PROVEDOR_INDISPONIVEL"=>"Erro de comunicação",
        "SEM_DADOS_UTEIS"=>"Resposta sem informações úteis para revisão",
        _=>"Consulta não concluída: "+code
    };
}
