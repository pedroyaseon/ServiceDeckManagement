# Configuração pública

Esta pasta contém somente schemas e exemplos seguros para publicação.

Configurações reais como `api.json`, `manager-security.json` e
`services/*.json` são locais e estão protegidas pelo `.gitignore`. Em uma
distribuição, elas permanecem sob a raiz portátil escolhida pelo usuário.

Para a API local, copie `examples/api.example.json` para `config/api.json`.
Na versão `1.0.0-beta.2`, somente `127.0.0.1` é aceito; acesso remoto permanece
desativado até existir provisionamento de HTTPS e uma política explícita de
origem.

O Launcher usa `http://127.0.0.1:5180/` por padrão. Para alterar apenas a porta
local, copie `examples/launcher.example.json` para `config/launcher.json`. A
configuração real permanece ignorada pelo Git; URLs externas, credenciais na URL
e caminhos adicionais são recusados nesta versão.

Para autorizar a identidade dedicada da API no Manager, copie
`examples/manager-security.example.json` para `config/manager-security.json` e
substitua o SID ilustrativo pelo SID real da conta do serviço. Esse arquivo é
local e não deve ser versionado.
