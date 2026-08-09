# Configuração pública

Esta pasta contém somente schemas e exemplos seguros para publicação.

Configurações reais como `api.json`, `manager-security.json` e
`services/*.json` são locais e estão protegidas pelo `.gitignore`. Em uma
distribuição, elas permanecem sob a raiz portátil escolhida pelo usuário.

Para a API local, copie `examples/api.example.json` para `config/api.json`.
Na versão `1.0.0-beta.1`, somente `127.0.0.1` é aceito; acesso remoto permanece
desativado até existir provisionamento de HTTPS e uma política explícita de
origem.

Para autorizar a identidade dedicada da API no Manager, copie
`examples/manager-security.example.json` para `config/manager-security.json` e
substitua o SID ilustrativo pelo SID real da conta do serviço. Esse arquivo é
local e não deve ser versionado.
