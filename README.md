# Super Agent 🦸‍♂️

![Super Agent Dashboard](/super_agent_final_dashboard_1770282859819.png)

> **[📥 Download Latest Release](../../releases/latest)**

**Super Agent** is a next-generation AI assistant for Windows that combines the power of cloud-based reasoning (Gemini 2.0 Flash) with the privacy and speed of local computing (OnnxRuntime, Windows Media OCR, Whisper.net).

It is designed to be your **proactive digital sidekick**, capable of remembering your context, browsing the web, and perceiving the world through vision and audio—independent of the cloud when needed.

## 🌟 Key Features

### 🧠 Hybrid AI Architecture
- **Cloud Power**: Uses **Google Gemini 2.0 Flash** for complex reasoning, coding, and creative tasks.
- **Local Speed**: Uses **Microsoft Phi-3 / Llama 3** (via ONNX) for fast, offline chat and simple queries.
- **Smart Routing**: automatically routes requests to the best model based on complexity and privacy settings.

### 💾 Long-Term Memory (RAG)
- **Never Forgets**: Stores conversations and memories in a local SQLite vector database.
- **Semantic Search**: Automatically retrieves relevant past interactions to provide context-aware answers.
- **Privacy-First**: Your memories live on your device, not on a remote server.

### 🌐 Web & Research Capabilities
- **Web Surfer**: Can search the web (Google/Bing) and read page content to answer current questions.
- **Smart Parsing**: Intelligent extraction of article text, bypassing ads and clutter.

### 👁️ Vision & Perception (Multi-Modal)
- **Drag & Drop Vision**: Drop images into the chat to analyze them with Gemini Vision.
- **Local OCR**: Extract text from images locally using Windows built-in OCR engine (offline & private).
- **Local Whisper**: Transcribe audio and video files (`.wav`, `.mp4`) locally using `Whisper.net`.

### 🛡️ Safety & Security
- **Human-in-the-Loop**: Critical actions (like file system writes or PowerShell execution) require your explicit approval.
- **Sandboxed**: File operations are restricted to your User Documents folder.

## 🚀 Getting Started

### Prerequisites
- Windows 10 (Build 19041) or higher.
- [Gemini API Key](https://aistudio.google.com/) (Required for Cloud/Vision features).

### Installation
1.  Clone this repository.
2.  Open `super-agent.sln` in Visual Studio 2022.
3.  Build and Run (F5).
4.  On first launch, enter your Gemini API Key in `secrets.json` or environment variables.
5.  *Optional*: Local models will be downloaded automatically on first use (~2GB for LLM, ~140MB for Whisper).

## 🛠️ Tech Stack
- **UI**: WinUI 3 (Windows App SDK)
- **Language**: C# / .NET 10
- **AI Orchestration**: Semantic Kernel
- **Local Inference**: ONNX Runtime GenAI, Whisper.net
- **Database**: SQLite + Vector Embeddings

## 🔮 Future Roadmap
- [ ] **Real-time Voice Mode**: Talk to your agent naturally.
- [ ] **Agentic Workflows**: Multi-step complex task execution.
- [ ] **Plugin System**: Community-driven tools extensions.

---
*Made with ❤️ in California*
