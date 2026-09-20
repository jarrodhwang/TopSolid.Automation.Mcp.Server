# TopSolid Automation AI — 0.5.21

[English](README.md) · [한국어](README.ko.md) · [Français](README.fr.md) · **Português** · [日本語](README.ja.md) · [Deutsch](README.de.md)

Studio AI para Windows e servidor MCP separado para automação do TopSolid. O Studio pode usar uma API na nuvem ou um servidor Ollama local. O servidor MCP lê documentos e dados CAM reais por meio da TopSolid Automation, e toda alteração exige confirmação visível.

## Pré-visualização do Studio em produção

As imagens foram capturadas no aplicativo Windows publicado, fora do modo de desenvolvimento, com a interface do Studio em inglês. A pré-visualização 3D usa somente o documento CAM TopSolid que já estava aberto. Os modelos locais exibidos são Mistral, Gemma e GPT-OSS.

<p align="center">
  <img src="docs/images/ai-studio-main.png" alt="Janela de chat TopSolid Automation AI com o ícone Mistral" width="32%" />
  <img src="docs/images/ai-studio-model-selection.png" alt="Configuração do modelo Ollama com o ícone Mistral" width="32%" />
  <img src="docs/images/ai-studio-cam-preview.png" alt="Pré-visualização 3D somente leitura do documento CAM TopSolid aberto" width="32%" />
</p>

O seletor associa o ícone correto à família do modelo selecionado, inclusive antes da descoberta de modelos terminar. Qwen e DeepSeek não aparecem nas capturas públicas.

## Início rápido

Requer Windows x64, .NET 10 Desktop, .NET Framework 4.8 e uma instalação licenciada do TopSolid 7.18 ou posterior. Mantenha a pasta completa `artifacts/TopSolid-AI-0.5.21` e execute `TopSolid.Automation.AI.Studio.exe`. O TopSolid deve estar aberto na mesma sessão do Windows.

Escolha Ollama ou uma API na nuvem, selecione Mistral, Gemma ou GPT-OSS, use **List models** e salve com **Save settings**. Depois, peça a leitura do documento ativo ou uma pré-visualização 3D somente leitura.

Chaves de API, DLLs do SDK e o cache local da documentação da API não são publicados. Consulte o [README em inglês](README.md) para detalhes de escopo e validação.
