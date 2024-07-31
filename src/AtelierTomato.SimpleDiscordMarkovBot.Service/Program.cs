using System.Diagnostics;
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
	.ConfigureServices(services =>
	{
		services.AddHostedService<Worker>();

		var discordSocketConfig = new DiscordSocketConfig
		{
			// request all unprivileged but unrequest the ones that keep causing log spam
			GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents | GatewayIntents.MessageContent,
		};
		var client = new DiscordSocketClient(config: discordSocketConfig);

		services.AddSingleton(client);
		services.AddSingleton<DiscordEventDispatcher>();
	})
	.Build();

await host.RunAsync();

//var builder = Host.CreateApplicationBuilder(args);
//builder.Services.AddHostedService<Worker>();

//var host = builder.Build();
//host.Run();
