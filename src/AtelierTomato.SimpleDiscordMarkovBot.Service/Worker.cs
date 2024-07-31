using AtelierTomato.SimpleDiscordMarkovBot.Core;
using Discord;
using Discord.WebSocket;

namespace AtelierTomato.SimpleDiscordMarkovBot.Service
{
	public class Worker : IHostedService
	{
		private readonly ILogger<Worker> logger;
		private readonly IConfiguration configuration;
		private readonly DiscordSocketClient client;
		private readonly DiscordEventDispatcher discordEventDispatcher;
		public Worker(ILogger<Worker> logger, IConfiguration configuration, DiscordSocketClient client, DiscordEventDispatcher discordEventDispatcher)
		{
			this.logger = logger;
			this.configuration = configuration;
			this.client = client;
			this.discordEventDispatcher = discordEventDispatcher;
		}

		public async Task StartAsync(CancellationToken stoppingToken)
		{
			this.logger.LogInformation("hello");

			this.client.Log += msg => Task.Run(() => this.LogDiscordNetMessage(msg));

			var token = this.configuration["Discord-API-Key"];

			await this.client.LoginAsync(TokenType.Bot, token);
			await this.client.StartAsync();

			this.logger.LogInformation("ready to go");
		}

		public async Task StopAsync(CancellationToken stoppingToken)
		{
			await this.client.StopAsync();

			this.logger.LogInformation("goodbye");
		}

		private static LogLevel MapSeverity(LogSeverity logSeverity) => logSeverity switch
		{
			LogSeverity.Critical => LogLevel.Critical,
			LogSeverity.Error => LogLevel.Error,
			LogSeverity.Warning => LogLevel.Warning,
			LogSeverity.Info => LogLevel.Information,
			LogSeverity.Verbose => LogLevel.Debug,
			LogSeverity.Debug => LogLevel.Trace,
			_ => LogLevel.None,
		};

		private void LogDiscordNetMessage(LogMessage logMessage)
		{
			this.logger.Log(
				logLevel: MapSeverity(logMessage.Severity),
				exception: logMessage.Exception ?? null,
				message: "Discord.NET ({Source}): {DiscordNetMessage}",
				logMessage.Source ?? "unknown",
				logMessage.Message ?? logMessage.Exception?.Message ?? "An error occurred."
			);
		}
	}
}
