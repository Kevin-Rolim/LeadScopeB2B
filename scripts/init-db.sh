#!/usr/bin/env bash
set -Eeuo pipefail

: "${MSSQL_SA_PASSWORD:?Defina MSSQL_SA_PASSWORD no arquivo .env}"
export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"
readonly SQLCMD=/opt/mssql-tools18/bin/sqlcmd
if [[ ! -x "$SQLCMD" ]]; then
  echo "ERRO: sqlcmd não encontrado em $SQLCMD. Verifique a imagem oficial SQL Server 2022." >&2
  exit 1
fi

sql() {
  "$SQLCMD" -S sqlserver -U sa -C -b -l 15 -t 120 -r 1 "$@"
}

echo "[sql-init] Verificando a existência do banco LeadScopeB2B..."
existe="$(sql -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N'LeadScopeB2B') IS NULL THEN 0 ELSE 1 END" | tr -d '\r' | xargs)"
case "$existe" in
  0)
    echo "[sql-init] Criando banco LeadScopeB2B (nenhum banco anterior será removido)."
    sql -d master -Q "CREATE DATABASE [LeadScopeB2B]"
    ;;
  1)
    echo "[sql-init] Banco preexistente encontrado. Dados serão preservados."
    ;;
  *)
    echo "ERRO: resposta inesperada ao consultar a existência do banco: '$existe'" >&2
    exit 1
    ;;
esac

qtd="$(sql -d LeadScopeB2B -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0" | tr -d '\r' | xargs)"
case "$qtd" in
  0)
    echo "[sql-init] Criando as 14 tabelas e os perfis de referência..."
    sql -d LeadScopeB2B -i /bootstrap/LeadScopeB2B.sql
    ;;
  14)
    echo "[sql-init] Tabelas existentes encontradas. Não executando o script de recriação."
    ;;
  *)
    echo "ERRO: esquema incompleto/incompatível ($qtd tabelas). Nenhuma tabela será removida. Examine o banco antes de prosseguir." >&2
    exit 1
    ;;
esac

echo "[sql-init] Conferindo compatibilidade do esquema e dos perfis..."
sql -d LeadScopeB2B -i /bootstrap/ValidarInstalacao.sql
echo "[sql-init] Inicialização concluída com sucesso."
