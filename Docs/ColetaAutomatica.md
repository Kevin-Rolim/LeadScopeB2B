# Coleta automática — guia substituído

A coleta exclusiva da CNPJAPI foi substituída pelo motor universal de integrações. Consulte [MotorIntegracoes.md](MotorIntegracoes.md) para configuração atual, autenticação, limites, revisão, automação e Docker Compose.

O worker existente foi reaproveitado e executa contratos genéricos. Para continuar usando CNPJAPI, adicione seu contrato pelo catálogo, selecione a fonte existente, recadastre a chave na persistência cifrada e revise operação/plano/rotina. As antigas opções ColetaAutomatica/CNPJAPI não disparam consultas.

As evidências SQL e a auditoria anteriores foram preservadas. O [relatório da primeira entrega](EntregaColetaAutomatica.md) é histórico e não descreve a arquitetura atual.
