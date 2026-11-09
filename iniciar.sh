#!/usr/bin/env bash
# Inicialização local. Não utiliza a composição de produção do Coolify.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

command -v docker >/dev/null || { echo 'Instale o Docker com Docker Compose antes de continuar.' >&2; exit 1; }
docker compose version >/dev/null
docker info >/dev/null

if [[ ! -f .env ]]; then
  (umask 077; printf 'MSSQL_SA_PASSWORD=Ls@%s\n' "$(od -An -N24 -tx1 /dev/urandom | tr -d ' \n')" > .env)
  echo 'Configuração local criada em .env.'
fi

compose=(docker compose -f compose.yaml)
sql() {
  "${compose[@]}" exec -T sqlserver sh -c 'SQLCMDPASSWORD="$MSSQL_SA_PASSWORD" /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b "$@"' sh "$@"
}

echo 'Iniciando o banco de dados...'
"${compose[@]}" up -d --wait sqlserver
sql -Q "IF DB_ID(N'LeadScopeB2B') IS NULL CREATE DATABASE [LeadScopeB2B];"
tables=$(sql -d LeadScopeB2B -h -1 -W -Q 'SET NOCOUNT ON; SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0;' | tr -d '[:space:]')
if [[ "$tables" == 0 ]]; then
  echo 'Preparando as tabelas...'
  sql -d LeadScopeB2B < Data/LeadScopeB2B.sql
fi

echo 'Compilando o sistema...'
"${compose[@]}" build leadscope
admins=$(sql -d LeadScopeB2B -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.Usuarios u JOIN dbo.PerfisAcesso p ON p.Id=u.PerfilAcessoId JOIN dbo.ConjuntoPermissoes c ON c.Id=p.ConjuntoPermissoesId WHERE c.Nome=N'Administrador';" | tr -d '[:space:]')
if [[ "$admins" == 0 ]]; then
  echo 'Crie o primeiro administrador (guarde o e-mail e a senha para entrar).'
  read -r -p 'Nome: ' LEADSCOPE_ADMIN_NOME
  read -r -p 'E-mail: ' LEADSCOPE_ADMIN_EMAIL
  read -r -s -p 'Senha: ' LEADSCOPE_ADMIN_SENHA
  printf '\n'
  export LEADSCOPE_ADMIN_NOME LEADSCOPE_ADMIN_EMAIL LEADSCOPE_ADMIN_SENHA
  "${compose[@]}" run --rm --no-deps \
    -e LEADSCOPE_ADMIN_NOME -e LEADSCOPE_ADMIN_EMAIL -e LEADSCOPE_ADMIN_SENHA \
    leadscope --criar-admin
  unset LEADSCOPE_ADMIN_NOME LEADSCOPE_ADMIN_EMAIL LEADSCOPE_ADMIN_SENHA
fi

"${compose[@]}" up -d --wait leadscope
echo 'Sistema iniciado: http://localhost:18080'
