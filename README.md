# Super Agent 🦸‍♂️

![Super Agent Screenshot](Assets/screenshot.png)

**Super Agent 🦸‍♂️** is a powerful, native Windows application that brings the power of **Google Gemini** and **Local On-Device AI** to your desktop.

It is designed as a **Thinking Agent** with long-term memory and system control capabilities.

## 🧠 Brain Features (Phase 2)

### 1. Long-Term Memory (RAG)
The agent never forgets. It uses a **Retrieval-Augmented Generation** system to recall past conversations.
- **Embeddings**: Uses `gemini-embedding-004` to vectorize every message.
- **Vector Database**: specific memories are stored in a local SQLite database (`messages.db`).
- **Context Injection**: Relevant past information is automatically retrieved and provided to the agent before it answers.

### 2. Expanded Action Space (Phase 1)
The agent can "touch" the world:
- **`write_file`**: Can create and edit files in your Documents folder.
- **`run_powershell`**: Can execute system commands.
- **`read_file`**: Securely read files.

### 3. 🛡️ Human-in-the-Loop Safety
The agent is **incapable** of executing high-risk tools autonomously.
- **Approval Required**: Unsafe actions (like writing files) pause execution.
- **Transparency**: You see exactly what tool is being requested.
- **Control**: You must type **`yes`** to authorize the action.

## Getting Started

### Prerequisites
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Google Gemini API Key](https://aistudio.google.com/)

### Installation
1. Clone the repo.
2. Set API Key: `setx GEMINI_API_KEY "your_key"`
3. Run: `dotnet run`

## License
Apache License 2.0 - Made with ❤️ in California
