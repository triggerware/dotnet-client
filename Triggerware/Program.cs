using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json.Schema.Generation;
using OpenAI;
using OpenAI.Chat;
using Triggerware;

var client = new TriggerwareClient(IPAddress.Loopback, 5221);
client.Start();

var key = File.ReadAllText("/home/aiden/Work/aiden2/keys/openai").Trim();
var openAi = new OpenAIClient(key).GetChatClient("gpt-4o-mini-2024-07-18");

var api = new ApiThing(client, openAi);
 
// var parser = new Triggerware.SqlParser();
var sql =
    """ select * from yellowpages where searchfor = 'restaurant' and location = 'santa monica';  """;

var view = client.ExecuteQuery<object>(new SqlQuery(sql));
    

// parser.Main(sql, [api.RelDataElements[46], api.RelDataElements[47]]);
// return;





var result1 = api.FirstPrompt("all restaurants less than 20 miles from santa monica");
if (result1 == null)
    return;

Console.WriteLine(result1.description);
foreach (var i in result1.indices)
    Console.WriteLine(i);
foreach (var i in result1.names)
    Console.WriteLine(i);

return;
var result2 = api.SecondPrompt(result1);
if (result2 == null)
    return;

Console.WriteLine(result2.query);

internal class ApiThing
{
    public TriggerwareClient Client;
    public BinaryData FirstPromptSchemaBytes;
    public ChatClient OpenAi;
    public RelDataElement[] RelDataElements;
    public BinaryData SecondPromptSchemaBytes;

    public ApiThing(TriggerwareClient client, ChatClient openAi)
    {
        Client = client;
        OpenAi = openAi;
        var generator = new JSchemaGenerator();
        var schema1 = generator.Generate(typeof(FirstPromptResult));
        var schema2 = generator.Generate(typeof(SecondPromptResult));
        FirstPromptSchemaBytes = BinaryData.FromString(schema1.ToString());
        SecondPromptSchemaBytes = BinaryData.FromString(schema2.ToString());
        RelDataElements = client.GetRelData().SelectMany(x => x.Elements).ToArray();
    }

    public FirstPromptResult? FirstPrompt(string input)
    {
        var prompt =
            $"""
                The following text is an english language query. From the list of tables found below, rephrase what the
                user is looking for in more detail, starting with (first person) "I want...". Then provide a list of
                names of the selected tables the user would need in a query, and then a list of their indices.
                <TABLES>
                {ProcessRelData(RelDataElements)}
                </TABLES>
             """;
        var chat = new List<ChatMessage> { new SystemChatMessage(prompt), new UserChatMessage(input) };
        var completion = OpenAi.CompleteChat(chat, new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("result", FirstPromptSchemaBytes)
        });

        var text = completion.Value.Content.ToList().Select(x => x.Text).Aggregate((x, acc) => acc + x);
        return JsonSerializer.Deserialize<FirstPromptResult>(text);
    }

    public SecondPromptResult? SecondPrompt(FirstPromptResult input)
    {
        var prompt =
            $"""
                The following text is a request for some data. Provided are relevant tables the user may need for their
                query. Each table is listed as json data with fields 'name', 'description', 'columns', and
                'columnTypes'. You will return a json object with one field 'query', a SQL query that returns the 
                information the user wants. The query must be valid with the tables provided. Don't leave any of what
                the user wants out. 
                Only use simple syntax - keep it to 'where' and 'and'.
                <TABLES>
                {ProcessTables(RelDataElements, input.indices)}
                </TABLES>
             """;
        var chat = new List<ChatMessage> { new SystemChatMessage(prompt), new UserChatMessage(input.description) };
        var completion = OpenAi.CompleteChat(chat, new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("result", SecondPromptSchemaBytes)
        });

        var text = completion.Value.Content.ToList().Select(x => x.Text).Aggregate((x, acc) => acc + x);
        try
        {
            Console.WriteLine("chatgpt says " + text);
            var js = JsonSerializer.Deserialize<SecondPromptResult>(text);
            Console.WriteLine(js?.query is "" or null);
            return js;
        }
        catch (Exception e)
        {
            Console.WriteLine("failed deserializing " + text);
            Console.WriteLine(e.Message);
            throw;
        }
    }

    public string ProcessRelData(RelDataElement[] elements)
    {
        var result = elements.Select((e, i) =>
        {
            var result = i + ": ";
            result += e.Name + ", ";
            if (e.Description == "")
                result += "no description\n";
            else
                result += e.Description + "\n";

            return result;
        }).Aggregate((x, acc) => acc + x);

        Console.WriteLine(result);
        return result;
    }

    public string ProcessTables(RelDataElement[] elements, int[] indices)
    {
        var result = elements
            .Where((x, i) => indices.Contains(i))
            .Select(x => new TableDescription
            {
                name = x.Name,
                description = x.Description,
                columns = x.SignatureNames,
                columnTypes = x.SignatureTypes
            })
            .Select(x => JsonSerializer.Serialize(x))
            .Aggregate((x, acc) => acc + ",\n" + x);

        Console.WriteLine(result);
        return result;
    }
}

internal class FirstPromptResult
{
    [JsonPropertyName("description")] public string description { get; set; }

    [JsonPropertyName("names")] public string[] names { get; set; }

    [JsonPropertyName("indices")] public int[] indices { get; set; }
}

internal class SecondPromptResult
{
    [JsonPropertyName("query")] public string query { get; set; }
}

internal class TableDescription
{
    [JsonPropertyName("name")] public string name { get; set; }

    [JsonPropertyName("description")] public string description { get; set; }

    [JsonPropertyName("columns")] public string[] columns { get; set; }

    [JsonPropertyName("columnTypes")] public string[] columnTypes { get; set; }
}

// var sql = new SqlQuery(""" select * from inflation where year1=:y1 and year2=1995; """);
// var fol = new FolQuery(""" ((x) s.t. (E (y z) (AND (TWEETS-FROM "upsettrout" x y z)))) """);
// 
// var view = new View<object[]>(client, fol);
// var prepared = new PreparedQuery<object[]>(client, sql);
// Console.WriteLine(string.Join(",", prepared.InputSignatureNames));
// prepared.SetParameter("?y1", 1991);
// 
// var resultSet = view.Execute();
// 
// Console.WriteLine(string.Join(",", resultSet.Pull(3).SelectMany(x => x).Select(x => x.ToString())));

// var elements = relData.SelectMany(x => x.Elements).ToList();
// var relData = client.GetRelData();
// elements.Sort((a,b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
// foreach (var e in elements)
// {
//     Console.WriteLine(e.Name);
//     Console.WriteLine($"  {string.Join(", ", e.SignatureNames)}");
//     Console.WriteLine($"  {string.Join(", ", e.SignatureTypes)}");
// }

// var assembly = Assembly.Load(typeof(TriggerwareClient).Assembly.FullName!);
// 
// foreach (var file in Directory.GetFiles("/home/aiden/Work/aiden2/tests", "*.json"))
// {
//     var className = Path.GetFileNameWithoutExtension(file);
//     var fullTypeName = $"Triggerware.{className}";
// 
//     var type = assembly.GetType(fullTypeName);
//     if (type is null)
//     {
//         Console.WriteLine($"Could not find type '{className}'.");
//         continue;
//     }
// 
//     var content = File.ReadAllText(file);
// 
//     try
//     {
//         var myObject = JsonSerializer.Deserialize(content, type);
//         Console.WriteLine($"finished {myObject}");
//     }
//     catch (JsonException ex)
//     {
//         Console.WriteLine($"Failed to parse JSON in {file}: {ex.Message}");
//     
// }