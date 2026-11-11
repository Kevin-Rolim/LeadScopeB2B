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
        SELECT {Colunas} FROM dbo.vw_Pessoas WHERE DataExclusao IS NULL
        AND (@busca IS NULL OR CHARINDEX(@busca,Nome)>0 OR CHARINDEX(@busca,Email)>0 OR CHARINDEX(@busca,Origem)>0)
        AND (@status IS NULL OR Status=@status) ORDER BY Nome,Id
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 30));
    public async Task<Pessoas?> ObterAtivaPorIdAsync(int id) => (await sql.ConsultarAsync($"SELECT {Colunas} FROM dbo.vw_Pessoas WHERE Id=@id AND DataExclusao IS NULL", Mapear, Int("@id", id))).SingleOrDefault();
    private const string Duplicada = "SELECT Id FROM dbo.vw_Pessoas WITH (UPDLOCK,HOLDLOCK) WHERE (@id IS NULL OR Id<>@id) AND ((@email IS NOT NULL AND LOWER(LTRIM(RTRIM(Email)))=@email) OR (@linkedin IS NOT NULL AND LOWER(UrlLinkedIn)=LOWER(@linkedin)))";
    public async Task<bool> DuplicadaAsync(string? email, string? linkedin, int? ignorarId = null) =>
        (await sql.ConsultarAsync(Duplicada, r => r.GetInt32(0), Text("@email", email, 150), Text("@linkedin", linkedin, 255), Int("@id", ignorarId))).Count > 0;
    private static SqlParameter[] Parametros(Pessoas p) =>
    [
        Text("@Nome", p.Nome, 150), Text("@Email", p.Email, 150), Text("@Telefone", p.Telefone, 20),
        Text("@UrlLinkedIn", p.UrlLinkedIn, 255), Text("@Status", p.Status, 50), Text("@Origem", p.Origem, 100)
    ];
    public async Task AdicionarAsync(Pessoas pessoa) => await SalvarAsync(pessoa, true);
    public async Task AtualizarAsync(Pessoas pessoa) => await SalvarAsync(pessoa, false);
    private async Task SalvarAsync(Pessoas p, bool novo)
    {
        await sql.TransacaoAsync(async (conn, tx) =>
        {
            var duplicates = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, Duplicada, r => r.GetInt32(0), Int("@id", novo ? null : p.Id), Text("@email", p.Email, 150), Text("@linkedin", p.UrlLinkedIn, 255));
            if (duplicates.Count > 0) throw new ConflitoException("Já existe uma pessoa com este e-mail ou perfil profissional, inclusive na base removida.");
            if (novo)
            {
                p.Id = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_PessoasAdd", "@NovoId",
                    [..Parametros(p), Date("@DataColeta", p.DataColeta), Bit("@InformarDataColeta", true)]);
            }
            else
            {
                var linhas = await SqlLeadScopeB2B.ExecutarProcedureComSaidaAsync(conn, tx, "dbo.usp_PessoasAlt", "@Linhas",
                    [..Parametros(p), Int("@Id", p.Id), Date("@DataBloqueio", p.DataBloqueio),
                     Date("@DataExclusao", p.DataExclusao), Bit("@SomenteAtivo", true)]);
                if (linhas == 0) throw new KeyNotFoundException("Pessoa não encontrada.");
            }
            return true;
        });
    }
}
