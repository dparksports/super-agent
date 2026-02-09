using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace OpenClaw.Windows.Services;

public class OnnxLocalAiService : IAiService, IDisposable
{
    private Model? _model;
    private Tokenizer? _tokenizer;
    private readonly string _modelPath;
    private bool _isInitialized;

    public event Action<string, double>? DownloadProgressChanged;

    public OnnxLocalAiService()
    {
        // Model is stored in the "Model" subdirectory of the app
        string appDir = AppContext.BaseDirectory;
        _modelPath = Path.Combine(appDir, "Model");
        // Model path will be determined dynamically in InitializeAsync
    }

    private readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);

    public async Task InitializeAsync()
    {
        // Double-check locking pattern
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Model");
            
            // precise check for the data file which caused the crash
            bool modelExists = File.Exists(Path.Combine(modelPath, "model.onnx")) && 
                              File.Exists(Path.Combine(modelPath, "model.onnx.data"));

            if (!modelExists)
            {
                throw new FileNotFoundException("Model files not found. Please download the model in Settings.", modelPath);
            }

            try 
            {
                _model = new Model(modelPath);
                _tokenizer = new Tokenizer(_model);
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ONNX model: {ex.Message}");
                throw;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async IAsyncEnumerable<OpenClaw.Windows.Models.AgentResponse> GetStreamingResponseAsync(string systemPrompt, string userPrompt)
    {
        if (_model == null || _tokenizer == null)
        {
            await InitializeAsync();
        }

        var sequences = _tokenizer!.Encode($"<|system|>{systemPrompt}<|end|><|user|>{userPrompt}<|end|><|assistant|>");

        var generatorParams = new GeneratorParams(_model);
        generatorParams.SetSearchOption("max_length", 2048);
        
        using var generator = new Generator(_model, generatorParams);
        generator.AppendTokenSequences(sequences);

        while (!generator.IsDone())
        {
             string part = "";
             await Task.Run(() => 
             {
                 generator.GenerateNextToken();
                 // Decode only the last token
                 // Note: This logic assumes simple token-to-text mapping which is typical
                 // For robust streaming, ideally we keep track of previous length.
                 var outputSequences = generator.GetSequence(0);
                 var newToken = outputSequences[^1..];
                 part = _tokenizer.Decode(newToken);
             });

             yield return new OpenClaw.Windows.Models.AgentResponse { Text = part };
        }
    }

    public async Task RedownloadModelAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            _model?.Dispose();
            _tokenizer?.Dispose();
            _model = null;
            _tokenizer = null;
            _isInitialized = false;
            
            throw new NotSupportedException("Please use the Settings menu to download models.");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<OpenClaw.Windows.Models.AgentResponse> GenerateContentAsync(string prompt)
    {
        // Simple wrapper, not streaming
        var response = new StringBuilder();
        await foreach (var chunk in GetStreamingResponseAsync("", prompt))
        {
            if (chunk.Text != null)
            {
                response.Append(chunk.Text);
            }
        }
        return new OpenClaw.Windows.Models.AgentResponse { Text = response.ToString() };
    }

    public Task<OpenClaw.Windows.Models.AgentResponse> GenerateContentAsync(List<OpenClaw.Windows.Models.GeminiContent> history)
    {
        // Local model doesn't support full history yet, just use the last user message
        var lastMessage = history.Count > 0 && history[^1].Parts.Count > 0 ? history[^1].Parts[0].Text ?? "" : "";
        return GenerateContentAsync(lastMessage);
    }

    public async Task SwitchModelAsync(string modelPath)
    {
        await _initLock.WaitAsync();
        try
        {
            // Dispose existing
            _model?.Dispose();
            _tokenizer?.Dispose();
            _model = null;
            _tokenizer = null;
            _isInitialized = false;

            // For now, just reinitialize with the existing path
            await InitializeAsync();
        }
        finally
        {
            _initLock.Release();
        }
    }

    public void Dispose()
    {
        _model?.Dispose();
        _tokenizer?.Dispose();
        _initLock?.Dispose();
    }
}
