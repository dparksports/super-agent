using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using OpenClaw.Windows.Models;

namespace OpenClaw.Windows.Services.Data
{
    public class ChatContextDb
    {
        private readonly string _dbPath;

        public ChatContextDb()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenClaw");
            Directory.CreateDirectory(folder);
            _dbPath = Path.Combine(folder, "messages.db");
        }

        public async Task InitializeAsync()
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = 
            @"
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Role TEXT NOT NULL,
                    Content TEXT,
                    ToolCallId TEXT,
                    Timestamp TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Memories (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Content TEXT NOT NULL,
                    Vector TEXT, 
                    Timestamp TEXT NOT NULL
                );
            ";
            await command.ExecuteNonQueryAsync();
        }

        public async Task SaveMessageAsync(string role, string content, string? toolCallId = null)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = 
            @"
                INSERT INTO Messages (Role, Content, ToolCallId, Timestamp)
                VALUES ($role, $content, $toolCallId, $timestamp);
            ";
            command.Parameters.AddWithValue("$role", role);
            command.Parameters.AddWithValue("$content", content ?? "");
            command.Parameters.AddWithValue("$toolCallId", (object?)toolCallId ?? DBNull.Value);
            command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<ChatMessage>> GetRecentMessagesAsync(int count = 20)
        {
            var messages = new List<ChatMessage>();

            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = 
            @"
                SELECT Role, Content, ToolCallId, Timestamp FROM (
                    SELECT Role, Content, ToolCallId, Timestamp FROM Messages
                    ORDER BY Timestamp DESC
                    LIMIT $count
                )
                ORDER BY Timestamp ASC;
            ";
            command.Parameters.AddWithValue("$count", count);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var role = reader.GetString(0);
                var content = reader.IsDBNull(1) ? "" : reader.GetString(1);
                var toolCallId = reader.IsDBNull(2) ? null : reader.GetString(2);
                var timestampStr = reader.GetString(3);

                var msg = new ChatMessage(role, content)
                {
                    ToolCallId = toolCallId ?? "",
                    Timestamp = DateTime.Parse(timestampStr)
                };
                messages.Add(msg);
            }

            return messages;
        }

        public async Task SaveMemoryAsync(string content, float[] vector)
        {
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = 
            @"
                INSERT INTO Memories (Content, Vector, Timestamp)
                VALUES ($content, $vector, $timestamp);
            ";
            command.Parameters.AddWithValue("$content", content);
            command.Parameters.AddWithValue("$vector", System.Text.Json.JsonSerializer.Serialize(vector));
            command.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));

            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<MemoryItem>> GetAllMemoriesAsync()
        {
            var list = new List<MemoryItem>();
            using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Content, Vector, Timestamp FROM Memories";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var vectorJson = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
                var vector = System.Text.Json.JsonSerializer.Deserialize<float[]>(vectorJson) ?? Array.Empty<float>();

                list.Add(new MemoryItem
                {
                    Id = reader.GetInt32(0),
                    Content = reader.GetString(1),
                    Vector = vector,
                    Timestamp = DateTime.Parse(reader.GetString(3))
                });
            }
            return list;
        }
    }
}
