using System.Diagnostics;
using AtelierTomato.Markov.Core;
using AtelierTomato.Markov.Core.Generation;
using AtelierTomato.Markov.Service.Discord;
using AtelierTomato.Markov.Storage;
using AtelierTomato.Markov.Storage.Sqlite;
using AtelierTomato.SimpleDiscordMarkovBot.Core;
using AtelierTomato.SimpleDiscordMarkovBot.Service;
using Discord;
using Discord.WebSocket;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSystemd();

if (Debugger.IsAttached)
{
	builder.Configuration.AddUserSecrets<Program>();
}

if (Microsoft.Extensions.Hosting.Systemd.SystemdHelpers.IsSystemdService())
{
	builder.Logging.ClearProviders();
	builder.Logging.AddJournal(options => options.SyslogIdentifier = builder.Configuration["SyslogIdentifier"]);
}

builder.Services.AddOptions<DiscordBotOptions>().Bind(builder.Configuration.GetSection("DiscordBot"));
builder.Services.AddOptions<SentenceParserOptions>().Bind(builder.Configuration.GetSection("SentenceParser"));
builder.Services.AddOptions<DiscordSentenceParserOptions>().Bind(builder.Configuration.GetSection("DiscordSentenceParser"));
builder.Services.AddOptions<SqliteAccessOptions>().Bind(builder.Configuration.GetSection("SqliteAccess"));
builder.Services.AddOptions<MarkovChainOptions>().Bind(builder.Configuration.GetSection("MarkovChain"));
builder.Services.AddOptions<KeywordOptions>().Bind(builder.Configuration.GetSection("Keyword"));

builder.Services.AddHostedService<Worker>();

var discordSocketConfig = new DiscordSocketConfig
{
	// request all unprivileged but unrequest the ones that keep causing log spam
	GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents | GatewayIntents.MessageContent,
};
var client = new DiscordSocketClient(config: discordSocketConfig);

builder.Services.AddSingleton(client);

builder.Services
	.AddSingleton<DiscordEventDispatcher>()
	.AddSingleton<DiscordSentenceParser>()
	.AddSingleton<ISentenceAccess, SqliteSentenceAccess>()
	.AddSingleton<IWordStatisticAccess, SqliteWordStatisticAccess>()
	.AddSingleton<MarkovChain>()
	.AddSingleton<KeywordProvider>()
	.AddSingleton<DiscordSentenceRenderer>();

var host = builder.Build();

await host.RunAsync();
