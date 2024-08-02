using AtelierTomato.Markov.Core.Generation;
using AtelierTomato.Markov.Model;
using AtelierTomato.Markov.Service.Discord;
using AtelierTomato.Markov.Storage;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtelierTomato.SimpleDiscordMarkovBot.Core
{
	public class DiscordEventDispatcher
	{
		private readonly ILogger<DiscordEventDispatcher> logger;
		private readonly DiscordSocketClient client;
		private readonly DiscordSentenceParser sentenceParser;
		private readonly IWordStatisticAccess wordStatisticAccess;
		private readonly ISentenceAccess sentenceAccess;
		private readonly DiscordBotOptions options;
		private readonly MarkovChain markovChain;
		private readonly KeywordProvider keywordProvider;
		private readonly DiscordSentenceRenderer sentenceRenderer;
		public DiscordEventDispatcher(ILogger<DiscordEventDispatcher> logger, DiscordSocketClient client, DiscordSentenceParser sentenceParser, IWordStatisticAccess wordStatisticAccess, ISentenceAccess sentenceAccess, IOptions<DiscordBotOptions> options, MarkovChain markovChain, KeywordProvider keywordProvider, DiscordSentenceRenderer sentenceRenderer)
		{
			this.logger = logger;
			this.client = client;
			this.sentenceParser = sentenceParser;
			this.wordStatisticAccess = wordStatisticAccess;
			this.sentenceAccess = sentenceAccess;
			this.options = options.Value;
			this.markovChain = markovChain;
			this.keywordProvider = keywordProvider;
			this.sentenceRenderer = sentenceRenderer;

			this.client.Ready += this.Client_Ready;

			this.client.MessageReceived += this.Client_MessageReceived;
			this.client.ReactionAdded += this.Client_ReactionAdded;
		}

		private async Task Client_Ready()
		{
			await this.client.SetGameAsync("Placeholder!", type: Discord.ActivityType.Competing);
		}

		private async Task Client_MessageReceived(SocketMessage messageParam)
		{
			// Don't process the message if it was a system message
			if (messageParam is not SocketUserMessage message)
				return;
			// Don't process the message if it was sent by a bot
			if (message.Author.IsBot)
				return;

			var context = new SocketCommandContext(this.client, message);

			// If the bot is in MessageReceivedMode, continue
			if (options.MessageReceivedMode)
			{
				// If there are no values in RestrictToIds, or the user's ID is in RestrictToIds, continue
				if (options.RestrictToIds.Count == 0 || options.RestrictToIds.Contains(message.Author.Id))
				{
					// Parse the text of the message, write the words in it to the WordStatistic table, write the sentences into the Sentence table
					IEnumerable<string> parsedMessage = sentenceParser.ParseIntoSentenceTexts(message.Content, message.Tags);
					if (parsedMessage.Any())
					{
						foreach (string parsedText in parsedMessage)
						{
							await wordStatisticAccess.WriteWordStatisticsFromString(parsedText);
						}
						IEnumerable<Sentence> sentences = await DiscordSentenceBuilder.Build(context.Guild, context.Channel, message.Id, context.User.Id, message.CreatedAt, parsedMessage);
						await sentenceAccess.WriteSentenceRange(sentences);
					}
				}
			}

			// Respond with a markov-generated sentence if a user says the bot's name or replies to the bot.
			if (message.Content.Contains(options.BotName, StringComparison.InvariantCultureIgnoreCase) ||
				(message.ReferencedMessage is not null && (message.ReferencedMessage.Author.Id == client.CurrentUser.Id)))
			{
				using (context.Channel.EnterTypingState()) _ =
					await context.Channel.SendMessageAsync
					(
						sentenceRenderer.Render
						(
							await markovChain.Generate
							(
								new SentenceFilter(null, null),
								await keywordProvider.Find(message.Content)
							),
							context.Guild.Emotes,
							client.Guilds.SelectMany(g => g.Emotes)
						)
					);
			}

		}

		private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> originChannel, SocketReaction reaction)
		{
			// If the bot is in ReactMode, continue.
			if (options.ReactMode)
			{
				IEnumerable<IEmote> writeEmojis = options.WriteEmojis.Select(e => new Emoji(e));
				IEnumerable<IEmote> deleteEmojis = options.DeleteEmojis.Select(e => new Emoji(e));
				IEnumerable<IEmote> failEmojis = [new Emoji(options.FailEmoji)];
				var message = await cachedMessage.GetOrDownloadAsync();
				var context = new CommandContext(client, message);

				if (message is null) return;

				if (options.RestrictToIds.Count == 0 || (options.RestrictToIds.Contains(message.Author.Id) && options.RestrictToIds.Contains(reaction.UserId)))
				{
					if (writeEmojis.Contains(reaction.Emote))
					{
						// Parse the text of the message, write the words in it to the WordStatistic table, write the sentences into the Sentence table
						IEnumerable<string> parsedMessage = sentenceParser.ParseIntoSentenceTexts(message.Content, message.Tags);
						if (parsedMessage.Any())
						{
							foreach (string parsedText in parsedMessage)
							{
								await wordStatisticAccess.WriteWordStatisticsFromString(parsedText);
							}
							IEnumerable<Sentence> sentences = await DiscordSentenceBuilder.Build(context.Guild, context.Channel, message.Id, context.User.Id, message.CreatedAt, parsedMessage);
							await sentenceAccess.WriteSentenceRange(sentences);
							await message.AddReactionAsync(reaction.Emote);
						}
						else
						{
							await message.AddReactionAsync(failEmojis.First());
						}
					}
					else if (deleteEmojis.Contains(reaction.Emote))
					{
						// Delete all sentences made from this message from the database
						await sentenceAccess.DeleteSentenceRange(new SentenceFilter(await DiscordObjectOIDBuilder.Build(context.Guild, context.Channel, context.Message.Id), null));
						await message.AddReactionAsync(reaction.Emote);
					}
				}
			}
		}
	}
}
