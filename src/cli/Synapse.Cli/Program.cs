﻿// Copyright © 2024-Present The Synapse Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

var parser = BuildCommandLineParser();
await parser.InvokeAsync(args);

static Parser BuildCommandLineParser()
{
    var configuration = new ConfigurationBuilder()
        .AddYamlFile(CliConstants.ConfigurationFileName, true, true)
        .Build();
    var services = new ServiceCollection();
    ConfigureServices(services, configuration);
    var serviceProvider = services.BuildServiceProvider();
    var scope = serviceProvider.CreateScope();
    var rootCommand = new RootCommand();
    foreach (var command in scope.ServiceProvider.GetServices<Command>()) rootCommand.AddCommand(command);
    return new CommandLineBuilder(rootCommand)
        .UseDefaults()
        .UseExceptionHandler((ex, context) =>
        {
            AnsiConsole.MarkupLine($"[red]{ex.ToString().EscapeMarkup()}[/]");
            var inner = ex.InnerException;
            while (inner != null)
            {
                AnsiConsole.Markup($"[red]{inner.Message.EscapeMarkup()}[/]");
                inner = inner.InnerException;
            }
        })
        .Build();
}

static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    var applicationOptions = new ApplicationOptions();
    configuration.Bind(applicationOptions);
    services.AddSingleton(configuration);
    services.Configure<ApplicationOptions>(configuration);
    services.AddLogging();
    services.Configure<JsonSerializerOptions>(options =>
    {
        options.WriteIndented = true;
    });
    services.AddSynapseHttpApiClient(http =>
    {
        if (string.IsNullOrWhiteSpace(applicationOptions.Api.Current)) return;
        var apiConfiguration = applicationOptions.Api.Configurations[applicationOptions.Api.Current];
        http.BaseAddress = apiConfiguration.Server;
        http.TokenFactory = provider =>
        {
            var envToken = Environment.GetEnvironmentVariable("SYNAPSE_API_AUTH_TOKEN");
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                Console.WriteLine($"🔑 Using token from ENV: {envToken}");
                return Task.FromResult(envToken)!;
            }

            if (!string.IsNullOrWhiteSpace(apiConfiguration.Token))
            {
                Console.WriteLine($"🔑 Using token from config: {apiConfiguration.Token}");
                return Task.FromResult(apiConfiguration.Token)!;
            }

            Console.WriteLine("❌ No token found.");
            return Task.FromResult<string?>(null)!;
        };
    });
    services.AddCliCommands();
    services.AddSingleton<IOptionsManager, OptionsManager>();
}