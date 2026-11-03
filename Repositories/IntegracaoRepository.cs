using LeadScopeB2B.Data;
using LeadScopeB2B.Services;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;
namespace LeadScopeB2B.Repositories;

public partial class IntegracaoRepository(SqlLeadScopeB2B sql)
{
    private const string Colunas = "Id,Campo,Valor,NivelConfianca,FonteDadosId,PessoaId,EmpresaId";
    private static DadosColetados Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("Id"), Campo = r.Text("Campo")!, Valor = r.Text("Valor")!, NivelConfianca = r.Decimal("NivelConfianca"), FonteDadosId = r.Int("FonteDadosId"),
        PessoaId = r.IsDBNull(r.GetOrdinal("PessoaId")) ? null : r.Int("PessoaId"), EmpresaId = r.IsDBNull(r.GetOrdinal("EmpresaId")) ? null : r.Int("EmpresaId")
    };
    public Task<List<DadosColetados>> ListarAsync() => sql.ConsultarAsync($"SELECT TOP (200) {Colunas} FROM dbo.DadosColetados ORDER BY Id DESC", Mapear);
    public async Task<DadosColetados?> ObterAsync(int id) => (await sql.ConsultarAsync($"SELECT {Colunas} FROM dbo.DadosColetados WHERE Id=@id", Mapear, Int("@id", id))).SingleOrDefault();
    public async Task<int> AdicionarAsync(DadosColetados dado, int usuarioId) => await sql.TransacaoAsync(async (conn, tx) =>
    {
        var query = dado.PessoaId.HasValue
            ? "SELECT Id FROM dbo.Pessoas WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id AND DataExclusao IS NULL AND DataBloqueio IS NULL"
            : "SELECT Id FROM dbo.Empresas WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id AND ISNULL(Status,'') NOT IN ('REMOVIDA','INATIVA')";
        var valid = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, query, r => r.GetInt32(0), Int("@id", dado.PessoaId ?? dado.EmpresaId));
        if (valid.Count == 0) throw new ConflitoException("Destino inexistente, bloqueado ou removido.");
        var fontes = await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "SELECT Id FROM dbo.FonteDados WITH (UPDLOCK,HOLDLOCK) WHERE Id=@id AND DataHoraExclusao IS NULL", r => r.GetInt32(0), Int("@id", dado.FonteDadosId));
        if (fontes.Count == 0) throw new ConflitoException("Fonte inativa.");
        var duplicados = await SqlLeadScopeB2B.ConsultarAsync(conn, tx,
            "SELECT Id FROM dbo.DadosColetados WITH (UPDLOCK,HOLDLOCK) WHERE FonteDadosId=@fonte AND Campo=@campo AND Valor=@valor AND ((EmpresaId=@empresa AND PessoaId IS NULL) OR (PessoaId=@pessoa AND EmpresaId IS NULL))",
            r => r.GetInt32(0), Int("@fonte", dado.FonteDadosId), Text("@campo", dado.Campo, 100), Text("@valor", dado.Valor), Int("@empresa", dado.EmpresaId), Int("@pessoa", dado.PessoaId));
        if (duplicados.Count > 0) return duplicados[0];
        var id = (await SqlLeadScopeB2B.ConsultarAsync(conn, tx, "INSERT INTO dbo.DadosColetados (Campo,Valor,NivelConfianca,FonteDadosId,PessoaId,EmpresaId) OUTPUT INSERTED.Id VALUES (@campo,@valor,@confianca,@fonte,@pessoa,@empresa)", r => r.GetInt32(0), Text("@campo", dado.Campo, 100), Text("@valor", dado.Valor), Decimal("@confianca", dado.NivelConfianca), Int("@fonte", dado.FonteDadosId), Int("@pessoa", dado.PessoaId), Int("@empresa", dado.EmpresaId))).Single();
        await AuditoriaRepository.RegistrarAsync(conn, tx, usuarioId, "DADO_RECEBIDO", $"Dado coletado {id} recebido da fonte {dado.FonteDadosId}.");
        return id;
    });
}
