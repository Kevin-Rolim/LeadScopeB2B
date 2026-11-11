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
        SELECT {Colunas} FROM dbo.vw_Empresas e WHERE ISNULL(Status,'') <> 'REMOVIDA'
        AND (@busca IS NULL OR CHARINDEX(@busca,RazaoSocial)>0 OR CHARINDEX(@busca,NomeFantasia)>0 OR CHARINDEX(@busca,Cnpj)>0 OR CHARINDEX(@busca,Cidade)>0 OR CHARINDEX(@busca,Segmento)>0)
        AND (@status IS NULL OR Status=@status)
        AND (@cnae IS NULL OR EXISTS (SELECT 1 FROM dbo.vw_CnaesEmpresas ce WHERE ce.EmpresaId=e.Id AND ce.CnaeNumero=@cnae))
        ORDER BY RazaoSocial,Id
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 30), Text("@cnae", cnae, 20));
    public async Task<Empresas?> ObterPorIdAsync(int id) => (await sql.ConsultarAsync($"SELECT {Colunas} FROM dbo.vw_Empresas WHERE Id=@id AND ISNULL(Status,'')<>'REMOVIDA'", Mapear, Int("@id", id))).SingleOrDefault();
    public async Task<bool> CnpjExisteAsync(string cnpj, int? ignorarId = null) => (await sql.ConsultarAsync("SELECT Id FROM dbo.vw_Empresas WHERE Cnpj=@cnpj AND (@id IS NULL OR Id<>@id)", r => r.GetInt32(0), Text("@cnpj", cnpj, 18), Int("@id", ignorarId))).Count > 0;
    private static SqlParameter[] Parametros(Empresas e) =>
    [
        Text("@RazaoSocial", e.RazaoSocial, 200), Text("@NomeFantasia", e.NomeFantasia, 200),
        Text("@Cnpj", e.Cnpj, 18), Text("@UrlSite", e.UrlSite, 255), Text("@Segmento", e.Segmento, 100),
        Text("@Porte", e.Porte, 50), Text("@Cidade", e.Cidade, 100), Text("@Estado", e.Estado, 2),
        Text("@Email", e.Email, 150), Text("@Telefone", e.Telefone, 20), Text("@Status", e.Status, 50),
        Date("@DataAtualizacao", e.DataAtualizacao)
    ];

    public async Task AdicionarAsync(Empresas empresa) =>
        empresa.Id = await sql.ExecutarProcedureComSaidaAsync("dbo.usp_EmpresasAdd", "@NovoId",
            [..Parametros(empresa), Date("@DataCriacao", empresa.DataCriacao), Bit("@InformarDatas", true)]);

    public async Task AtualizarAsync(Empresas empresa)
    {
        var linhas = await sql.ExecutarProcedureComSaidaAsync("dbo.usp_EmpresasAlt", "@Linhas",
            [..Parametros(empresa), Int("@Id", empresa.Id), Bit("@SomenteAtivo", true), Bit("@InformarDataAtualizacao", true)]);
        if (linhas == 0) throw new KeyNotFoundException("Empresa não encontrada.");
    }

    public async Task ExcluirAsync(int id)
    {
        if (await sql.ExecutarProcedureComSaidaAsync("dbo.usp_EmpresasExcluir", "@Linhas", Int("@Id", id)) == 0)
            throw new KeyNotFoundException("Empresa não encontrada.");
    }
}
