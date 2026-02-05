# OpenGemini for Windows

![OpenGemini Screenshot](Assets/screenshot.png)

OpenGemini is a powerful, native Windows application that brings the power of **Google Gemini** and **Local On-Device AI** (via ONNX Runtime GenAI) to your desktop.

It features a **Phase 1 "Super Agent"** architecture, giving the AI the ability to interact with your system while keeping you in full control.

## 🦾 Super Agent Features

### 1. Expanded Action Space
The agent can now "touch" the world, not just look at it:
- **`write_file`**: Can create, edit, and overwrite files in your Documents folder.
- **`run_powershell`**: Can execute system commands (e.g., `dotnet build`, `dir`, `ping`).
- **`read_file`**: Securely read files from your Documents folder.
- **`get_system_time`**: Check the local system time.

### 2. 🛡️ Human-in-the-Loop Safety System
We believe in **Safe AI**. The agent is **incapable** of executing high-risk tools autonomously.
- **Approval Required**: If the agent wants to write a file or run a command, it **pauses execution**.
- **Transparency**: It clearly states usage: `⚠️ APPROVAL REQUIRED: run_powershell`.
- **Control**: You must explicitly type **`yes`** or **`approve`** to authorize the action.

### 3. Agentic Loop & Persistence
- "Think-Act-Observe" loop allows for complex multi-step reasoning.
- **SQLite Database** (`messages.db`) automatically saves your entire conversation history, including tool outputs and missed approvals.

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

- **AgentOrchestrator**: Manages the core loop and safety checks.
- **GoogleGeminiService**: Handles multi-turn Agent/Function history.
- **ToolRegistry**: Manages tool definitions (Unsafe vs Safe).
- **ChatContextDb**: Local persistence layer.

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

---
Made with ❤️ in California
