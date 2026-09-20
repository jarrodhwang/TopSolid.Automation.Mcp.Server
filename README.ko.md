# TopSolid Automation AI — 0.5.21

[English](README.md) · **한국어** · [Français](README.fr.md) · [Português](README.pt.md) · [日本語](README.ja.md) · [Deutsch](README.de.md)

Windows용 TopSolid 자동화 AI Studio와 MCP 서버입니다. Studio는 클라우드 API 또는 로컬 Ollama 모델을 선택하고, MCP 서버는 TopSolid Automation API를 통해 실제 문서와 CAM 데이터를 읽습니다. 변경 작업은 확인 대화상자와 함께 실행됩니다.

## 프로덕션 Studio 미리보기

아래 이미지는 개발 모드가 아닌 배포된 Windows 앱에서 캡처했습니다. Studio UI 언어는 영어이며, 3D 미리보기는 현재 열려 있던 TopSolid CAM 문서만 사용했습니다. 공개 캡처의 로컬 모델은 Mistral, Gemma, GPT-OSS만 포함합니다.

<p align="center">
  <img src="docs/images/ai-studio-main.png" alt="Mistral 아이콘이 표시된 TopSolid Automation AI 채팅" width="32%" />
  <img src="docs/images/ai-studio-model-selection.png" alt="Mistral 아이콘이 표시된 AI 모델 설정" width="32%" />
  <img src="docs/images/ai-studio-cam-preview.png" alt="열려 있는 TopSolid CAM 문서의 읽기 전용 3D 미리보기" width="32%" />
</p>

모델 선택기는 선택된 모델 계열에 맞는 아이콘을 표시합니다. 모델 목록을 조회하기 전에도 현재 설정 모델의 아이콘이 유지됩니다. Qwen과 DeepSeek은 공개 이미지에 표시하지 않았습니다.

## 빠른 시작

Windows x64, .NET 10 Desktop, .NET Framework 4.8 및 라이선스가 있는 TopSolid 7.18 이상이 필요합니다. `artifacts/TopSolid-AI-0.5.21` 전체 폴더를 유지하고 `TopSolid.Automation.AI.Studio.exe`를 실행하십시오. TopSolid은 같은 Windows 세션에서 먼저 실행되어야 합니다.

1. **Ollama** 또는 클라우드 연결을 선택합니다.
2. Ollama 서버와 Mistral, Gemma 또는 GPT-OSS 모델을 선택합니다.
3. **List models**로 모델 ID를 조회하고 **Save settings**를 누릅니다.
4. TopSolid에 연결한 뒤 활성 문서 조회나 읽기 전용 3D 미리보기를 요청합니다.

API 키, SDK DLL 및 오프라인 API 문서 캐시는 공개 저장소에 포함하지 않습니다. 생성된 API-reference 파일도 로컬 전용입니다. 자세한 동작과 검증 범위는 [영문 README](README.md)를 참고하십시오.
