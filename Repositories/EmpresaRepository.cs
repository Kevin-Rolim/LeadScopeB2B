using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class EmpresaRepository(SqlLeadScopeB2B sql) : IEmpresaRepository
{
    internal const string Colunas = "Id, RazaoSocial, NomeFantasia, Cnpj, UrlSite, Segmento, Porte, Cidade, Estado, Email, Telefone, Status, DataCriacao, DataAtualizacao";
    internal static Empresas Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("Id"),
        RazaoSocial = r.Text("RazaoSocial")!,
        NomeFantasia = r.Text("NomeFantasia"),
        Cnpj = r.Text("Cnpj"),
        UrlSite = r.Text("UrlSite"),
        Segmento = r.Text("Segmento"),
        Porte = r.Text("Porte"),
        Cidade = r.Text("Cidade"),
        Estado = r.Text("Estado"),
        Email = r.Text("Email"),
        Telefone = r.Text("Telefone"),
        Status = r.Text("Status"),
        DataCriacao = r.Date("DataCriacao"),
        DataAtualizacao = r.Date("DataAtualizacao")
    };
    public Task<List<Empresas>> ListarAsync(string? busca = null, string? status = null, string? cnae = null) =>
        sql.ConsultarAsync($"""
        SELECT {Colunas} FROM dbo.Empresas e WHERE ISNULL(Status,'') <> 'REMOVIDA'
        AND (@busca IS NULL OR CHARINDEX(@busca,RazaoSocial)>0 OR CHARINDEX(@busca,NomeFantasia)>0 OR CHARINDEX(@busca,Cnpj)>0 OR CHARINDEX(@busca,Cidade)>0 OR CHARINDEX(@busca,Segmento)>0)
        AND (@status IS NULL OR Status=@status)
        AND (@cnae IS NULL OR EXISTS (SELECT 1 FROM dbo.CnaesEmpresas ce WHERE ce.EmpresaId=e.Id AND ce.CnaeNumero=@cnae))
        ORDER BY RazaoSocial,Id
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 30), Text("@cnae", cnae, 20));
    public async Task<Empresas?> ObterPorIdAsync(int id) => (await sql.ConsultarAsync($"SELECT {Colunas} FROM dbo.Empresas WHERE Id=@id AND ISNULL(Status,'')<>'REMOVIDA'", Mapear, Int("@id", id))).SingleOrDefault();
    public async Task<bool> CnpjExisteAsync(string cnpj, int? ignorarId = null) => (await sql.ConsultarAsync("SELECT Id FROM dbo.Empresas WHERE Cnpj=@cnpj AND (@id IS NULL OR Id<>@id)", r => r.GetInt32(0), Text("@cnpj", cnpj, 18), Int("@id", ignorarId))).Count > 0;
    private static SqlParameter[] Parametros(Empresas e) => [Int("@id", e.Id), Text("@razao", e.RazaoSocial, 200), Text("@fantasia", e.NomeFantasia, 200), Text("@cnpj", e.Cnpj, 18), Text("@site", e.UrlSite, 255), Text("@segmento", e.Segmento, 100), Text("@porte", e.Porte, 50), Text("@cidade", e.Cidade, 100), Text("@estado", e.Estado, 2), Text("@email", e.Email, 150), Text("@telefone", e.Telefone, 20), Text("@status", e.Status, 50), Date("@criacao", e.DataCriacao), Date("@atualizacao", e.DataAtualizacao)];
    public async Task AdicionarAsync(Empresas empresa)
    {
        empresa.Id = (await sql.ConsultarAsync("""
        INSERT INTO dbo.Empresas (RazaoSocial,NomeFantasia,Cnpj,UrlSite,Segmento,Porte,Cidade,Estado,Email,Telefone,Status,DataCriacao,DataAtualizacao)
        OUTPUT INSERTED.Id VALUES (@razao,@fantasia,@cnpj,@site,@segmento,@porte,@cidade,@estado,@email,@telefone,@status,@criacao,@atualizacao)
        """, r => r.GetInt32(0), Parametros(empresa))).Single();
    }
    public async Task AtualizarAsync(Empresas empresa)
    {
        if (await sql.ExecutarAsync("""
        UPDATE dbo.Empresas SET RazaoSocial=@razao,NomeFantasia=@fantasia,Cnpj=@cnpj,UrlSite=@site,Segmento=@segmento,Porte=@porte,Cidade=@cidade,Estado=@estado,Email=@email,Telefone=@telefone,Status=@status,DataAtualizacao=@atualizacao
        WHERE Id=@id AND ISNULL(Status,'')<>'REMOVIDA'
        """, Parametros(empresa)) == 0) throw new KeyNotFoundException("Empresa não encontrada.");
    }
    public async Task ExcluirAsync(int id)
    {
        if (await sql.ExecutarAsync("UPDATE dbo.Empresas SET Status='REMOVIDA',DataAtualizacao=SYSUTCDATETIME() WHERE Id=@id AND ISNULL(Status,'')<>'REMOVIDA'", Int("@id", id)) == 0) throw new KeyNotFoundException("Empresa não encontrada.");
    }
}
