using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Chat;
using System.ClientModel;
using System.Security.Cryptography;
using System.Text.Json;
using TestiranjeKlijent;

#pragma warning disable S103 // Lines should not be too long
#pragma warning disable S1067 // Expressions should not be too complex
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
#pragma warning disable CA1305 // Specify IFormatProvider
#pragma warning disable S1135 // Track uses of "TODO" tags


// Connect to an MCP server
Console.WriteLine("Connecting client to MCP 'everything' server");

// Create OpenAI client (or any other compatible with IChatClient)
// Provide your own OPENAI_API_KEY via an environment variable.
OpenAIClient openAIClient = new OpenAIClient(new ApiKeyCredential(""),
    options: new OpenAIClientOptions()
    {
        Endpoint = new Uri("https://api.moonshot.ai/v1"),
    });

// Create a sampling client.
using IChatClient samplingClient = openAIClient.GetChatClient("kimi-k2-0711-preview").AsIChatClient();

var mcpClient = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "dotnet",
        Arguments = ["run", "--project", "D:\\Test Poslovni folder\\Testiranje"],
        Name = "blog-mcp-server",
    }),
    clientOptions: new()
    {
        Capabilities = new() { Sampling = new() { SamplingHandler = samplingClient.CreateSamplingHandler() } },
    });

// Get all available tools
Console.WriteLine("Tools available:");
var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"  {tool}");
}

Console.WriteLine();

// Create an IChatClient that can use the tools.
using IChatClient chatClient = openAIClient.GetChatClient("kimi-k2-0711-preview").AsIChatClient();


// Have a conversation, making all tools available to the LLM.
List<Microsoft.Extensions.AI.ChatMessage> messages = [];


while(true)
{
    bool hadToolCall;
    Console.Write("Q: ");
    messages.Add(new(ChatRole.User, Console.ReadLine()));
    do
    {
        hadToolCall = false;
        /*List<ChatResponseUpdate> updates = [];
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, new() { Tools = [.. tools], AllowMultipleToolCalls = false }))
        {
            Console.Write(update);
            updates.Add(update);
        }
        Console.WriteLine(updates);

        messages.AddMessages(updates);*/

        var completion = await chatClient.GetResponseAsync(messages, new() { Tools = [.. tools] });

        if (completion.FinishReason.Equals(Microsoft.Extensions.AI.ChatFinishReason.Stop))
        {
            messages.Add(new(ChatRole.Assistant, completion.Text));
            Console.WriteLine("A: " + completion.Text);
            break;
        }
        if (completion.FinishReason.Equals(Microsoft.Extensions.AI.ChatFinishReason.ToolCalls))
        {
            if (completion.RawRepresentation is ChatCompletion chatCompletion)
            {
                foreach (ChatToolCall toolCall in chatCompletion.ToolCalls)
                {
                    switch (toolCall.FunctionName)
                    {
                        case "get_database_schema":
                            {
                                var result = await mcpClient.CallToolAsync("get_database_schema");
                                var toolResultMessage = result.ToFunctionResultMessage(toolCall.Id, toolCall.FunctionName);
                                messages.Add(toolResultMessage);

                                break;
                            }
                        case "ReturnBlogUrl":
                            {
                                using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);
                                bool hasLocation = argumentsJson.RootElement.TryGetProperty("id", out JsonElement id);
                                var result = await mcpClient.CallToolAsync("ReturnBlogUrl", new Dictionary<string, object> { ["id"] = id });
                                // 1. Inject the protocol-level function result so the model knows the tool_call was handled
                                var toolResultMessage = result.ToFunctionResultMessage(toolCall.Id, toolCall.FunctionName);
                                messages.Add(toolResultMessage);

                                break;
                            }
                        default:
                            {
                                // Handle other unexpected calls.
                                throw new NotImplementedException();
                            }

                    }
                }
            }
            hadToolCall = true;
        }
    } while (hadToolCall);
}