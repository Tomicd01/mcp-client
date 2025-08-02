using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestiranjeKlijent
{
    internal static class Converter
    {
        public static ChatMessage ToFunctionResultMessage(this CallToolResult result, string toolCallId, string toolName)
        {
            var json = JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);

            var functionResultContent = new FunctionResultContent(toolCallId, json)
            {
                RawRepresentation = result
            };

            // Optional: extract human-readable text if possible
            string? humanText = null;
            if (result.Content.FirstOrDefault() is TextContentBlock textBlock)
            {
                humanText = textBlock.Text;
            }

            var contents = new List<AIContent> { functionResultContent };

            if (!string.IsNullOrEmpty(humanText))
            {
                contents.Add(new TextContent(humanText)); // This will appear in .Text
            }

            var message = new ChatMessage(ChatRole.Assistant, contents)
            {
                AuthorName = toolName,
                MessageId = toolCallId
            };

            return message;
        }
    }
}
