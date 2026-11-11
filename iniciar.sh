#!/usr/bin/env bash
# Inicialização local. Não utiliza a composição de produção do Coolify.
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

command -v docker >/dev/null || { echo 'Instale o Docker com Docker Compose antes de continuar.' >&2; exit 1; }
docker compose version >/dev/null
engine=$(docker info --format '{{.OSType}}')
[[ "$engine" == linux ]] || { echo 'Configure o Docker para executar contêineres Linux.' >&2; exit 1; }

if [[ ! -f .env && -z ${MSSQL_SA_PASSWORD:-} ]]; then
  (umask 077; printf 'MSSQL_SA_PASSWORD=Ls@%s\n' "$(od -An -N24 -tx1 /dev/urandom | tr -d ' \n')" > .env)
  echo 'Configuração local criada em .env.'
fi

docker compose -f compose.yaml up -d --build --wait
docker compose -f compose.yaml exec leadscope dotnet LeadScopeB2B.dll --criar-admin
echo 'Sistema iniciado: http://localhost:18080'
