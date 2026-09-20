# TopSolid Automation AI — 0.5.21

[English](README.md) · [한국어](README.ko.md) · **Français** · [Português](README.pt.md) · [日本語](README.ja.md) · [Deutsch](README.de.md)

Studio AI Windows pour l’automatisation de TopSolid, avec un serveur MCP séparé. Studio peut utiliser une API cloud ou un serveur Ollama local. Le serveur MCP lit les documents et les données CAM réels via TopSolid Automation; toute modification passe par une confirmation visible.

## Aperçu de Studio en production

Ces images viennent de l’application Windows publiée, et non du mode développeur. L’interface de Studio est en anglais. L’aperçu 3D provient uniquement du document CAM TopSolid déjà ouvert. Les exemples locaux visibles sont Mistral, Gemma et GPT-OSS.

<p align="center">
  <img src="docs/images/ai-studio-main.png" alt="Fenêtre de chat TopSolid Automation AI avec l’icône Mistral" width="32%" />
  <img src="docs/images/ai-studio-model-selection.png" alt="Sélection du modèle Ollama avec l’icône Mistral" width="32%" />
  <img src="docs/images/ai-studio-cam-preview.png" alt="Aperçu 3D en lecture seule du document CAM TopSolid ouvert" width="32%" />
</p>

Le sélecteur associe automatiquement une icône à la famille du modèle sélectionné, y compris avant la fin de la découverte des modèles. Qwen et DeepSeek ne sont pas montrés dans les captures publiques.

## Démarrage rapide

Il faut Windows x64, .NET 10 Desktop, .NET Framework 4.8 et une installation TopSolid 7.18 ou ultérieure sous licence. Conservez le dossier complet `artifacts/TopSolid-AI-0.5.21`, puis lancez `TopSolid.Automation.AI.Studio.exe`. TopSolid doit déjà être ouvert dans la même session Windows.

Choisissez Ollama ou une API cloud, sélectionnez Mistral, Gemma ou GPT-OSS, cliquez sur **List models**, puis **Save settings**. Vous pouvez ensuite demander la lecture du document actif ou un aperçu 3D en lecture seule.

Les clés API, les DLL du SDK et le cache de documentation API hors ligne restent locaux et ne sont pas publiés. Consultez le [README anglais](README.md) pour la portée complète et les limites de validation.
