using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class LeadRepository(SqlLeadScopeB2B sql, IPessoaRepository pessoas, IEmpresaRepository empresas) : ILeadRepository
{
    private const string Select = """
        SELECT l.*,p.Nome AS PessoaNome,e.RazaoSocial,e.NomeFantasia FROM dbo.PessoasEmpresas l
        JOIN dbo.Pessoas p ON p.Id=l.PessoaId JOIN dbo.Empresas e ON e.Id=l.EmpresaId
        WHERE p.DataExclusao IS NULL AND ISNULL(e.Status,'')<>'REMOVIDA' AND ISNULL(l.StatusLead,'')<>'REMOVIDO'
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
    public Task<List<PessoasEmpresas>> ListarAsync(string? busca = null, string? status = null, int? pessoaId = null, int? empresaId = null, string? cnae = null) => sql.ConsultarAsync(Select + """
        AND (@busca IS NULL OR CHARINDEX(@busca,p.Nome)>0 OR CHARINDEX(@busca,e.RazaoSocial)>0 OR CHARINDEX(@busca,e.NomeFantasia)>0 OR CHARINDEX(@busca,l.Cargo)>0)
        AND (@status IS NULL OR l.StatusLead=@status) AND (@pessoa IS NULL OR l.PessoaId=@pessoa) AND (@empresa IS NULL OR l.EmpresaId=@empresa)
        AND (@cnae IS NULL OR EXISTS(SELECT 1 FROM dbo.CnaesEmpresas ce WHERE ce.EmpresaId=e.Id AND ce.CnaeNumero=@cnae)) ORDER BY p.Nome,e.RazaoSocial
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 50), Int("@pessoa", pessoaId), Int("@empresa", empresaId), Text("@cnae", cnae, 20));
    public async Task<PessoasEmpresas?> ObterAsync(int pessoaId, int empresaId) => (await sql.ConsultarAsync(Select + " AND l.PessoaId=@pessoa AND l.EmpresaId=@empresa", Mapear, Int("@pessoa", pessoaId), Int("@empresa", empresaId))).SingleOrDefault();
    public async Task<bool> PessoaExisteAsync(int pessoaId) => (await pessoas.ObterAtivaPorIdAsync(pessoaId)) is { DataBloqueio: null };
    public async Task<bool> EmpresaExisteAsync(int empresaId) => (await empresas.ObterPorIdAsync(empresaId)) is { Status: not "INATIVA" };
    public async Task<List<Pessoas>> ListarPessoasAsync() => (await pessoas.ListarAtivasAsync()).Where(p => p.DataBloqueio is null).ToList();
    public async Task<List<Empresas>> ListarEmpresasAsync() => (await empresas.ListarAsync()).Where(e => e.Status != "INATIVA").ToList();
    private static SqlParameter[] Parametros(PessoasEmpresas l) => [Int("@pessoa", l.PessoaId), Int("@empresa", l.EmpresaId), Text("@cargo", l.Cargo, 100), Text("@departamento", l.Departamento, 100), Text("@senioridade", l.Senioridade, 50), Text("@vinculo", l.StatusVinculo, 50), Text("@status", l.StatusLead, 50), Decimal("@confianca", l.NivelConfianca), Text("@fonte", l.FonteVinculo, 100), Date("@verificacao", l.DataVerificacao)];
    public async Task AdicionarAsync(PessoasEmpresas lead)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, lead.PessoaId, lead.EmpresaId);
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.PessoasEmpresas (PessoaId,EmpresaId,Cargo,Departamento,Senioridade,StatusVinculo,StatusLead,NivelConfianca,FonteVinculo,DataVerificacao) VALUES (@pessoa,@empresa,@cargo,@departamento,@senioridade,@vinculo,@status,@confianca,@fonte,@verificacao)", Parametros(lead));
            return true;
        });
    }
    internal static async Task ValidarDestinosAsync(SqlConnection conn, SqlTransaction tx, int pessoaId, int empresaId)
    {
        var valid = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT p.Id FROM dbo.Pessoas p WITH (UPDLOCK,HOLDLOCK) CROSS JOIN dbo.Empresas e WITH (UPDLOCK,HOLDLOCK) WHERE p.Id=@pessoa AND e.Id=@empresa AND p.DataExclusao IS NULL AND p.DataBloqueio IS NULL AND ISNULL(e.Status,'') NOT IN ('REMOVIDA','INATIVA')", r => r.GetInt32(0), Int("@pessoa", pessoaId), Int("@empresa", empresaId));
        if (valid.Count == 0) throw new ConflitoException("A pessoa ou empresa está indisponível, bloqueada ou removida.");
    }
    public async Task AtualizarAsync(PessoasEmpresas lead)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, lead.PessoaId, lead.EmpresaId);
            if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.PessoasEmpresas SET Cargo=@cargo,Departamento=@departamento,Senioridade=@senioridade,StatusVinculo=@vinculo,StatusLead=@status,NivelConfianca=@confianca,FonteVinculo=@fonte,DataVerificacao=@verificacao,DataRevisao=NULL WHERE PessoaId=@pessoa AND EmpresaId=@empresa AND ISNULL(StatusLead,'')<>'REMOVIDO'", Parametros(lead)) == 0) throw new KeyNotFoundException("Lead não encontrado.");
            return true;
        });
    }
    public async Task ExcluirAsync(int pessoaId, int empresaId)
    {
        if (await sql.ExecutarAsync("UPDATE dbo.PessoasEmpresas SET StatusLead='REMOVIDO',StatusVinculo='INATIVO',DataVerificacao=SYSUTCDATETIME() WHERE PessoaId=@pessoa AND EmpresaId=@empresa AND ISNULL(StatusLead,'')<>'REMOVIDO'", Int("@pessoa", pessoaId), Int("@empresa", empresaId)) == 0) throw new KeyNotFoundException("Lead não encontrado.");
    }
    public async Task RevisarAsync(int pessoaId, int empresaId, int usuarioId, string decisao, string? comentario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, pessoaId, empresaId);
            if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.PessoasEmpresas SET StatusLead=@decisao,DataRevisao=SYSUTCDATETIME(),DataVerificacao=SYSUTCDATETIME() WHERE PessoaId=@pessoa AND EmpresaId=@empresa AND ISNULL(StatusLead,'')<>'REMOVIDO'", Int("@pessoa", pessoaId), Int("@empresa", empresaId), Text("@decisao", decisao, 50)) == 0) throw new KeyNotFoundException("Lead não encontrado.");
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.Revisoes (Decisao,Comentario,UsuarioId,PessoaId,EmpresaId) VALUES (@decisao,@comentario,@usuario,@pessoa,@empresa)", Text("@decisao", decisao, 100), Text("@comentario", comentario), Int("@usuario", usuarioId), Int("@pessoa", pessoaId), Int("@empresa", empresaId));
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "REVISAO_LEAD", $"Lead {pessoaId}/{empresaId}: {decisao}");
            return true;
        });
    }
    public Task<List<Revisoes>> ListarRevisoesAsync(int pessoaId, int empresaId) => sql.ConsultarAsync("SELECT r.*,u.Nome AS UsuarioNome FROM dbo.Revisoes r JOIN dbo.Usuarios u ON u.Id=r.UsuarioId WHERE r.PessoaId=@pessoa AND r.EmpresaId=@empresa ORDER BY r.DataHora DESC,r.Id DESC", r => new Revisoes { Id = r.Int("Id"), PessoaId = r.Int("PessoaId"), EmpresaId = r.Int("EmpresaId"), UsuarioId = r.Int("UsuarioId"), Decisao = r.Text("Decisao"), Comentario = r.Text("Comentario"), DataHora = r.Date("DataHora"), Usuarios = new Usuarios { Id = r.Int("UsuarioId"), Nome = r.Text("UsuarioNome")! } }, Int("@pessoa", pessoaId), Int("@empresa", empresaId));
}
