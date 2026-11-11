using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class LeadRepository(SqlLeadScopeB2B sql, IPessoaRepository pessoas, IEmpresaRepository empresas) : ILeadRepository
{
    private const string Select = """
        SELECT l.PessoaId,l.EmpresaId,l.PessoaNome,l.RazaoSocial,l.NomeFantasia,l.Cargo,l.Departamento,
               l.Senioridade,l.StatusVinculo,l.StatusLead,l.DataRevisao,l.NivelConfianca,l.FonteVinculo,l.DataVerificacao
        FROM dbo.vw_LeadsCadastro AS l
        """;
    private static PessoasEmpresas Mapear(SqlDataReader r) => new()
    {
        PessoaId = r.Int("PessoaId"),
        EmpresaId = r.Int("EmpresaId"),
        Cargo = r.Text("Cargo"),
        Departamento = r.Text("Departamento"),
        Senioridade = r.Text("Senioridade"),
        StatusVinculo = r.Text("StatusVinculo"),
        StatusLead = r.Text("StatusLead"),
        NivelConfianca = r.Decimal("NivelConfianca"),
        FonteVinculo = r.Text("FonteVinculo"),
        DataRevisao = r.Date("DataRevisao"),
        DataVerificacao = r.Date("DataVerificacao"),
        Pessoas = new Pessoas { Id = r.Int("PessoaId"), Nome = r.Text("PessoaNome")! },
        Empresas = new Empresas { Id = r.Int("EmpresaId"), RazaoSocial = r.Text("RazaoSocial")!, NomeFantasia = r.Text("NomeFantasia") }
    };
    public Task<List<PessoasEmpresas>> ListarAsync(string? busca = null, string? status = null, int? pessoaId = null, int? empresaId = null, string? cnae = null) => sql.ConsultarAsync(Select + " " + """
        WHERE (@busca IS NULL OR CHARINDEX(@busca,l.PessoaNome)>0 OR CHARINDEX(@busca,l.RazaoSocial)>0 OR CHARINDEX(@busca,l.NomeFantasia)>0 OR CHARINDEX(@busca,l.Cargo)>0)
        AND (@status IS NULL OR l.StatusLead=@status) AND (@pessoa IS NULL OR l.PessoaId=@pessoa) AND (@empresa IS NULL OR l.EmpresaId=@empresa)
        AND (@cnae IS NULL OR EXISTS(SELECT 1 FROM dbo.vw_CnaesEmpresas ce WHERE ce.EmpresaId=l.EmpresaId AND ce.CnaeNumero=@cnae)) ORDER BY l.PessoaNome,l.RazaoSocial
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 50), Int("@pessoa", pessoaId), Int("@empresa", empresaId), Text("@cnae", cnae, 20));
    public async Task<PessoasEmpresas?> ObterAsync(int pessoaId, int empresaId) => (await sql.ConsultarAsync(Select + " WHERE l.PessoaId=@pessoa AND l.EmpresaId=@empresa", Mapear, Int("@pessoa", pessoaId), Int("@empresa", empresaId))).SingleOrDefault();
    public async Task<bool> PessoaExisteAsync(int pessoaId) => (await pessoas.ObterAtivaPorIdAsync(pessoaId)) is { DataBloqueio: null };
    public async Task<bool> EmpresaExisteAsync(int empresaId) => (await empresas.ObterPorIdAsync(empresaId)) is { Status: not "INATIVA" };
    public async Task<List<Pessoas>> ListarPessoasAsync() => (await pessoas.ListarAtivasAsync()).Where(p => p.DataBloqueio is null).ToList();
    public async Task<List<Empresas>> ListarEmpresasAsync() => (await empresas.ListarAsync()).Where(e => e.Status != "INATIVA").ToList();
    private static SqlParameter[] Parametros(PessoasEmpresas l) =>
    [
        Int("@PessoaId", l.PessoaId), Int("@EmpresaId", l.EmpresaId), Text("@Cargo", l.Cargo, 100),
        Text("@Departamento", l.Departamento, 100), Text("@Senioridade", l.Senioridade, 50),
        Text("@StatusVinculo", l.StatusVinculo, 50), Text("@StatusLead", l.StatusLead, 50),
        Decimal("@NivelConfianca", l.NivelConfianca), Text("@FonteVinculo", l.FonteVinculo, 100),
        Date("@DataVerificacao", l.DataVerificacao)
    ];
    public async Task AdicionarAsync(PessoasEmpresas lead)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, lead.PessoaId, lead.EmpresaId);
            await SqlLeadScopeB2B.ExecutarProcedureAsync(conn, tx, "dbo.usp_PessoasEmpresasAdd", Parametros(lead));
            return true;
        });
    }
    internal static async Task ValidarDestinosAsync(SqlConnection conn, SqlTransaction tx, int pessoaId, int empresaId)
    {
        var valid = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT p.Id FROM dbo.vw_Pessoas p WITH (UPDLOCK,HOLDLOCK) CROSS JOIN dbo.vw_Empresas e WITH (UPDLOCK,HOLDLOCK) WHERE p.Id=@pessoa AND e.Id=@empresa AND p.DataExclusao IS NULL AND p.DataBloqueio IS NULL AND ISNULL(e.Status,'') NOT IN ('REMOVIDA','INATIVA')", r => r.GetInt32(0), Int("@pessoa", pessoaId), Int("@empresa", empresaId));
        if (valid.Count == 0) throw new ConflitoException("A pessoa ou empresa está indisponível, bloqueada ou removida.");
    }
    public async Task AtualizarAsync(PessoasEmpresas lead)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, lead.PessoaId, lead.EmpresaId);
            var linhas = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_PessoasEmpresasAlt", "@Linhas",
                [..Parametros(lead), Bit("@SomenteAtivo", true)]);
            if (linhas == 0) throw new KeyNotFoundException("Lead não encontrado.");
            return true;
        });
    }
    public async Task ExcluirAsync(int pessoaId, int empresaId)
    {
        var linhas = await sql.ExecutarProcedureComSaidaAsync("dbo.usp_PessoasEmpresasExcluir", "@Linhas",
            Int("@PessoaId", pessoaId), Int("@EmpresaId", empresaId));
        if (linhas == 0) throw new KeyNotFoundException("Lead não encontrado.");
    }
    public async Task RevisarAsync(int pessoaId, int empresaId, int usuarioId, string decisao, string? comentario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var autorizado = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
                "SELECT UsuarioId FROM dbo.vw_UsuariosPerfis WHERE UsuarioId=@id AND ConjuntoPermissoes IN (N'Administrador',N'Revisor')",
                r => r.GetInt32(0), Int("@id", usuarioId));
            if (autorizado.Count == 0) throw new UnauthorizedAccessException("Revisão não autorizada.");
            await ValidarDestinosAsync(conn, tx, pessoaId, empresaId);
            var linhas = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_PessoasEmpresasRevisar", "@Linhas",
                Int("@PessoaId", pessoaId), Int("@EmpresaId", empresaId), Text("@Decisao", decisao, 50));
            if (linhas == 0) throw new KeyNotFoundException("Lead não encontrado.");
            await SqlLeadScopeB2B.ExecutarProcedureAsync(conn, tx, "dbo.usp_RevisoesAdd",
                Text("@Decisao", decisao, 100), Text("@Comentario", comentario), Int("@UsuarioId", usuarioId),
                Int("@PessoaId", pessoaId), Int("@EmpresaId", empresaId));
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "REVISAO_LEAD", $"Lead {pessoaId}/{empresaId}: {decisao}");
            return true;
        });
    }
    public Task<List<Revisoes>> ListarRevisoesAsync(int pessoaId, int empresaId) => sql.ConsultarAsync("SELECT RevisaoId AS Id,PessoaId,EmpresaId,RevisorId AS UsuarioId,Decisao,Comentario,DataHora,Revisor AS UsuarioNome FROM dbo.vw_RevisoesLeads WHERE PessoaId=@pessoa AND EmpresaId=@empresa ORDER BY DataHora DESC,RevisaoId DESC", r => new Revisoes { Id = r.Int("Id"), PessoaId = r.Int("PessoaId"), EmpresaId = r.Int("EmpresaId"), UsuarioId = r.Int("UsuarioId"), Decisao = r.Text("Decisao"), Comentario = r.Text("Comentario"), DataHora = r.Date("DataHora"), Usuarios = new Usuarios { Id = r.Int("UsuarioId"), Nome = r.Text("UsuarioNome")! } }, Int("@pessoa", pessoaId), Int("@empresa", empresaId));
}
