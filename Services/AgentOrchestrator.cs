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

        private OpenClaw.Windows.Models.FunctionCall? _pendingToolCall;

        public async IAsyncEnumerable<string> ChatAsync(string userMessage, ObservableCollection<ChatMessage> messageHistory)
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
                     
                     // Execute the pending tool
                     // But wait, the loop structure below starts fresh.
                     // We need to jump straight to execution phase or inject the logic.
                     // A cleaner way is to handle the *approval* as just another user message, 
                     // but logically we want to resume the *agent's* intention.
                     
                     // REFACTOR STRATEGY: 
                     // If we are approving, we just execute the tool and append the result. 
                     // Then we let the normal loop pick up the history (which now has user:yes, tool:result).
                     // But Gemini needs to see [Model: Call] -> [Tool: Result]. 
                     // "User: Yes" confuses that sequence if we are not careful.
                     
                     // SIMPLIFIED:
                     // If approved, we execute the tool immediately here, save the result, and THEN start the loop.
                     // The loop will load history which includes the new Tool Result, so Gemini picks up from there.
                     
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
                
                // Inject as a System/User instruction at the very beginning or right before the latest message.
                // Appending it to the list of parts of the *first* message is one way, 
                // but adding a dedicated "system" or "user" context message is cleaner.
                // Let's prepend it to the history if possible, or add it to the current user message context.
                
                // Option A: Add as a separate "user" message before the real user message.
                 geminiHistory.Add(new GeminiContent
                 {
                     Role = "user",
                     Parts = new List<GeminiPart> { new GeminiPart { Text = $"Context:\n{contextBlock}\n\n(Use this information to answer the user's next question if relevant.)" } }
                 });
                 
                 // Option B: Gemini 1.5 System Instruction (not implemented in this simplified client yet).
            }

            // 3. Add current user message
            await _db.SaveMessageAsync("User", userMessage); // Save to Chat Log
            
            // Auto-Save to Long-Term Memory (Fire and Forget)
            _ = _memoryService.SaveMemoryAsync($"User: {userMessage}");

            geminiHistory.Add(new GeminiContent
            {
                Role = "user",
                Parts = new List<GeminiPart> { new GeminiPart { Text = userMessage } }
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
                    foreach (var call in response.FunctionCalls)
                    {
                        var tool = _toolRegistry.GetTool(call.Name);
                        if (tool == null) 
                        {
                            await _db.SaveMessageAsync("Tool", "Tool not found", call.Name);
                            continue;
                        }

                        // SAFETY CHECK
                        if (tool.IsUnsafe)
                        {
                            _pendingToolCall = call;
                            // Add the Model's intent to history (so next time we see it asked)
                            await _db.SaveMessageAsync("Model", "", call.Name); 
                            
                            yield return $"[Agent] ⚠️ APPROVAL REQUIRED: {call.Name}\n";
                            yield return $"[Agent] Type 'yes' to approve or 'no' to deny.\n";
                            yield break; // STOP execution loop and wait for user input
                        }
                    
                        yield return $"[Agent]  🛠️ Executing {call.Name}...\n";
                        await _db.SaveMessageAsync("Model", "", call.Name); // Save tool call attempt (simplified)

                        // 1. Add the Model's Function Call request to history
                        // Note: Gemini expects the model's turn to preserve the function call id context conceptually,
                        // or just the structure. 
                        var modelPart = new GeminiPart 
                        { 
                            FunctionCall = new GeminiFunctionCall 
                            { 
                                Name = call.Name, 
                                Args = JsonSerializer.Deserialize<object>(call.JsonArgs) ?? new { } 
                            } 
                        };
                        
                        // We must append this to the history so Gemini knows it asked for it.
                        // However, we can't append multiple model parts if they came in one response easily without grouping.
                        // The simple loop assumes one turn = one response.
                        // If there are multiple function calls in one response, we should strictly add ONE model content with multiple parts.
                        // But our AgentResponse model flattens it slightly. Let's assume sequential or grouped.
                        // Simplified: Create a Model Content for this turn.
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
                        yield return response.Text;
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

            foreach (var msg in chatMessages)
            {
                if (msg.Role == "System") continue; // Skip system messages for now

                if (msg.Role == "Tool")
                {
                    // Map "Tool" role to Gemini "function" role
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

                // Handle Model (possibly Function Call) vs Model (Text)
                if (msg.Role == "Model" && !string.IsNullOrEmpty(msg.ToolCallId))
                {
                    // Reconstruct a placeholder Function Call
                    // Note: We currently don't store the original JSON args in the DB, only that a call occurred.
                    // We pass empty args {} to satisfy the schema. 
                    // In the future, we should store the JSON args in the Content field or a dedicated column.
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
                    continue;
                }

                // Standard Text Message (User or Model)
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
