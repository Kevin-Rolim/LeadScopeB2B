using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class PessoaRepository(SqlLeadScopeB2B sql) : IPessoaRepository
{
    internal const string Colunas = "id,nome,email_profissional,telefone_comercial,url_perfil_profissional,status_validacao,origem,data_coleta,data_bloqueio,data_del";
    internal static Pessoas Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("id"),
        Nome = r.Text("nome")!,
        Email = r.Text("email_profissional"),
        Telefone = r.Text("telefone_comercial"),
        UrlLinkedin = r.Text("url_perfil_profissional"),
        Status = r.Text("status_validacao"),
        Origem = r.Text("origem"),
        DataColeta = r.Date("data_coleta")!.Value,
        DataBloqueio = r.Date("data_bloqueio"),
        DataDel = r.Date("data_del")
    };
    public Task<List<Pessoas>> ListarAtivasAsync(string? busca = null, string? status = null) => sql.ConsultarAsync($"""
        SELECT {Colunas} FROM dbo.Pessoas WHERE data_del IS NULL
        AND (@busca IS NULL OR CHARINDEX(@busca,nome)>0 OR CHARINDEX(@busca,email_profissional)>0 OR CHARINDEX(@busca,origem)>0)
        AND (@status IS NULL OR status_validacao=@status) ORDER BY nome,id
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 30));
    public async Task<Pessoas?> ObterAtivaPorIdAsync(int id) => (await sql.ConsultarAsync($"SELECT {Colunas} FROM dbo.Pessoas WHERE id=@id AND data_del IS NULL", Mapear, Int("@id", id))).SingleOrDefault();
    private const string Duplicada = "SELECT id FROM dbo.Pessoas WITH (UPDLOCK,HOLDLOCK) WHERE (@id IS NULL OR id<>@id) AND ((@email IS NOT NULL AND LOWER(LTRIM(RTRIM(email_profissional)))=@email) OR (@linkedin IS NOT NULL AND LOWER(url_perfil_profissional)=LOWER(@linkedin)))";
    public async Task<bool> DuplicadaAsync(string? email, string? linkedin, int? ignorarId = null) =>
        (await sql.ConsultarAsync(Duplicada, r => r.GetInt32(0), Text("@email", email, 150), Text("@linkedin", linkedin, 255), Int("@id", ignorarId))).Count > 0;
    private static SqlParameter[] Parametros(Pessoas p) => [Int("@id", p.Id), Text("@nome", p.Nome, 150), Text("@email", p.Email, 150), Text("@telefone", p.Telefone, 20), Text("@linkedin", p.UrlLinkedin, 255), Text("@status", p.Status, 30), Text("@origem", p.Origem, 100), Date("@coleta", p.DataColeta), Date("@bloqueio", p.DataBloqueio), Date("@del", p.DataDel)];
    public async Task AdicionarAsync(Pessoas pessoa) => await SalvarAsync(pessoa, true);
    public async Task AtualizarAsync(Pessoas pessoa) => await SalvarAsync(pessoa, false);
    private async Task SalvarAsync(Pessoas p, bool novo)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicates = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Duplicada, r => r.GetInt32(0), Int("@id", novo ? null : p.Id), Text("@email", p.Email, 150), Text("@linkedin", p.UrlLinkedin, 255));
            if (duplicates.Count > 0) throw new ConflitoException("Já existe uma pessoa com este e-mail ou perfil profissional, inclusive na base removida.");
            if (novo)
                p.Id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "INSERT INTO dbo.Pessoas (nome,email_profissional,telefone_comercial,url_perfil_profissional,status_validacao,origem,data_coleta) OUTPUT INSERTED.id VALUES (@nome,@email,@telefone,@linkedin,@status,@origem,@coleta)", r => r.GetInt32(0), Parametros(p))).Single();
            else if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Pessoas SET nome=@nome,email_profissional=@email,telefone_comercial=@telefone,url_perfil_profissional=@linkedin,status_validacao=@status,origem=@origem,data_bloqueio=@bloqueio,data_del=@del WHERE id=@id AND data_del IS NULL", Parametros(p)) == 0)
                throw new KeyNotFoundException("Pessoa não encontrada.");
            return true;
        });
    }
}
