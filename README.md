# Projeto Envio Israel

MVP para campanha de arrecadação do envio pastoral a Israel.

## Stack

- .NET 8 / ASP.NET Core Minimal API
- Arquitetura em camadas: `Domain`, `Application`, `Infrastructure`, `Api`
- SPA responsiva servida por `wwwroot`
- Persistência local em JSON para desenvolvimento
- Upload local de comprovantes fora da pasta pública

O projeto foi estruturado para permitir trocar a persistência local por PostgreSQL/Entity Framework Core e o armazenamento local por Azure Blob Storage sem alterar o contrato da API pública.

## Como rodar localmente

```powershell
$env:TEMP=(Resolve-Path .\.tmp).Path
$env:TMP=$env:TEMP
dotnet restore MissaoIsrael.sln
dotnet run --project src\MissaoIsrael.Api\MissaoIsrael.Api.csproj --urls http://localhost:5190
```

Acesse:

- Landing page: http://localhost:5190
- Painel admin: http://localhost:5190/#admin
- Healthcheck: http://localhost:5190/api/health

Credenciais locais padrão, criadas apenas quando `App_Data/store.json` ainda não existe:

- E-mail: `admin@envioisrael.local`
- Senha: `admin123`

## Checklist antes de publicar

1. Configure `AdminAuth__Secret` com pelo menos 32 caracteres.
2. Configure `AdminSeed__Email`, `AdminSeed__Name` e `AdminSeed__Password` antes de usar o painel administrativo em produção.
3. Entre no painel admin e substitua chave PIX, QR Code PIX, telefone/e-mail de contato, imagem principal e URL do vídeo.
4. Revise meta financeira, textos da campanha e status. Mantenha `Inativa` até tudo estar conferido.
5. Garanta backup/persistência externa para `App_Data` ou troque a persistência JSON por banco antes de escalar o uso.

## Publicar no Render

O projeto está preparado para Render via Docker:

- `Dockerfile`: build e publish da aplicação .NET 8.
- `render.yaml`: blueprint com Web Service, healthcheck e persistent disk.
- Persistent disk: montado em `/var/data`.
- Dados, comprovantes, fotos do mural, imagens e vídeos enviados pelo admin usam `Storage__DataRoot`.

Passos:

1. Suba este repositório para o GitHub/GitLab.
2. No Render, crie um Blueprint usando `render.yaml` ou um Web Service Docker apontando para o repositório.
3. Configure os secrets que estão com `sync: false`:
   - `AdminAuth__Secret`
   - `AdminSeed__Email`
   - `AdminSeed__Password`
4. Confirme que `Storage__DataRoot=/var/data`.
5. Faça o primeiro deploy e acesse `/api/health`.
6. Entre em `/#admin`, revise a campanha, configure PIX, imagens, vídeo e só então mude o status para `Ativa`.

Se `AdminSeed__Password` ainda não estiver configurado, o site público sobe normalmente, mas nenhum usuário administrativo é criado. Ao configurar o secret e redeployar, o primeiro admin é criado se ainda não existir nenhum usuário no `store.json`.

Observação: o persistent disk do Render exige plano pago e impede múltiplas instâncias usando o mesmo disco. Para escala maior, migre JSON/upload local para PostgreSQL e storage externo.

## Variáveis de ambiente

Use `.env.example` como referência. Em ASP.NET Core, `__` representa `:` na configuração.

```powershell
$env:AdminAuth__Secret="troque-por-um-segredo-com-mais-de-32-caracteres"
$env:AdminSeed__Email="admin@seudominio.com"
$env:AdminSeed__Name="Administrador"
$env:AdminSeed__Password="troque-esta-senha"
$env:Storage__DataRoot="/var/data"
```

## Fluxo do MVP

1. Visitante acessa a campanha e vê meta, arrecadado, restante e progresso.
2. Visitante copia a chave PIX e faz a contribuição externamente.
3. Visitante envia valor, WhatsApp e comprovante.
4. Contribuição entra como `Pendente`.
5. Admin aprova ou rejeita no painel.
6. Ao aprovar, o valor passa a contar no progresso e pode aparecer no mural.

## Dados locais

Durante o desenvolvimento, os dados ficam em:

- `src/MissaoIsrael.Api/App_Data/store.json`
- `src/MissaoIsrael.Api/App_Data/receipts/`

Esses arquivos simulam banco e storage no ambiente local.
