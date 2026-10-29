# Validação da simplificação — 26/10/2026

Conexão do catálogo com credencial, seleção de operações e ativação de automação; opções técnicas preservadas no editor avançado. Descoberta compartilhada de cota, headers, cache, projeção de consumo, cooldown e política conservadora. Consulte o [guia de uso e limitações](SimplificacaoIntegracoes.md).

## Verificações executadas

- `dotnet build -c Release`: sucesso, zero avisos e erros.
- `dotnet run --project Tests -- --universais`: 129 verificações universais.
- `dotnet run --project Tests` com conexão SQL de teste: sucesso; 160 verificações gerais, além da suíte universal e verificações específicas de SQL/schema. Bancos temporários isolados removidos ao final.
- Testes incluem conexão MVC só com chave; cota/RPM/saldo e ilimitado; cache; política desconhecida; headers/janelas; 429/Retry-After; bloqueios; REST sem adaptador; OpenAPI/manifesto privado; persistência/cifragem; autorizações e automação desabilitada; SSRF e revisão humana.
- `docker compose build leadscope` e inicialização com automação padrão desabilitada: sucesso. SQL saudável, login HTTP 200 e área administrativa HTTP 302 para autenticação.
- Comparação SQL antes/depois: 14 tabelas e metadados idênticos para colunas, índices e relacionamentos. Nenhum model acadêmico ou script definitivo alterado nesta tarefa. Zero bancos temporários restantes.
- `git diff --check`: sem erros.

Nenhuma consulta real a CNPJAPI ou outro provedor comercial foi executada. Respostas externas dos testes são simuladas em servidores locais. Plano, mensalidade, consumo fora do LeadScopeB2B e renovação não informada continuam dependendo do fornecedor; formatos de headers não suportados exigem declaração/adaptação. Não houve novos containers, serviços de automação, alterações do Compose ou remoção de volumes.

## Arquivos desta tarefa

- `Integration/Universal/ApiHttpService.cs` (modificado)
- `Integration/Universal/CatalogoApis.cs` (modificado)
- `Integration/Universal/Contratos.cs` (modificado)
- `Integration/Universal/ValidacaoContrato.cs` (modificado)
- `Services/AutomacaoIntegracaoService.cs` (modificado)
- `Services/ColetaAutomaticaWorker.cs` (modificado)
- `Services/ConsumoIntegracaoService.Descoberta.cs` (criado)
- `Services/ConsumoIntegracaoService.cs` (modificado)
- `Services/ExecutorIntegracaoService.Descoberta.cs` (criado)
- `Services/ExecutorIntegracaoService.cs` (modificado)
- `Services/GerenciadorIntegracaoService.cs` (modificado)
- `Repositories/IntegracaoRepository.Universal.cs` (modificado)
- `Controllers/IntegracoesApisController.cs` (modificado)
- `Views/IntegracoesApis/Avancado.cshtml` (criado)
- `Views/IntegracoesApis/Configurar.cshtml` (modificado)
- `Views/IntegracoesApis/Executar.cshtml` (modificado)
- `Views/IntegracoesApis/Index.cshtml` (modificado)
- `Views/IntegracoesApis/Nova.cshtml` (modificado)
- `Views/IntegracoesApis/Operacao.cshtml` (modificado)
- `Views/IntegracoesApis/_Consumo.cshtml` (criado)
- `ViewModel/Integracoes/ApiForms.cs` (modificado)
- `Tests/ContaFlowTests.cs` (modificado)
- `Tests/Program.cs` (modificado)
- `Tests/UniversalApiTests.cs` (modificado)
- `Docs/ContratoIntegracaoLeadScopeB2B.md` (modificado)
- `Docs/MotorIntegracoes.md` (modificado)
- `Docs/SimplificacaoIntegracoes.md` (criado)
- `wwwroot/js/integracoes-apis.js` (modificado)
- `README.md` (modificado)
- `Docs/EntregaSimplificacaoIntegracoes.md` (criado)
