using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace AtelierTomato.SimpleDiscordMarkovBot.Core
{
	public class DiscordEventDispatcher
	{
		private readonly ILogger<DiscordEventDispatcher> logger;
		private readonly DiscordSocketClient client;

		public DiscordEventDispatcher(ILogger<DiscordEventDispatcher> logger, DiscordSocketClient client)
		{
			this.logger = logger;
			this.client = client;

			this.client.Ready += this.Client_Ready;

			this.client.MessageReceived += this.Client_MessageReceived;
		}

		private async Task Client_Ready()
		{
			await this.client.SetGameAsync("Placeholder!", type: Discord.ActivityType.Competing);
		}

		private async Task Client_MessageReceived(SocketMessage messageParam)
		{
			// Don't process the command if it was a system message
			if (messageParam is not SocketUserMessage message)
			{
				return;
			}

			var context = new SocketCommandContext(this.client, message);
		}
	}
}
