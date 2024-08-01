using AtelierTomato.Markov.Model;
using AtelierTomato.Markov.Service.Discord;
using AtelierTomato.Markov.Storage;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace AtelierTomato.SimpleDiscordMarkovBot.Core
{
	public class DiscordEventDispatcher
	{
		private readonly ILogger<DiscordEventDispatcher> logger;
		private readonly DiscordSocketClient client;
		private readonly DiscordSentenceParser sentenceParser;
		private readonly IWordStatisticAccess wordStatisticAccess;
		private readonly ISentenceAccess sentenceAccess;
		public DiscordEventDispatcher(ILogger<DiscordEventDispatcher> logger, DiscordSocketClient client, DiscordSentenceParser sentenceParser, IWordStatisticAccess wordStatisticAccess, ISentenceAccess sentenceAccess)
		{
			this.logger = logger;
			this.client = client;
			this.sentenceParser = sentenceParser;
			this.wordStatisticAccess = wordStatisticAccess;
			this.sentenceAccess = sentenceAccess;

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

			var abc = await wordStatisticAccess.ReadWordStatistic("b");

			IEnumerable<string> parsedMessage = sentenceParser.ParseIntoSentenceTexts(message.Content, message.Tags);
			foreach (string parsedText in parsedMessage)
			{
				await WriteWordStatisticsFromString(parsedText);
			}
			IEnumerable<Sentence> sentences = await DiscordSentenceBuilder.Build(context.Guild, context.Channel, message.Id, context.User.Id, message.CreatedAt, parsedMessage);
			await sentenceAccess.WriteSentenceRange(sentences);
		}

		public async Task WriteWordStatisticsFromString(string str)
		{
			var tokenizedStr = str.Split(' ');
			var words = tokenizedStr.Distinct();
			var enumerable = await wordStatisticAccess.ReadWordStatisticRange(words);
			var storedWordStatistics = enumerable.ToDictionary(w => w.Name, w => w.Appearances);

			foreach (string word in tokenizedStr)
			{
				_ = storedWordStatistics.TryGetValue(word, out var appearances);
				storedWordStatistics[word] = appearances + 1;
			}

			await wordStatisticAccess.WriteWordStatisticRange(storedWordStatistics.Select(kv => new WordStatistic(kv.Key, kv.Value)));
		}
	}
}
