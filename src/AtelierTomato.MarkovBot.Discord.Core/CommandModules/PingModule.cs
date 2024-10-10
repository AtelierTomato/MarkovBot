using Discord.Commands;

namespace AtelierTomato.MarkovBot.Discord.Core.CommandModules
{
	public class PingModule : ModuleBase<SocketCommandContext>
	{
		[Command("ping")]
		public async Task Ping()
		{
			await ReplyAsync(message: "pong!!");
		}
	}
}
