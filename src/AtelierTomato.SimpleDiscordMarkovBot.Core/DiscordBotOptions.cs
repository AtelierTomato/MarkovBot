namespace AtelierTomato.SimpleDiscordMarkovBot.Core
{
	public class DiscordBotOptions
	{
		public string BotName { get; set; } = "devbot";
		public bool MessageReceivedMode { get; set; } = false;
		public bool ReactMode { get; set; } = true;
		public List<string> WriteEmojis { get; set; } = ["\uD83D\uDCDD"];
		public List<string> WriteDiscordEmojiNames { get; set; } = [];
		public List<string> DeleteEmojis { get; set; } = ["\u274C"];
		public List<string> DeleteDiscordEmojiNames { get; set; } = [];
		public string FailEmoji { get; set; } = "\uD83D\uDEAB";
		public string FailDiscordEmojiName { get; set; } = "";
		public List<ulong> RestrictToIds { get; set; } = [];
	}
}
