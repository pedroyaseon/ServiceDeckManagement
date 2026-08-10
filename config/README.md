# Configuração pública

Esta pasta contém somente schemas e exemplos seguros para publicação.

Configurações reais como `api.json`, `manager-security.json` e
`services/*.json` são locais e estão protegidas pelo `.gitignore`. Em uma
distribuição, elas permanecem sob a raiz portátil escolhida pelo usuário.

Para a API local, copie `examples/api.example.json` para `config/api.json`.
Na versão `1.0.0-beta.4`, somente `127.0.0.1` é aceito; acesso remoto permanece
desativado até existir provisionamento de HTTPS e uma política explícita de
origem.

O Launcher não possui URL de API e não usa `launcher.json`. Ele se conecta ao
Manager pelo Named Pipe local autenticado. A API é opcional e será configurada
separadamente quando o acesso remoto for habilitado.

Para autorizar as identidades dedicadas da API e do Launcher no Manager, copie
`examples/manager-security.example.json` para `config/manager-security.json` e
substitua os SIDs ilustrativos pelos SIDs reais. Os campos `apiClientSid` e
`launcherClientSid` são separados e não podem usar o mesmo valor. Esse arquivo
é local e não deve ser versionado.
