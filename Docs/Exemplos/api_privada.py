"""API de demonstração, não parte do executor do LeadScope.
Dados fictícios; executar manualmente em desenvolvimento. Sem dependências externas.
DEMO_API_KEY deve ser definida no ambiente. Nunca é registrada em log.
"""
import json
import os
import re
import secrets
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

KEY = os.environ.get("DEMO_API_KEY", "")
if not KEY:
    raise SystemExit("Defina DEMO_API_KEY antes de iniciar a demonstração.")

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass  # Não registrar headers, URLs com parâmetros ou credenciais.

    def send_json(self, status, body):
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if not secrets.compare_digest(self.headers.get("X-Integration-Key", ""), KEY):
            return self.send_json(401, {"error": {"code": "invalid_credentials"}})
        registration = re.fullmatch(r"/v1/companies/([0-9]{14})", self.path)
        if not registration:
            return self.send_json(404, {"error": {"code": "not_found"}})
        self.send_json(200, {
            "registration": registration.group(1),
            "organization": {"legalName": "Empresa fictícia de demonstração"},
            "location": {"city": "São Paulo", "state": "SP"},
        })

if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", 8015), Handler)
    print("Demonstração em http://127.0.0.1:8015; sem acesso público.")
    server.serve_forever()
