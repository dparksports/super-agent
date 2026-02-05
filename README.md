# OpenGemini for Windows

![OpenGemini Screenshot](Assets/screenshot.png)

OpenGemini is a powerful, native Windows application that brings the power of **Google Gemini** and **Local On-Device AI** (via ONNX Runtime GenAI) to your desktop.

Built with **WinUI 3** and **C#**, it demonstrates a modern Agentic architecture capable of autonomous tool execution, persistence, and hybrid model orchestration.

## Features

- **🧠 Hybrid AI Engine**: Seamlessly switch between cloud-based **Gemini 2.0 Flash/Pro** models and local **Phi-3/Llama-3** models running on-device.
- **🛠️ Agentic Tooling**: The AI can autonomously execute tools to perform tasks:
    - `get_system_time`: Check the local system time.
    - `read_file`: Securely read files from your Documents folder.
- **🔄 Autonomous Agent Loop**: Features a "Think-Act-Observe" loop where the agent can perform multiple steps to solve complex queries.
- **💾 Persistence**: Built-in SQLite database saves your conversation history, including tool outputs and execution logs, across app restarts.
- **💬 Slack Integration**: Communicate with your agent via Slack (optional).

## Getting Started

### Prerequisites

- Windows 10 (1809+) or Windows 11
- [.NET 8+ SDK](https://dotnet.microsoft.com/download)
- [Google Gemini API Key](https://aistudio.google.com/)

### Installation

1. Clone the repository.
2. Set your API Key:
   ```powershell
   setx GEMINI_API_KEY "your_api_key_here"
   ```
3. Build and Run:
   ```powershell
   dotnet build
   dotnet run
   ```

## Architecture

OpenGemini uses a clean, service-oriented architecture:

- **AgentOrchestrator**: Manages the core agent loop.
- **GoogleGeminiService**: Handles API communication, maintaining full conversation history (User/Model/Function turns).
- **ToolRegistry**: Manages available tools and generates JSON schemas for the LLM.
- **ChatContextDb**: Persists all events to `messages.db` in `%AppData%`.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

---
Made with ❤️ in California
