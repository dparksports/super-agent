# Super Agent 🦸‍♂️

![Super Agent Dashboard](Assets/dashboard_v2.png)

> **[📥 Download v1.2.1 (Win-x64)](https://github.com/dparksports/open-gemini/releases/download/v1.2.1/super-agent-v1.2.1-win-x64.zip)**

**Super Agent** is a next-generation AI assistant for Windows that combines the power of cloud-based reasoning with the privacy and speed of local inferencing.

It is designed to be your **proactive digital sidekick**, living in your system tray, watching your files, and extending its capabilities through a dynamic skill system—compatible with the OpenClaw ecosystem.

## 🌟 Key Features

### 🧩 Dynamic Skills Ecosystem (New!)
- **OpenClaw Compatible**: Simply drop existing OpenClaw skills into `%APPDATA%\OpenClaw\Skills`.
- **Polyglot Runtime**: Supports both **PowerShell** (`.ps1`) for deep Windows automation and **Python** (`.py`) for data processing.
- **Hot-Reload**: Add new skills on the fly without restarting the agent.

### 🧠 Hybrid AI Architecture
- **Cloud Power**: Uses **Google/Azure/OpenAI/Grok** for complex reasoning, coding, and creative tasks.
- **Local Speed**: Uses SOTA Local LLMs for fast, offline chat and simple queries.
- **Smart Routing**: Automatically routes requests to the best model based on complexity and privacy settings.

### 🤖 Autonomous & Proactive
- **System Tray Agent**: Lives exclusively in the tray. Minimizes out of your way but is always one click away.
- **Background Heartbeat**: Runs a background service loop to monitor tasks and health.
- **File Sensors**: Watches your file system for changes to proactively offer help (e.g., "I see you added a new PDF, want me to summarize it?").

### 💾 Long-Term Memory (RAG)
- **Never Forgets**: Stores conversations and memories in a local SQLite vector database.
- **Semantic Search**: Automatically retrieves relevant past interactions to provide context-aware answers.
- **Privacy-First**: Your memories live on your device, not on a remote server.

### 👁️ Vision & Perception
- **Drag & Drop Vision**: Drop images into the chat to analyze them with LVM.
- **Local OCR**: Extract text from images locally using LVM (offline & private).
- **Local Whisper**: Transcribe audio/video files locally.

## 🚀 Getting Started

### Prerequisites
- Windows 10 (Build 19041) or higher.
- [Gemini API Key](https://aistudio.google.com/) (Required for Cloud/Vision features).

### Installation
1.  **Download** the latest release or clone the repository.
2.  **Run** `super-agent.exe`.
3.  **Configure**: On first launch, enter your Gemini API Key in `secrets.json` or allow the agent to prompt you.
4.  **Skills**:
    - Navigate to `%APPDATA%\OpenClaw\Skills`.
    - Create a folder (e.g., `CheckWeather`) and add your `script.ps1`.
    - Click the 🧩 icon in the app to manage them.

## 🛠️ Tech Stack
- **UI**: WinUI 3 (Windows App SDK)
- **Language**: C# / .NET 10
- **AI Orchestration**: Semantic Kernel
- **Local Inference**: ONNX Runtime GenAI, Whisper.net
- **Database**: SQLite + Vector Embeddings

## 🔮 Roadmap
- [ ] **Voice Interaction**: Full real-time speech loop.
- [ ] **Calendar Integration**: Native Outlook/Google Calendar support.
- [ ] **Remote Gateway**: Secure control via mobile.

---
*Made with ❤️ in California*
