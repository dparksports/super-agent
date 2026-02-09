using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using OpenClaw.Windows.Models;
using OpenClaw.Windows.Services.Tools;

namespace OpenClaw.Windows.Services
{
    public class AgentOrchestrator
    {
        private readonly GoogleGeminiService _geminiService;
        private readonly ToolRegistry _toolRegistry;
        private readonly Data.ChatContextDb _db;
        private readonly MemoryService _memoryService;

        public AgentOrchestrator(GoogleGeminiService geminiService, ToolRegistry toolRegistry, Data.ChatContextDb db, MemoryService memoryService)
        {
            _geminiService = geminiService;
            _toolRegistry = toolRegistry;
            _db = db;
            _memoryService = memoryService;
            
            _ = InitializeDbAsync();
        }

        private async Task InitializeDbAsync()
        {
            await _db.InitializeAsync();
        }

        public async Task<List<ChatMessage>> LoadHistoryAsync()
        {
            return await _db.GetRecentMessagesAsync(20);
        }

        public async Task ClearHistoryAsync()
        {
            await _db.ClearMessagesAsync();
        }

        private OpenClaw.Windows.Models.FunctionCall? _pendingToolCall;

        public async IAsyncEnumerable<string> ChatAsync(string userMessage, ObservableCollection<ChatMessage> messageHistory, string? base64Image = null)
        {
            // 0. Check for Pending Tool Approval
            if (_pendingToolCall != null)
            {
                var pendingCall = _pendingToolCall;
                _pendingToolCall = null; // Clear state

                bool isApproved = userMessage.Trim().ToLower() == "yes" || userMessage.Trim().ToLower() == "approve";
                
                if (!isApproved)
                {
                     yield return $"[Agent] ❌ Tool execution denied.\n";
                     await _db.SaveMessageAsync("Tool", "User denied execution.", pendingCall.Name);

                     // Continue the loop with the denial response
                     // We need to reconstruct the history up to the point of the call
                     // Since ChatAsync is stateless per call regarding local variables, we rely on the DB/MessageHistory
                }
                else
                {
                     yield return $"[Agent] ✅ Tool approved. Executing...\n";
                     
                     var tool = _toolRegistry.GetTool(pendingCall.Name);
                     if (tool != null)
                     {
                        string result;
                        try { result = await tool.ExecuteAsync(pendingCall.JsonArgs); }
                        catch (Exception ex) { result = $"Error: {ex.Message}"; }
                        await _db.SaveMessageAsync("Tool", result, pendingCall.Name);
                        yield return $"[Agent]  ✅ Result: {Shorten(result)}\n";
                     }
                     else
                     {
                         yield return $"[Agent] ❌ Tool not found: {pendingCall.Name}\n";
                     }
                }
            }

            // 1. Build initial history from ChatMessage collection
            var geminiHistory = BuildGeminiHistory(messageHistory);

            // 2. RAG: Search and Inject Memories
            var memories = await _memoryService.SearchMemoriesAsync(userMessage);
            if (memories.Any())
            {
                var contextBlock = "### Long-Term Memory (Context from past conversations):\n" + 
                                   string.Join("\n", memories.Select(m => $"- {m.Content} ({m.Timestamp:g})"));
                 
                 geminiHistory.Add(new GeminiContent
                 {
                     Role = "user",
                     Parts = new List<GeminiPart> { new GeminiPart { Text = $"Context:\n{contextBlock}\n\n(Use this information to answer the user's next question if relevant.)" } }
                 });
            }

            // 3. Add current user message
            await _db.SaveMessageAsync("User", userMessage); // Save to Chat Log
            
            // Auto-Save to Long-Term Memory (Fire and Forget)
            _ = _memoryService.SaveMemoryAsync($"User: {userMessage}");

            var userParts = new List<GeminiPart>();
            if (!string.IsNullOrEmpty(base64Image))
            {
                userParts.Add(new GeminiPart 
                { 
                    InlineData = new GeminiInlineData 
                    { 
                        MimeType = "image/jpeg", 
                        Data = base64Image 
                    } 
                });
            }
            userParts.Add(new GeminiPart { Text = userMessage });

            geminiHistory.Add(new GeminiContent
            {
                Role = "user",
                Parts = userParts
            });

            // 4. Start Agent Loop
            int maxTurns = 5; // Safety break
            int currentTurn = 0;

            while (currentTurn < maxTurns)
            {
                currentTurn++;

                // Call Gemini
                var response = await _geminiService.GenerateContentAsync(geminiHistory);

                // Check for Tool Calls
                if (response.FunctionCalls != null && response.FunctionCalls.Any())
                {
                    // For each tool call
                    // Check HITL setting
                    bool hitlEnabled = SettingsHelper.Get<bool>("HitlEnabled", true);

                    foreach (var call in response.FunctionCalls)
                    {
                        var tool = _toolRegistry.GetTool(call.Name);
                        if (tool == null) 
                        {
                            await _db.SaveMessageAsync("Tool", "Tool not found", call.Name);
                            continue;
                        }

                        // Always notify the user BEFORE execution (transparency)
                        var argsPreview = call.JsonArgs.Length > 100 ? call.JsonArgs.Substring(0, 100) + "..." : call.JsonArgs;
                        yield return $"[Agent] 📋 Planning to use: **{call.Name}** with args: {argsPreview}\n";

                        // SAFETY CHECK — only block if HITL is enabled AND tool is unsafe
                        if (tool.IsUnsafe && hitlEnabled)
                        {
                            _pendingToolCall = call;
                            await _db.SaveMessageAsync("Model", "", call.Name); 
                            
                            yield return $"[Agent] ⚠️ APPROVAL REQUIRED: {call.Name}\n";
                            yield return $"[Agent] Type 'yes' to approve or 'no' to deny.\n";
                            yield break; // STOP and wait for user input
                        }
                    
                        yield return $"[Agent] 🛠️ Executing {call.Name}...\n";
                        await _db.SaveMessageAsync("Model", "", call.Name);
                    }
                    
                    // Add the 'model' turn with function calls to history
                    var modelContent = new GeminiContent
                    {
                        Role = "model",
                        Parts = response.FunctionCalls.Select(call => new GeminiPart
                        {
                            FunctionCall = new GeminiFunctionCall
                            {
                                Name = call.Name,
                                Args = JsonSerializer.Deserialize<object>(call.JsonArgs) ?? new { }
                            }
                        }).ToList()
                    };
                    geminiHistory.Add(modelContent);

                    // Execute Tools and Gather Responses
                    var functionResponseParts = new List<GeminiPart>();

                    foreach (var call in response.FunctionCalls)
                    {
                        var tool = _toolRegistry.GetTool(call.Name);
                        // Skip if unsafe/pending (already handled above via yield break)
                        // Wait, if it WAS unsafe, we yielded break. 
                        // So if we are here, it is SAFE.
                        
                        string result;

                        if (tool != null)
                        {
                            try
                            {
                                result = await tool.ExecuteAsync(call.JsonArgs);
                            }
                            catch (Exception ex)
                            {
                                result = $"Error executing tool: {ex.Message}";
                            }
                        }
                        else
                        {
                            result = $"Error: Tool '{call.Name}' not found.";
                        }

                        // Create Function Response Part
                        // The 'name' must match the function call name.
                        // The 'response' field accepts an object.
                        functionResponseParts.Add(new GeminiPart
                        {
                            FunctionResponse = new GeminiFunctionResponse
                            {
                                Name = call.Name,
                                Response = new { result = result } // Wrap in object as expected by some APIs, or just raw? Gemini usually likes structured.
                            }
                        });
                        
                        yield return $"[Agent]  ✅ Result: {Shorten(result)}\n";
                        await _db.SaveMessageAsync("Tool", result, call.Name);
                    }

                    // Add the 'function' turn to history
                    var functionContent = new GeminiContent
                    {
                        Role = "function",
                        Parts = functionResponseParts
                    };
                    geminiHistory.Add(functionContent);

                    // Loop continues to send this new history back to Gemini
                }
                else
                {
                    // No function calls, just text. We are done.
                    if (response.Text != null)
                    {
                        await _db.SaveMessageAsync("Model", response.Text);
                        
                        // Add Source Indicator for the user
                        if (currentTurn == 1) // Only on first turn? Or always?
                        {
                            // We can prepend a small badge
                            yield return $"[☁️ Gemini] {response.Text}";
                        }
                        else
                        {
                            yield return response.Text;
                        }
                    }
                    break;
                }
            }

            if (currentTurn >= maxTurns)
            {
               yield return "\n[Agent] 🛑 Max turns reached. Stopping loop.";
            }
        }

