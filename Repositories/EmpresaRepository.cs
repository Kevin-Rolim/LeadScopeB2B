using LeadScopeB2B.Data;
using LeadScopeB2B.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using static LeadScopeB2B.Data.SqlValues;

namespace LeadScopeB2B.Repositories;

public class EmpresaRepository(SqlLeadScopeB2B sql) : IEmpresaRepository
{
    internal const string Colunas = "id, razao_social, nome_fantasia, cnpj, site, segmento, porte, cidade, estado, email_institucional, telefone_comercial, status_validacao, data_criacao, data_atualizacao";
    internal static Empresas Mapear(SqlDataReader r) => new()
    {
        Id = r.Int("id"),
        RazaoSocial = r.Text("razao_social")!,
        NomeFantasia = r.Text("nome_fantasia"),
        Cnpj = r.Text("cnpj"),
        UrlSite = r.Text("site"),
        Segmento = r.Text("segmento"),
        Porte = r.Text("porte"),
        Cidade = r.Text("cidade"),
        Estado = r.Text("estado"),
        Email = r.Text("email_institucional"),
        Telefone = r.Text("telefone_comercial"),
        Status = r.Text("status_validacao"),
        DataCriacao = r.Date("data_criacao")!.Value,
        DataAtualizacao = r.Date("data_atualizacao")!.Value
    };
    public Task<List<Empresas>> ListarAsync(string? busca = null, string? status = null, string? cnae = null) =>
        sql.ConsultarAsync($"""
        SELECT {Colunas} FROM dbo.Empresas e WHERE status_validacao <> 'REMOVIDA'
        AND (@busca IS NULL OR CHARINDEX(@busca,razao_social)>0 OR CHARINDEX(@busca,nome_fantasia)>0 OR CHARINDEX(@busca,cnpj)>0 OR CHARINDEX(@busca,cidade)>0 OR CHARINDEX(@busca,segmento)>0)
        AND (@status IS NULL OR status_validacao=@status)
        AND (@cnae IS NULL OR EXISTS (SELECT 1 FROM dbo.CNAEs_Empresas ce WHERE ce.empresa_id=e.id AND ce.cnae_numero=@cnae))
        ORDER BY razao_social,id
        """, Mapear, Text("@busca", busca, 200), Text("@status", status, 30), Text("@cnae", cnae, 20));
    public async Task<Empresas?> ObterPorIdAsync(int id) => (await sql.ConsultarAsync($"SELECT {Colunas} FROM dbo.Empresas WHERE id=@id AND status_validacao<>'REMOVIDA'", Mapear, Int("@id", id))).SingleOrDefault();
    public async Task<bool> CnpjExisteAsync(string cnpj, int? ignorarId = null) => (await sql.ConsultarAsync("SELECT id FROM dbo.Empresas WHERE cnpj=@cnpj AND (@id IS NULL OR id<>@id)", r => r.GetInt32(0), Text("@cnpj", cnpj, 14), Int("@id", ignorarId))).Count > 0;
    private static SqlParameter[] Parametros(Empresas e) => [Int("@id", e.Id), Text("@razao", e.RazaoSocial, 200), Text("@fantasia", e.NomeFantasia, 200), Text("@cnpj", e.Cnpj, 14), Text("@site", e.UrlSite, 255), Text("@segmento", e.Segmento, 100), Text("@porte", e.Porte, 50), Text("@cidade", e.Cidade, 100), Text("@estado", e.Estado, 2), Text("@email", e.Email, 150), Text("@telefone", e.Telefone, 20), Text("@status", e.Status, 30), Date("@criacao", e.DataCriacao), Date("@atualizacao", e.DataAtualizacao)];
    public async Task AdicionarAsync(Empresas empresa)
    {
        empresa.Id = (await sql.ConsultarAsync("""
        INSERT INTO dbo.Empresas (razao_social,nome_fantasia,cnpj,cnpj_raiz,site,segmento,porte,cidade,estado,email_institucional,telefone_comercial,status_validacao,data_criacao,data_atualizacao)
        OUTPUT INSERTED.id VALUES (@razao,@fantasia,@cnpj,LEFT(@cnpj,8),@site,@segmento,@porte,@cidade,@estado,@email,@telefone,@status,@criacao,@atualizacao)
        """, r => r.GetInt32(0), Parametros(empresa))).Single();
    }
    public async Task AtualizarAsync(Empresas empresa)
    {
        if (await sql.ExecutarAsync("""
        UPDATE dbo.Empresas SET razao_social=@razao,nome_fantasia=@fantasia,cnpj=@cnpj,cnpj_raiz=LEFT(@cnpj,8),site=@site,segmento=@segmento,porte=@porte,cidade=@cidade,estado=@estado,email_institucional=@email,telefone_comercial=@telefone,status_validacao=@status,data_atualizacao=@atualizacao
        WHERE id=@id AND status_validacao<>'REMOVIDA'
        """, Parametros(empresa)) == 0) throw new KeyNotFoundException("Empresa não encontrada.");
    }
    public async Task ExcluirAsync(int id)
    {
        if (await sql.ExecutarAsync("UPDATE dbo.Empresas SET status_validacao='REMOVIDA',data_atualizacao=SYSUTCDATETIME() WHERE id=@id AND status_validacao<>'REMOVIDA'", Int("@id", id)) == 0) throw new KeyNotFoundException("Empresa não encontrada.");
    }
}
