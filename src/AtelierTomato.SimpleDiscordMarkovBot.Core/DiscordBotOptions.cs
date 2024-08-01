namespace AtelierTomato.SimpleDiscordMarkovBot.Core
{
	public class DiscordBotOptions
	{
		public string BotName { get; set; } = "devbot";
		public List<ulong> RestrictToIds { get; set; } = [];
	}
}