        private List<GeminiContent> BuildGeminiHistory(ObservableCollection<ChatMessage> chatMessages)
        {
            var history = new List<GeminiContent>();
            var messages = chatMessages.ToList();

            for (int i = 0; i < messages.Count; i++)
            {
                var msg = messages[i];
                if (msg.Role == "System") continue;

                if (msg.Role == "Tool")
                {
                    // Validation: A tool response must have had a preceding tool call.
                    // However, we can't easily valid backwards without lookbehind.
                    // But Gemini enforces: Model(Call) -> Function(Response).
                    // If we just add function responses that are valid, it's fine, 
                    // provided the *previous* turn was the call.
                    
                    if (!string.IsNullOrEmpty(msg.ToolCallId))
                    {
                        history.Add(new GeminiContent
                        {
                            Role = "function",
                            Parts = new List<GeminiPart> 
                            { 
                                new GeminiPart 
                                { 
                                    FunctionResponse = new GeminiFunctionResponse
                                    {
                                        Name = msg.ToolCallId,
                                        Response = new { content = msg.Content }
                                    }
                                } 
                            }
                        });
                    }
                    continue;
                }

                if (msg.Role == "Model" && !string.IsNullOrEmpty(msg.ToolCallId))
                {
                    // This is a Function Call.
                    // CRITICAL: We must check if the NEXT message is a Tool response for this ID.
                    // If not, we skip this message, because sending a Call without a Response breaks the API.
                    
                    bool hasResponse = false;
                    if (i + 1 < messages.Count)
                    {
                        var nextMsg = messages[i + 1];
                        if (nextMsg.Role == "Tool" && nextMsg.ToolCallId == msg.ToolCallId)
                        {
                            hasResponse = true;
                        }
                    }

                    if (hasResponse)
                    {
                        history.Add(new GeminiContent
                        {
                            Role = "model",
                            Parts = new List<GeminiPart> 
                            { 
                                new GeminiPart 
                                { 
                                    FunctionCall = new GeminiFunctionCall
                                    {
                                        Name = msg.ToolCallId,
                                        Args = new { } 
                                    }
                                } 
                            }
                        });
                    }
                    else
                    {
                        // Skip orphaned call
                        System.Diagnostics.Debug.WriteLine($"[History] Skipped orphaned tool call: {msg.ToolCallId}");
                    }
                    continue;
                }

                // Standard Text
                string role = msg.Role.ToLower() == "user" ? "user" : "model";
                history.Add(new GeminiContent
                {
                    Role = role,
                    Parts = new List<GeminiPart> { new GeminiPart { Text = msg.Content } }
                });
            }

            return history;
        }
        
        private string Shorten(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return input.Length > 50 ? input.Substring(0, 50) + "..." : input;
        }
    }
}
