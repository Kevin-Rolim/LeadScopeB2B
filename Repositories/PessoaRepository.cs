using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class PessoaRepository(SqlLeadScopeB2B sql) : IPessoaRepository
{
    internal const string Colunas = "Id,Nome,Email,Telefone,UrlLinkedIn,Status,Origem,DataColeta,DataBloqueio,DataExclusao";
    internal static Pessoas Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("Id"),
        Nome = r.Text("Nome")!,
        Email = r.Text("Email"),
        Telefone = r.Text("Telefone"),
        UrlLinkedIn = r.Text("UrlLinkedIn"),
        Status = r.Text("Status"),
        Origem = r.Text("Origem"),
        DataColeta = r.Date("DataColeta"),
        DataBloqueio = r.Date("DataBloqueio"),
        DataExclusao = r.Date("DataExclusao")
    };
    public Task<List<Pessoas>> ListarAtivasAsync(string? busca = null, string? status = null) => sql.ConsultarAsync($"""
        SELECT {Colunas} FROM dbo.Pessoas WHERE DataExclusao IS NULL
        AND (@busca IS NULL OR CHARINDEX(@busca,Nome)>0 OR CHARINDEX(@busca,Email)>0 OR CHARINDEX(@busca,Origem)>0)
        AND (@status IS NULL OR Status=@status) ORDER BY Nome,Id
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 30));
    public async Task<Pessoas?> ObterAtivaPorIdAsync(int id) => (await sql.ConsultarAsync($"SELECT {Colunas} FROM dbo.Pessoas WHERE Id=@id AND DataExclusao IS NULL", Mapear, Int("@id", id))).SingleOrDefault();
    private const string Duplicada = "SELECT Id FROM dbo.Pessoas WITH (UPDLOCK,HOLDLOCK) WHERE (@id IS NULL OR Id<>@id) AND ((@email IS NOT NULL AND LOWER(LTRIM(RTRIM(Email)))=@email) OR (@linkedin IS NOT NULL AND LOWER(UrlLinkedIn)=LOWER(@linkedin)))";
    public async Task<bool> DuplicadaAsync(string? email, string? linkedin, int? ignorarId = null) =>
        (await sql.ConsultarAsync(Duplicada, r => r.GetInt32(0), Text("@email", email, 150), Text("@linkedin", linkedin, 255), Int("@id", ignorarId))).Count > 0;
    private static SqlParameter[] Parametros(Pessoas p) => [Int("@id", p.Id), Text("@nome", p.Nome, 150), Text("@email", p.Email, 150), Text("@telefone", p.Telefone, 20), Text("@linkedin", p.UrlLinkedIn, 255), Text("@status", p.Status, 50), Text("@origem", p.Origem, 100), Date("@coleta", p.DataColeta), Date("@bloqueio", p.DataBloqueio), Date("@del", p.DataExclusao)];
    public async Task AdicionarAsync(Pessoas pessoa) => await SalvarAsync(pessoa, true);
    public async Task AtualizarAsync(Pessoas pessoa) => await SalvarAsync(pessoa, false);
    private async Task SalvarAsync(Pessoas p, bool novo)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicates = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Duplicada, r => r.GetInt32(0), Int("@id", novo ? null : p.Id), Text("@email", p.Email, 150), Text("@linkedin", p.UrlLinkedIn, 255));
            if (duplicates.Count > 0) throw new ConflitoException("Já existe uma pessoa com este e-mail ou perfil profissional, inclusive na base removida.");
            if (novo)
                p.Id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "INSERT INTO dbo.Pessoas (Nome,Email,Telefone,UrlLinkedIn,Status,Origem,DataColeta) OUTPUT INSERTED.Id VALUES (@nome,@email,@telefone,@linkedin,@status,@origem,@coleta)", r => r.GetInt32(0), Parametros(p))).Single();
            else if (await SqlLeadScopeB2B.ExecutarAsync(conn, tx, "UPDATE dbo.Pessoas SET Nome=@nome,Email=@email,Telefone=@telefone,UrlLinkedIn=@linkedin,Status=@status,Origem=@origem,DataBloqueio=@bloqueio,DataExclusao=@del WHERE Id=@id AND DataExclusao IS NULL", Parametros(p)) == 0)
                throw new KeyNotFoundException("Pessoa não encontrada.");
            return true;
        });
    }
}
