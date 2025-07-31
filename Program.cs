using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

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
        Endpoint = new Uri("https://api.moonshot.ai/v1")
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
while (true)
{
    Console.Write("Q: ");
    messages.Add(new(ChatRole.User, Console.ReadLine()));

    List<ChatResponseUpdate> updates = [];
    await foreach (var update in chatClient.GetStreamingResponseAsync(messages, new() { Tools = [.. tools] }))
    {
        Console.Write(update);
        updates.Add(update);
    }
    Console.WriteLine();

    messages.AddMessages(updates);
}