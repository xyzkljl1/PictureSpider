using LLama.Common;
using LLama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLama.Sampling;
using LLama.Native;

namespace PictureSpider
{
    public static class GrammarDefinitions
    {
        /// <summary>
        /// 通用 JSON 对象（允许任意结构）
        /// 可以从 LLamaSharp 仓库的 Assets/json.gbnf 获取
        /// </summary>
        public const string GenericJson = """
        root   ::= object
        value  ::= object | array | string | number | ("true" | "false" | "null") ws

        object ::=
          "{" ws (
            string ":" ws value
            ("," ws string ":" ws value)*
          )? "}" ws

        array  ::=
          "[" ws (
            value
            ("," ws value)*
          )? "]" ws

        string ::=
          "\"" (
            [^\\"\x7F\x00-\x1F] |
            "\\" (["\\/bfnrt] | "u" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F])
          )* "\"" ws

        number ::= ("-"? ([0-9] | [1-9] [0-9]*)) ("." [0-9]+)? ([eE] [-+]? [0-9]+)? ws

        ws ::= ([ \t\n] ws)?
        """;

        /// <summary>
        /// 精确匹配 FileParseResult 的固定字段结构
        /// </summary>
        public const string FileParseResultJson = """
        root ::= "{" ws
            "\"author\"" ws ":" ws string-or-null "," ws
            "\"title\"" ws ":" ws string-or-null "," ws
            "\"author_original\"" ws ":" ws string-or-null "," ws
            "\"confidence\"" ws ":" ws number "," ws
            "\"notes\"" ws ":" ws string-or-null
            ws "}"

        string-or-null ::= string | "null"

        string ::=
          "\"" (
            [^\\"\x7F\x00-\x1F] |
            "\\" (["\\/bfnrt] | "u" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F])
          )* "\""

        number ::= ("0" | "1" | "0." [0-9]+)

        ws ::= ([ \t\n] ws)?
        """;
    }
    // 本地模型
    internal class LLMChatService: IDisposable
    {
        private readonly LLamaWeights model;
        private readonly LLamaContext context;
        private readonly ModelParams param;
        private string modelPath = "E:\\MyWebsiteHelper\\PictureSpider\\PictureSpider\\AI\\models\\qwen2.5-7b-instruct-q4_k_m-00001-of-00002.gguf";
        public LLMChatService()
        {
            /*
             * 日志
            NativeLibraryConfig.All.WithLogCallback((level, message) =>
            {
                if (message.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("CUDA", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Metal", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Vulkan", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("offload", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("VRAM", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("device", StringComparison.OrdinalIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[llama.cpp] {message}");
                    Console.ResetColor();
                }
            });*/
            param = new ModelParams(modelPath)
            {
                ContextSize = 2048,
                GpuLayerCount = 20 // 0=纯CPU，>0=GPU加速层数
            };
            // 此任务执行频率低，仅在执行时加载模型，避免平时占用显存；
            model = LLamaWeights.LoadFromFile(param);
            context = model.CreateContext(param);
            Console.WriteLine($"model loaded:{Path.GetFileName(modelPath)} {model.ParameterCount / 1_000_000_000.0:F1}B");
        }
        public async Task<string> ChatStatelessAsync(string systemPrompt, string userMessage)
        {
            //StatelessExecutor
            //var executor = new InteractiveExecutor(context);
            var executor = new StatelessExecutor(model, param);
            // 构建 ChatML 格式的 prompt
            var prompt = $"""
            <|im_start|>system
            {systemPrompt}<|im_end|>
            <|im_start|>user
            {userMessage}<|im_end|>
            <|im_start|>assistant
            """;

            //  指定了grammer，返回值仍然可能不符合json格式，只能通过反复重试解决了
            var grammar = new Grammar(GrammarDefinitions.GenericJson, "root");
            using var samplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.9f,
                Grammar = grammar
            };

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 512,
                AntiPrompts = new[] { "<|im_end|>", "<|im_start|>" },
                SamplingPipeline = samplingPipeline
            };

            var result = new System.Text.StringBuilder();

            await foreach (var token in executor.InferAsync(prompt, inferenceParams))
            {
                result.Append(token);
            }

            return result.ToString().Trim();
        }
        public void Dispose()
        {
            Console.WriteLine($"model unloaded:{Path.GetFileName(modelPath)}");
            model?.Dispose();
            context?.Dispose();
        }
    }
}
