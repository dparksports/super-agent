using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpenClaw.Windows.Services;

public class ModelDownloadService
{
    private readonly HttpClient _httpClient;
    
    // Most ONNX GenAI models need these files.
    private static readonly string[] RequiredFiles = 
    {
        "model.onnx",
        "model.onnx.data",
        "tokenizer.json",
        "tokenizer_config.json",
        "genai_config.json",
        "special_tokens_map.json",
        "added_tokens.json"
    };

    public ModelDownloadService()
    {
        _httpClient = new HttpClient();
    }

    public async Task DownloadModelAsync(OpenClaw.Windows.Models.ModelConfig modelConfig, IProgress<string>? statusProgress, IProgress<double>? downloadProgress)
    {
        string destinationFolder = modelConfig.GetLocalPath();
        
        // Base URL construction for Hugging Face "resolve/main"
        // Valid input RepoUrl: "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx"
        // We need to append "/resolve/main/directml/directml-int4-awq-block-128/" or similar based on the model.
        // For simplicity, we will assume RepoUrl in ModelConfig IS the base DirectML folder URL.
        string baseUrl = modelConfig.RepoUrl;
        if (!baseUrl.EndsWith("/")) baseUrl += "/";

        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        int totalFiles = RequiredFiles.Length;
        int downloadedFiles = 0;

        downloadProgress?.Report(0);

        foreach (var fileName in RequiredFiles)
        {
            string filePath = Path.Combine(destinationFolder, fileName);
            string url = baseUrl + fileName;

            long? expectedSize = null;
            try 
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead);
                if (headResponse.IsSuccessStatusCode)
                {
                    expectedSize = headResponse.Content.Headers.ContentLength;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HEAD request failed for {fileName}: {ex.Message}");
            }

            if (File.Exists(filePath))
            {
                var localSize = new FileInfo(filePath).Length;
                bool isSizeMatch = expectedSize.HasValue && localSize == expectedSize.Value;
                
                // If HEAD failed (null), we assume mismatch to be safe and force check/download
                // OR if explicit mismatch.
                if (isSizeMatch)
                {
                    downloadedFiles++;
                    downloadProgress?.Report((double)downloadedFiles / totalFiles * 100);
                    continue;
                }
                
                System.Diagnostics.Debug.WriteLine($"File mismatch for {fileName}: Local={localSize}, Server={expectedSize}. Deleting and Re-downloading.");
                try { File.Delete(filePath); } catch {}
            }

            statusProgress?.Report($"Downloading {fileName}...");

            // Create a progress reporter for this specific file that updates the overall progress
            var currentFileIndex = downloadedFiles; 
            var fileProgress = new Progress<double>(percentComplete =>
            {
                // Calculate overall progress: 
                // (Files Completed * 100 + Current File %) / Total Files
                double overall = ((double)currentFileIndex * 100 + percentComplete) / totalFiles;
                downloadProgress?.Report(overall);
                
                // Optional: Update status text with MB details if we passed that in, but for now stick to % bar
            });

            await DownloadFileAsync(url, filePath, fileProgress);
            
            downloadedFiles++;
            // Ensure we hit the exact boundary for the next file
            downloadProgress?.Report((double)downloadedFiles / totalFiles * 100);
        }

        statusProgress?.Report("Model download complete.");
        downloadProgress?.Report(100);
    }

    private async Task DownloadFileAsync(string url, string outputPath, IProgress<double> fileProgress)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        long totalRead = 0;
        byte[] buffer = new byte[8192];
        bool isMoreToRead = true;

        using Stream contentStream = await response.Content.ReadAsStreamAsync();
        using FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        do
        {
            int read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
            if (read == 0)
            {
                isMoreToRead = false;
            }
            else
            {
                await fileStream.WriteAsync(buffer, 0, read);
                totalRead += read;

                if (totalBytes.HasValue)
                {
                    fileProgress?.Report((double)totalRead / totalBytes.Value * 100);
                }
            }
        }
        while (isMoreToRead);
    }
}
