using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class LeadRepository(SqlLeadScopeB2B sql, IPessoaRepository pessoas, IEmpresaRepository empresas) : ILeadRepository
{
    private const string Select = """
        SELECT l.*,p.nome AS pessoa_nome,e.razao_social,e.nome_fantasia FROM dbo.Pessoas_Empresas l
        JOIN dbo.Pessoas p ON p.id=l.pessoa_id JOIN dbo.Empresas e ON e.id=l.empresa_id
        WHERE p.data_del IS NULL AND e.status_validacao<>'REMOVIDA' AND ISNULL(l.status_lead,'')<>'REMOVIDO'
        """;
    private static PessoasEmpresas Mapear(SqlDataReader r) => new()
    {
        PessoaId = r.Int("pessoa_id"),
        EmpresaId = r.Int("empresa_id"),
        Cargo = r.Text("cargo"),
        Departamento = r.Text("departamento"),
        Senioridade = r.Text("senioridade"),
        StatusVinculo = r.Text("status_vinculo"),
        StatusLead = r.Text("status_lead"),
        NivelConfianca = r.Decimal("nivel_confianca"),
        FonteVinculo = r.Text("fonte_vinculo"),
        DataRevisao = r.Date("data_revisao"),
        DataVerif = r.Date("data_verificacao"),
        Pessoas = new Pessoas { Id = r.Int("pessoa_id"), Nome = r.Text("pessoa_nome")! },
        Empresas = new Empresas { Id = r.Int("empresa_id"), RazaoSocial = r.Text("razao_social")!, NomeFantasia = r.Text("nome_fantasia") }
    };
    public Task<List<PessoasEmpresas>> ListarAsync(string? busca = null, string? status = null, int? pessoaId = null, int? empresaId = null, string? cnae = null) => sql.ConsultarAsync(Select + """
        AND (@busca IS NULL OR CHARINDEX(@busca,p.nome)>0 OR CHARINDEX(@busca,e.razao_social)>0 OR CHARINDEX(@busca,e.nome_fantasia)>0 OR CHARINDEX(@busca,l.cargo)>0)
        AND (@status IS NULL OR l.status_lead=@status) AND (@pessoa IS NULL OR l.pessoa_id=@pessoa) AND (@empresa IS NULL OR l.empresa_id=@empresa)
        AND (@cnae IS NULL OR EXISTS(SELECT 1 FROM dbo.CNAEs_Empresas ce WHERE ce.empresa_id=e.id AND ce.cnae_numero=@cnae)) ORDER BY p.nome,e.razao_social
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 50), Int("@pessoa", pessoaId), Int("@empresa", empresaId), Text("@cnae", cnae, 20));
    public async Task<PessoasEmpresas?> ObterAsync(int pessoaId, int empresaId) => (await sql.ConsultarAsync(Select + " AND l.pessoa_id=@pessoa AND l.empresa_id=@empresa", Mapear, Int("@pessoa", pessoaId), Int("@empresa", empresaId))).SingleOrDefault();
    public async Task<bool> PessoaExisteAsync(int pessoaId) => (await pessoas.ObterAtivaPorIdAsync(pessoaId)) is { DataBloqueio: null };
    public async Task<bool> EmpresaExisteAsync(int empresaId) => (await empresas.ObterPorIdAsync(empresaId)) is { Status: not "INATIVA" };
    public async Task<List<Pessoas>> ListarPessoasAsync() => (await pessoas.ListarAtivasAsync()).Where(p => p.DataBloqueio is null).ToList();
    public async Task<List<Empresas>> ListarEmpresasAsync() => (await empresas.ListarAsync()).Where(e => e.Status != "INATIVA").ToList();
    private static SqlParameter[] Parametros(PessoasEmpresas l) => [Int("@pessoa", l.PessoaId), Int("@empresa", l.EmpresaId), Text("@cargo", l.Cargo, 100), Text("@departamento", l.Departamento, 100), Text("@senioridade", l.Senioridade, 50), Text("@vinculo", l.StatusVinculo, 50), Text("@status", l.StatusLead, 50), Decimal("@confianca", l.NivelConfianca), Text("@fonte", l.FonteVinculo, 100), Date("@verificacao", l.DataVerif)];
    public async Task AdicionarAsync(PessoasEmpresas lead)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, lead.PessoaId, lead.EmpresaId);
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.Pessoas_Empresas (pessoa_id,empresa_id,cargo,departamento,senioridade,status_vinculo,status_lead,nivel_confianca,fonte_vinculo,data_verificacao) VALUES (@pessoa,@empresa,@cargo,@departamento,@senioridade,@vinculo,@status,@confianca,@fonte,@verificacao)", Parametros(lead));
            return true;
        });
    }
    internal static async Task ValidarDestinosAsync(SqlConnection conn, SqlTransaction tx, int pessoaId, int empresaId)
    {
        var valid = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT p.id FROM dbo.Pessoas p WITH (UPDLOCK,HOLDLOCK) CROSS JOIN dbo.Empresas e WITH (UPDLOCK,HOLDLOCK) WHERE p.id=@pessoa AND e.id=@empresa AND p.data_del IS NULL AND p.data_bloqueio IS NULL AND e.status_validacao NOT IN ('REMOVIDA','INATIVA')", r => r.GetInt32(0), Int("@pessoa", pessoaId), Int("@empresa", empresaId));
        if (valid.Count == 0) throw new ConflitoException("A pessoa ou empresa está indisponível, bloqueada ou removida.");
    }
    public async Task AtualizarAsync(PessoasEmpresas lead)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, lead.PessoaId, lead.EmpresaId);
            if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Pessoas_Empresas SET cargo=@cargo,departamento=@departamento,senioridade=@senioridade,status_vinculo=@vinculo,status_lead=@status,nivel_confianca=@confianca,fonte_vinculo=@fonte,data_verificacao=@verificacao,data_revisao=NULL WHERE pessoa_id=@pessoa AND empresa_id=@empresa AND ISNULL(status_lead,'')<>'REMOVIDO'", Parametros(lead)) == 0) throw new KeyNotFoundException("Lead não encontrado.");
            return true;
        });
    }
    public async Task ExcluirAsync(int pessoaId, int empresaId)
    {
        if (await sql.ExecutarAsync("UPDATE dbo.Pessoas_Empresas SET status_lead='REMOVIDO',status_vinculo='INATIVO',data_verificacao=SYSUTCDATETIME() WHERE pessoa_id=@pessoa AND empresa_id=@empresa AND ISNULL(status_lead,'')<>'REMOVIDO'", Int("@pessoa", pessoaId), Int("@empresa", empresaId)) == 0) throw new KeyNotFoundException("Lead não encontrado.");
    }
    public async Task RevisarAsync(int pessoaId, int empresaId, int usuarioId, string decisao, string? comentario)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            await ValidarDestinosAsync(conn, tx, pessoaId, empresaId);
            if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Pessoas_Empresas SET status_lead=@decisao,data_revisao=SYSUTCDATETIME(),data_verificacao=SYSUTCDATETIME() WHERE pessoa_id=@pessoa AND empresa_id=@empresa AND ISNULL(status_lead,'')<>'REMOVIDO'", Int("@pessoa", pessoaId), Int("@empresa", empresaId), Text("@decisao", decisao, 50)) == 0) throw new KeyNotFoundException("Lead não encontrado.");
            await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "INSERT INTO dbo.Revisoes (tipo_revisao,decisao,comentario,usuario_id,pessoa_id,empresa_id) VALUES ('LEAD',@decisao,@comentario,@usuario,@pessoa,@empresa)", Text("@decisao", decisao, 100), Text("@comentario", comentario), Int("@usuario", usuarioId), Int("@pessoa", pessoaId), Int("@empresa", empresaId));
            await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "REVISAO_LEAD", $"Lead {pessoaId}/{empresaId}: {decisao}");
            return true;
        });
    }
    public Task<List<Revisoes>> ListarRevisoesAsync(int pessoaId, int empresaId) => sql.ConsultarAsync("SELECT r.*,u.nome AS usuario_nome FROM dbo.Revisoes r JOIN dbo.Usuarios u ON u.id=r.usuario_id WHERE r.pessoa_id=@pessoa AND r.empresa_id=@empresa ORDER BY r.data_hora DESC,r.id DESC", r => new Revisoes { Id = r.Int("id"), PessoaId = r.Int("pessoa_id"), EmpresaId = r.Int("empresa_id"), UsuarioId = r.Int("usuario_id"), Decisao = r.Text("decisao"), Comentario = r.Text("comentario"), DataHora = r.Date("data_hora")!.Value, Usuarios = new Usuarios { Id = r.Int("usuario_id"), Nome = r.Text("usuario_nome")! } }, Int("@pessoa", pessoaId), Int("@empresa", empresaId));
}
