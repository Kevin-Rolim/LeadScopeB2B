using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LeadScopeB2B.Data;
using LeadScopeB2B.Models;
using LeadScopeB2B.Repositories;
using LeadScopeB2B.Security;
using LeadScopeB2B.Services;
using LeadScopeB2B.ViewModels.Empresas;
using LeadScopeB2B.ViewModels.Pessoas;
using LeadScopeB2B.ViewModels.Leads;
using LeadScopeB2B.ViewModels.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
if (args.Contains("--email")) { await ResendEmailTests.Executar(); return; }
SchemaContractTests.Executar(root);
if (args.Contains("--contrato")) return;
var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json").AddUserSecrets<SqlLeadScopeB2B>(true).AddEnvironmentVariables().Build();
var source = Environment.GetEnvironmentVariable("LEADSCOPE_TEST_CONNECTION") ?? config.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(source)) throw new Exception("Configure LEADSCOPE_TEST_CONNECTION. Os testes usam um banco temporário independente.");
var database = "LeadScopeB2B_Tests_" + Guid.NewGuid().ToString("N");
var csb = new SqlConnectionStringBuilder(source) { InitialCatalog = "master" };
await using var master = new SqlConnection(csb.ConnectionString);
await master.OpenAsync();
await new SqlCommand($"CREATE DATABASE [{database}]", master).ExecuteNonQueryAsync();
csb.InitialCatalog = database;
var connection = csb.ConnectionString;
var assertions = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception("FALHOU: " + message); assertions++; Console.WriteLine("OK: " + message); }
async Task Rejeita<T>(Func<Task> action, string message) where T : Exception { try { await action(); } catch(T) { Check(true,message); return; } throw new Exception("FALHOU: " + message); }
try
{
    await using(var conn = new SqlConnection(connection))
    {
        await conn.OpenAsync();
        var schema = File.ReadAllText(Path.Combine(root,"Docs/Database/LeadScopeB2B.sql"));
        var definitions = schema[schema.IndexOf("    CREATE TABLE ConjuntoPermissoes",StringComparison.Ordinal)..schema.IndexOf("    COMMIT TRANSACTION",StringComparison.Ordinal)];
        await new SqlCommand(definitions,conn).ExecuteNonQueryAsync();
        var reference = File.ReadAllText(Path.Combine(root,"Docs/Database/InsertCPPA.sql"));
        await new SqlCommand(reference[reference.IndexOf("SET XACT_ABORT",StringComparison.Ordinal)..],conn).ExecuteNonQueryAsync();
        var objetos = File.ReadAllText(Path.Combine(root, "Data/ObjetosLeadScopeB2B.sql"));
        // O primeiro lote contém USE. A conexão do teste já seleciona sua base temporária.
        foreach (var lote in Regex.Split(objetos, @"^GO[ \t]*\r?$", RegexOptions.Multiline).Skip(1))
        {
            if (!string.IsNullOrWhiteSpace(lote))
                await new SqlCommand(lote, conn).ExecuteNonQueryAsync();
        }
        Check(Convert.ToInt32(await new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0",conn).ExecuteScalarAsync())==14,"14 tabelas originais no SQL Server");
    }
    var sql = new SqlLeadScopeB2B(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"]=connection }).Build());
    var ur = new UsuarioRepository(sql); var pr = new PessoaRepository(sql); var er = new EmpresaRepository(sql);
    var usuarios = new UsuarioService(ur,new PasswordHasher<Usuarios>());
    await usuarios.CriarAdministradorInicialAsync("Administrador de teste","admin@example.test","Teste#Local123!");
    var perfis = await ur.ListarPerfisAsync();
    foreach(var perfil in perfis.Where(p=>p.Nome is "Usuário" or "Revisor"))
        await usuarios.CadastrarAsync(new UsuarioCreateViewModel { Nome=perfil.Nome, Email=(perfil.Nome=="Usuário"?"usuario":"revisor")+"@example.test", Senha="Teste#Local123!", PerfilAcessoId=perfil.Id });
    var admin=(await ur.ObterPorEmailAsync("admin@example.test"))!;
    var comum=(await ur.ObterPorEmailAsync("usuario@example.test"))!;
    var revisor=(await ur.ObterPorEmailAsync("revisor@example.test"))!;
    // Simula referências legadas e confirma que a adequação não muda IDs nem vínculos.
    await sql.ExecutarAsync("UPDATE dbo.ConjuntoPermissoes SET Nome=CASE Nome WHEN N'Administrador' THEN 'Admin' WHEN N'Revisor' THEN 'Gestor' WHEN N'Usuário' THEN 'Leitura' ELSE Nome END; UPDATE dbo.PerfisAcesso SET Nome=CASE Nome WHEN N'Revisor' THEN 'Gestor Comercial' WHEN N'Usuário' THEN 'Analista Comercial' ELSE Nome END");
    await sql.ExecutarAsync(File.ReadAllText(Path.Combine(root,"Data/AdequarPerfis.sql")));
    await sql.ExecutarAsync(File.ReadAllText(Path.Combine(root,"Data/AdequarPerfis.sql")));
    Check((await ur.ObterPorIdAsync(admin.Id))!.PerfilAcessoId==admin.PerfilAcessoId && Acesso.Papel((await ur.ObterPorIdAsync(comum.Id))!)=="USUARIO" && Acesso.Papel((await ur.ObterPorIdAsync(revisor.Id))!)=="REVISOR","adequação legada idempotente preserva IDs e permissões");
    var adminEdit=(await usuarios.ObterParaEdicaoAsync(admin.Id))!; adminEdit.PerfilAcessoId=comum.PerfilAcessoId;
    await Rejeita<ConflitoException>(()=>usuarios.EditarAsync(adminEdit),"último administrador não pode perder perfil");
    var empresas=new EmpresaService(er); var pessoas=new PessoaService(pr); var leads=new LeadService(new LeadRepository(sql,pr,er));
    var eid=await empresas.CadastrarAsync(new EmpresaFormViewModel { RazaoSocial="Empresa acadêmica",Cnpj="04252011000110" });
    var pid=await pessoas.CadastrarAsync(new PessoaFormViewModel { Nome="Contato",Email="contato@example.test" });
    await leads.CadastrarAsync(new LeadFormViewModel { PessoaId=pid,EmpresaId=eid,StatusVinculo="ATIVO" });
    Check((await leads.ObterDetalhesAsync(pid,eid))!.StatusVinculo=="INCERTO","cadastro não aprova vínculo por campo forjado");
    await Rejeita<UnauthorizedAccessException>(()=>leads.RevisarAsync(pid,eid,comum.Id,"QUALIFICADO",null),"serviço/repositório recusa revisão do usuário comum");
    await leads.RevisarAsync(pid,eid,revisor.Id,"QUALIFICADO","Conferido");
    Check((await leads.ObterDetalhesAsync(pid,eid)) is { StatusLead:"QUALIFICADO",StatusVinculo:"ATIVO" },"revisor decide vínculo e registra histórico");
    var cnaes=new CnaeService(new CnaeRepository(sql));
    await sql.ExecutarAsync("INSERT INTO dbo.Cnaes (Numero,Descricao) VALUES ('6201501','Desenvolvimento de software')");
    await cnaes.AssociarAsync(eid,"6201-5/01"); await cnaes.AssociarAsync(eid,"6201501");
    Check((await cnaes.ListarDaEmpresaAsync(eid)).Count==1,"associação CNAE idempotente");
    Check((await empresas.ListarAsync("acadêmica","PENDENTE","6201501")).Count==1 && (await empresas.ListarAsync("04252011000110","PENDENTE","6201501")).Count==1 && (await empresas.ListarAsync("acadêmica","INATIVA","6201501")).Count==0,"CNAE combinado com nome, CNPJ e status");
    Check((await cnaes.ListarAsync("software")).Count==1,"catálogo pesquisável por descrição");
    var csv=new ImportacaoCsvService(empresas,pessoas);
    async Task<List<ResultadoCsv>> Import(string text,string tipo) { using var stream=new MemoryStream(Encoding.UTF8.GetBytes(text)); return await csv.ImportarAsync(stream,tipo); }
    var result=await Import("RazaoSocial;Cnpj\nDuplicada;04252011000110\nInválida;123\nNova;11222333000181", "empresas");
    Check(result.Count==3 && result.Count(r=>r.Sucesso)==1,"CSV empresas: duplicidade, CNPJ inválido e sucesso por registro");
    result=await Import("Nome;Email\n\"Pessoa; CSV\";csv@example.test\nDuplicada;csv@example.test\nInválida;ruim", "pessoas");
    Check(result.Count==3 && result.Count(r=>r.Sucesso)==1,"CSV pessoas: aspas, separador, duplicidade e e-mail inválido");
    await Rejeita<System.ComponentModel.DataAnnotations.ValidationException>(()=>Import("Nome;Nome\na;b","pessoas"),"CSV rejeita cabeçalhos duplicados");
    await Rejeita<ConflitoException>(()=>new ExportacaoService(new ExportacaoRepository(sql)).ExportarAsync([$"{pid}:{eid}"],comum.Id),"exportação recusa cadastros pendentes");
    var empresaEdit=(await empresas.ObterParaEdicaoAsync(eid))!; empresaEdit.Status="VALIDADA"; await empresas.EditarAsync(empresaEdit);
    var pessoaEdit=(await pessoas.ObterParaEdicaoAsync(pid))!; pessoaEdit.Status="VALIDADO"; await pessoas.EditarAsync(pessoaEdit);
    var exported=await new ExportacaoService(new ExportacaoRepository(sql)).ExportarAsync([$"{pid}:{eid}"],comum.Id);
    Check(Encoding.UTF8.GetString(exported).Contains("Contato"),"exportação CSV registra seleção");
    await using(var app=await ContaFlowTests.AplicacaoTeste.IniciarAsync(root,connection))
    {
        foreach(var pair in new[]{("admin",true,true),("usuario",true,false),("revisor",false,true)})
        {
            using var web=app.Client();
            string Token(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            var html=await web.GetStringAsync("/Conta/Login");
            await web.PostAsync("/Conta/Login",new FormUrlEncodedContent(new Dictionary<string,string> { ["Email"]=pair.Item1+"@example.test",["Senha"]="Teste#Local123!",["__RequestVerificationToken"]=Token(html) }));
            Check((await web.GetAsync("/Empresas/Create")).IsSuccessStatusCode==pair.Item2,$"{pair.Item1}: permissão cadastro");
            Check((await web.GetAsync("/Importacao")).IsSuccessStatusCode==pair.Item2,$"{pair.Item1}: permissão CSV");
            Check((await web.GetAsync("/Usuarios")).IsSuccessStatusCode==(pair.Item1=="admin"),$"{pair.Item1}: administração restrita");
            var detail=await web.GetStringAsync($"/Leads/Details?pessoaId={pid}&empresaId={eid}");
            Check(detail.Contains("Registrar revisão")==pair.Item3,$"{pair.Item1}: revisão na view");
            var review=await web.PostAsync("/Leads/Revisar",new FormUrlEncodedContent(new Dictionary<string,string> { ["pessoaId"]=pid.ToString(),["empresaId"]=eid.ToString(),["decisao"]="EM_ANALISE",["__RequestVerificationToken"]=Token(await web.GetStringAsync("/Conta/Perfil")) }));
            Check(pair.Item3 ? review.Headers.Location?.OriginalString.Contains("Details")==true : review.Headers.Location?.OriginalString.Contains("/Conta/Negado")==true,$"{pair.Item1}: autorização POST revisão");
            if (pair.Item2)
            {
                using var multipart=new MultipartFormDataContent();
                multipart.Add(new StringContent(Token(await web.GetStringAsync("/Importacao"))),"__RequestVerificationToken");
                multipart.Add(new StringContent("pessoas"),"tipo");
                multipart.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Nome;Email\nContato HTTP;"+pair.Item1+"-csv@example.test")),"arquivo","pessoas.csv");
                var upload=await web.PostAsync("/Importacao",multipart);
                Check(upload.IsSuccessStatusCode && (await upload.Content.ReadAsStringAsync()).Contains("Cadastrado."),$"{pair.Item1}: upload CSV multipart real");
                var cadastroToken=Token(await web.GetStringAsync("/Conta/Perfil"));
                var remove=await web.PostAsync($"/Empresas/RemoverCnae/{eid}",new FormUrlEncodedContent(new Dictionary<string,string> { ["numero"]="6201501",["__RequestVerificationToken"]=cadastroToken }));
                Check(remove.StatusCode==HttpStatusCode.Redirect && (await cnaes.ListarDaEmpresaAsync(eid)).Count==0,"desassociação CNAE pelo cadastro");
                await web.PostAsync($"/Empresas/AssociarCnae/{eid}",new FormUrlEncodedContent(new Dictionary<string,string> { ["numero"]="6201501",["__RequestVerificationToken"]=cadastroToken }));
                Check((await cnaes.ListarDaEmpresaAsync(eid)).Count==1,"associação CNAE pelo cadastro");
            }
            foreach(var path in new[]{"/api/empresas","/Integracoes","/IntegracoesApis","/Fontes","/Cnaes"}) Check((await web.GetAsync(path)).StatusCode==HttpStatusCode.NotFound,"rota removida "+path);
            Check((await web.GetAsync($"/Empresas/Edit/{eid}")).IsSuccessStatusCode==pair.Item2,$"{pair.Item1}: edição empresa/CNAE");
        }
        if (args.Contains("--visual"))
        {
            var visual = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory=root, UseShellExecute=false };
            visual.ArgumentList.Add("scripts/test-ui.mjs"); visual.ArgumentList.Add(app.BaseUrl);
            visual.Environment["LEADSCOPE_UI_PASSWORD"]="Teste#Local123!";
            using var process=System.Diagnostics.Process.Start(visual)!;
            await process.WaitForExitAsync();
            Check(process.ExitCode==0,"validação visual e interações no Chrome desktop/mobile");
        }
    }
    Check((await new AuditoriaService(new AuditoriaRepository(sql)).ListarAsync()).Any(l=>l.TipoEvento=="IMPORTACAO_CSV"),"importação auditada");
    await ContaFlowTests.ExecutarAsync(root,connection,Check);
    await leads.ExcluirAsync(pid,eid); await pessoas.ExcluirAsync(pid); await empresas.ExcluirAsync(eid);
    Check(await leads.ObterDetalhesAsync(pid,eid) is null && await pessoas.ObterDetalhesAsync(pid) is null && await empresas.ObterDetalhesAsync(eid) is null,"exclusões lógicas preservadas");
    Console.WriteLine($"SUCESSO: {assertions} verificações SQL/HTTP.");
}
finally
{
    SqlConnection.ClearAllPools();
    await new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]",master).ExecuteNonQueryAsync();
    Console.WriteLine("Banco temporário excluído; base original preservada.");
}
