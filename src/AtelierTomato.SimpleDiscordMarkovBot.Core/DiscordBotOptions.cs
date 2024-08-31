using Discord;

namespace AtelierTomato.SimpleDiscordMarkovBot.Core
{
	public class DiscordBotOptions
	{
		public string BotName { get; set; } = "SET THIS";
		public List<ulong> RestrictToIds { get; set; } = [];
		// todo review what does this mean!!
		public bool MessageReceivedMode { get; set; } = false;
		public bool ReactMode { get; set; } = true;
		// todo review what does this mean!!
		public bool WordStatisticsOnMessageReceivedMode { get; set; } = true;
		public List<string> WriteEmojis { get; set; } = ["\uD83D\uDCDD"];
		public List<string> WriteDiscordEmojiNames { get; set; } = [];
		public List<string> DeleteEmojis { get; set; } = ["\u274C"];
		public List<string> DeleteDiscordEmojiNames { get; set; } = [];
		public string FailEmoji { get; set; } = "\uD83D\uDEAB";
		public string FailDiscordEmojiName { get; set; } = "";
		public string ActivityString { get; set; } = "Placeholder!";
		public ActivityType ActivityType { get; set; } = ActivityType.Playing;
		public string EmptyMarkovReturn { get; set; } = "The Markov chain failed to generate anything. This is likely because either the database or the specific query handed to it resulted in 0 Sentences.";
	}
}
