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

IHost host = Host.CreateDefaultBuilder(args)
	.UseSystemd()
	.ConfigureAppConfiguration((hostContext, builder) =>
	{
		// Add other providers for JSON, etc.

		// only use user secrets when debugging.
		if (Debugger.IsAttached)
		{
			builder.AddUserSecrets<Program>();
		}
	})
	.ConfigureLogging((hostContext, builder) =>
	{
		// if we're in fact using systemd, throw out the default console logger and only use the systemd journal
		if (Microsoft.Extensions.Hosting.Systemd.SystemdHelpers.IsSystemdService())
		{
			builder.ClearProviders();
			builder.AddJournal(options => options.SyslogIdentifier = hostContext.Configuration["SyslogIdentifier"]);
		}
	})
	.ConfigureServices((hostContext, services) =>
	{
		services.AddHostedService<Worker>();

		var discordSocketConfig = new DiscordSocketConfig
		{
			// request all unprivileged but unrequest the ones that keep causing log spam
			GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents | GatewayIntents.MessageContent,
		};
		var client = new DiscordSocketClient(config: discordSocketConfig);

		services.AddSingleton(client);
		services.AddSingleton<DiscordEventDispatcher>()
				.AddSingleton<DiscordSentenceParser>()
				.AddSingleton<ISentenceAccess, SqliteSentenceAccess>()
				.AddSingleton<IWordStatisticAccess, SqliteWordStatisticAccess>()
				.AddSingleton<MarkovChain>()
				.AddSingleton<KeywordProvider>()
				.AddSingleton<DiscordSentenceRenderer>();
		services.AddOptions<SentenceParserOptions>()
				.Bind(hostContext.Configuration.GetSection("SentenceParser"));
		services.AddOptions<DiscordSentenceParserOptions>()
				.Bind(hostContext.Configuration.GetSection("DiscordSentenceParser"));
		services.AddOptions<SqliteAccessOptions>()
				.Bind(hostContext.Configuration.GetSection("SqliteAccess"));
		services.AddOptions<DiscordBotOptions>()
				.Bind(hostContext.Configuration.GetSection("DiscordBot"));
		services.AddOptions<MarkovChainOptions>()
				.Bind(hostContext.Configuration.GetSection("MarkovChain"));
		services.AddOptions<KeywordOptions>()
				.Bind(hostContext.Configuration.GetSection("Keyword"));
	})
	.Build();

await host.RunAsync();
