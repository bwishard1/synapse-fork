using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text;
using Newtonsoft.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Synapse.Api.Client;
using ServerlessWorkflow.Sdk.Models;
using ServerlessWorkflow.Sdk.Models.Tasks; 
using Neuroglia;
using ServerlessWorkflow.Sdk; 

namespace Synapse.Cli.Commands.Workflows;

public class WorkflowResource
{
    public string ApiVersion { get; set; }
    public string Kind { get; set; }
    public Metadata Metadata { get; set; }
    public Spec Spec { get; set; }
}

public class Metadata
{
    public string Name { get; set; }
    public string Namespace { get; set; }
}

public class Spec
{
    public List<VersionSpec> Versions { get; set; }
}

public class VersionSpec
{
    public string Name { get; set; }
    public WorkflowDefinition Document { get; set; }
}


internal class CreateWorkflowCommand : Command
{
    public const string CommandName = "create";
    public const string CommandDescription = "Creates a new workflow from a YAML definition.";

    public CreateWorkflowCommand(IServiceProvider serviceProvider, ILoggerFactory loggerFactory, ISynapseApiClient api)
        : base(serviceProvider, loggerFactory, api, CommandName, CommandDescription)
    {
        this.Add(CommandOptions.File);
        this.Handler = CommandHandler.Create<string>(this.HandleAsync);
    }

    public async Task HandleAsync(string file)
    {
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            Console.WriteLine($"[ERROR] File not found: {file}");
            return;
        }

        string yamlContent = await File.ReadAllTextAsync(file);

        Console.WriteLine("📄 RAW YAML CONTENT:");
        Console.WriteLine(yamlContent);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        // Try parse outer YAML structure
        var yamlRoot = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
        if (!yamlRoot.TryGetValue("spec", out var specRaw) ||
            !(specRaw is Dictionary<object, object> specDict) ||
            !specDict.TryGetValue("versions", out var versionsRaw) ||
            !(versionsRaw is IEnumerable<object> versionsList) ||
            !versionsList.Cast<Dictionary<object, object>>().First().TryGetValue("document", out var documentRaw))
        {
            Console.WriteLine("[ERROR] Failed to find spec.versions[0].document in YAML.");
            return;
        }

        // Extract just the nested `document:` block into a Dictionary
        var firstVersion = versionsList
            .Cast<Dictionary<object, object>>()
            .First();

        var documentObj = firstVersion["document"];

        // Convert it back to YAML
        var innerSerializer = new SerializerBuilder()
            .JsonCompatible()
            .Build();

        var lines = yamlContent.Split('\n');
        // Extract just the lines under `document:` and unindent them properly
        var docStartIndex = Array.FindIndex(lines, l => l.TrimStart().StartsWith("document:"));
        if (docStartIndex == -1)
        {
            Console.WriteLine("[ERROR] Could not find 'document:' line.");
            return;
        }

        // Only take lines after "document:" and remove 2 leading spaces
        var docLines = lines
            .Skip(docStartIndex + 1)
            .Where(line => line.StartsWith("  ")) // ensure safe unindent
            .Select(line => line.Substring(2))   // remove exactly 2 spaces
            .ToArray();


        var docYaml = string.Join("\n", docLines);

        Console.WriteLine("📦 DOC YAML FOR DESERIALIZATION:\n" + docYaml);


        var wrapper = deserializer.Deserialize<WorkflowResource>(yamlContent);
        var swf = wrapper.Spec.Versions.FirstOrDefault()?.Document;

        Console.WriteLine("📄 Parsed workflow definition (swf):");
        Console.WriteLine(JsonConvert.SerializeObject(swf, Formatting.Indented));

        if (swf == null)
        {
            Console.WriteLine("[ERROR] WorkflowDefinition.Document is null.");
            return;
        }   

        if (swf.Do == null || swf.Do.Count == 0)
        {
            swf.Do = new Map<string, TaskDefinition>
            {
                ["greet"] = new SetTaskDefinition
                {
                    Set = new EquatableDictionary<string, object>
                    {
                        { "greeting", "'Hello World!'" }
                    }
                }
            };
        }


        Console.WriteLine($"✅ DSL: {swf.Document.Dsl}, Name: {swf.Document.Name}, Version: {swf.Document.Version}, Do Count: {swf.Do?.Count ?? -1}");

        //var id = swf.Name ?? "unnamed-workflow";
        var id = "unamed-workflow";
        var version = "v1";
       // var version = swf.Version ?? "v1";

        var workflow = new
        {
            metadata = new
            {
                name = id,
                @namespace = "default"
            },
            spec = new
            {
                versions = new[]
                {
                    new
                    {
                        name = version,
                        document = swf
                    }
                }
            }
        };

        var json = JsonConvert.SerializeObject(workflow);
        using var http = new HttpClient();

        http.BaseAddress = new Uri("http://localhost:8080");
        var token = Environment.GetEnvironmentVariable("SYNAPSE_API_AUTH_TOKEN");
        if (!string.IsNullOrEmpty(token))
        {
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await http.PostAsync("/api/v1/workflows", content);

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"✅ Workflow '{id}' created successfully.");
        }
        else
        {
            Console.WriteLine($"❌ API returned {response.StatusCode}:");
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }
    }


    static class CommandOptions
    {
        public static Option<string> File
        {
            get
            {
                return new Option<string>(
                    aliases: new[] { "--file", "-f" },
                    description: "Path to workflow YAML file"
                )
                {
                    IsRequired = true
                };
            }
        }
    }
}
