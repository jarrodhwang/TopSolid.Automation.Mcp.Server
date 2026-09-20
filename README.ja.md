# TopSolid Automation AI — 0.5.21

[English](README.md) · [한국어](README.ko.md) · [Français](README.fr.md) · [Português](README.pt.md) · **日本語** · [Deutsch](README.de.md)

TopSolid を自動化する Windows 用 AI Studio と、分離された MCP サーバーです。Studio はクラウド API またはローカル Ollama を利用できます。MCP サーバーは TopSolid Automation API を通して実際のドキュメントと CAM データを読み取り、変更は確認ダイアログを経由します。

## 本番 Studio のプレビュー

画像は開発モードではなく、公開用 Windows アプリから取得しました。Studio の UI 言語は英語です。3D プレビューは、開いていた TopSolid CAM ドキュメントだけを使用しています。表示しているローカルモデルは Mistral、Gemma、GPT-OSS です。

<p align="center">
  <img src="docs/images/ai-studio-main.png" alt="Mistral アイコンを表示した TopSolid Automation AI チャット" width="32%" />
  <img src="docs/images/ai-studio-model-selection.png" alt="Mistral アイコンを表示した AI モデル設定" width="32%" />
  <img src="docs/images/ai-studio-cam-preview.png" alt="開いている TopSolid CAM ドキュメントの読み取り専用 3D プレビュー" width="32%" />
</p>

モデルセレクターは、選択したモデルファミリーに対応するアイコンを表示します。モデル一覧の取得前でも、現在の設定モデルのアイコンを保持します。公開画像には Qwen と DeepSeek を表示していません。

## クイックスタート

Windows x64、.NET 10 Desktop、.NET Framework 4.8、ライセンス済みの TopSolid 7.18 以降が必要です。`artifacts/TopSolid-AI-0.5.21` フォルダー全体を保持し、`TopSolid.Automation.AI.Studio.exe` を起動してください。TopSolid は同じ Windows セッションで先に起動します。

Ollama またはクラウド API を選択し、Mistral、Gemma、GPT-OSS のいずれかを設定して **List models** と **Save settings** を実行します。その後、アクティブドキュメントの読み取りや読み取り専用 3D プレビューを依頼できます。

API キー、SDK DLL、オフライン API ドキュメントキャッシュは公開リポジトリに含めません。詳細な範囲と検証情報は[英語 README](README.md)を参照してください。
