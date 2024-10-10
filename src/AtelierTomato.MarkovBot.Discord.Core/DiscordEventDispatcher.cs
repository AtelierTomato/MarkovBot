using AtelierTomato.Markov.Core.Generation;
using AtelierTomato.Markov.Model;
using AtelierTomato.Markov.Model.ObjectOID;
using AtelierTomato.Markov.Service.Discord;
using AtelierTomato.Markov.Storage;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AtelierTomato.MarkovBot.Discord.Core
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
		private readonly DiscordSentenceBuilder discordSentenceBuilder;
		private readonly DiscordObjectOIDBuilder discordObjectOIDBuilder;
		public DiscordEventDispatcher(ILogger<DiscordEventDispatcher> logger, DiscordSocketClient client, DiscordSentenceParser sentenceParser, IWordStatisticAccess wordStatisticAccess, ISentenceAccess sentenceAccess, IOptions<DiscordBotOptions> options, MarkovChain markovChain, KeywordProvider keywordProvider, DiscordSentenceRenderer sentenceRenderer, DiscordSentenceBuilder discordSentenceBuilder, DiscordObjectOIDBuilder discordObjectOIDBuilder)
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
			this.discordSentenceBuilder = discordSentenceBuilder;
			this.discordObjectOIDBuilder = discordObjectOIDBuilder;

			this.client.Log += msg => Task.Run(() => this.Client_Log(msg));
			this.client.Ready += this.Client_Ready;

			this.client.MessageReceived += this.Client_MessageReceived;
			this.client.ReactionAdded += this.Client_ReactionAdded;
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

		private void Client_Log(LogMessage logMessage)
		{
			this.logger.Log(
				logLevel: MapSeverity(logMessage.Severity),
				exception: logMessage.Exception ?? null,
				message: "Discord.NET ({Source}): {DiscordNetMessage}",
				logMessage.Source ?? "unknown",
				logMessage.Message ?? logMessage.Exception?.Message ?? "An error occurred."
			);
		}

		public async Task LoginAsync(string token)
		{
			await this.client.LoginAsync(TokenType.Bot, token);
		}

		public async Task StartAsync()
		{
			await this.client.StartAsync();
		}

		public async Task StopAsync()
		{
			await this.client.StopAsync();
		}

		private async Task Client_Ready()
		{
			await this.client.SetGameAsync(options.ActivityString, type: options.ActivityType);
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

			_ = await ProcessForGathering(message, context);

			await ProcessForRetorting(message, context);
		}

		private async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> originChannel, SocketReaction reaction)
		{
			// Don't process the reaction if it was sent by a bot or put on a bot post
			if (reaction.User.GetValueOrDefault().IsBot)
				return;
			// Don't process the reaction if the bot is not in ReactMode.
			if (!options.ReactMode)
				return;
			// Do not process if message is not a user message (or null).
			if (await cachedMessage.GetOrDownloadAsync() is not IUserMessage message)
				return;
			// Do not process if the user message is from a bot.
			if (message.Author.IsBot)
				return;

			var context = new CommandContext(client, message);
			var currentEmojis = context.Guild.Emotes;
			var otherAvailableEmojis = client.Guilds.Where(g => g.Id != context.Guild.Id).SelectMany(g => g.Emotes);

			// Set up emojis to check the reaction for.
			IEnumerable<IEmote> writeEmojis = [], deleteEmojis = [], failEmojis = [];
			writeEmojis = options.WriteDiscordEmojiNames.SelectMany(n => ParseEmotesFromName(n, currentEmojis, otherAvailableEmojis))
				.Concat((IEnumerable<IEmote>)options.WriteEmojis.Select(e => new Emoji(e)));
			deleteEmojis = options.DeleteDiscordEmojiNames.SelectMany(n => ParseEmotesFromName(n, currentEmojis, otherAvailableEmojis))
				.Concat((IEnumerable<IEmote>)options.DeleteEmojis.Select(e => new Emoji(e)));
			if (options.FailDiscordEmojiName is not "")
			{
				failEmojis = failEmojis.Append(ParseEmotesFromName(options.FailDiscordEmojiName, currentEmojis, otherAvailableEmojis).First());
			}
			failEmojis = failEmojis.Append(new Emoji(options.FailEmoji));

			var reactionUserIsAllowed = !options.RestrictToIds.Any() || options.RestrictToIds.Contains(reaction.UserId);

			if (!reactionUserIsAllowed)
			{
				if (writeEmojis.Contains(reaction.Emote) || deleteEmojis.Contains(reaction.Emote))
				{
					await message.AddReactionAsync(failEmojis.First());
				}
				return;
			}

			if (writeEmojis.Contains(reaction.Emote))
			{
				if (await ProcessForGathering(message, context))
				{
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
				await ProcessForDeleting(message, context);
				await message.AddReactionAsync(reaction.Emote);
			}
		}

		private static IEnumerable<Emote> ParseEmotesFromName(string n, IEnumerable<Emote> currentEmojis, IEnumerable<Emote> otherAvailableEmojis)
		{
			IEnumerable<Emote> emoji = currentEmojis.Where(e => e.Name == n);
			emoji = emoji.Concat(otherAvailableEmojis.Where(e => e.Name == n));
			if (emoji.Any())
			{
				return emoji;
			}
			else
			{
				throw new InvalidOperationException($"Emoji with name '{n}' not found.");
			}
		}

		private async Task<bool> ProcessForGathering(IUserMessage message, ICommandContext context)
		{
			var authorIsAllowed = !options.RestrictToIds.Any() || !options.RestrictToIds.Contains(message.Author.Id);

			if (!authorIsAllowed)
			{
				return false;
			}

			bool gatherSentences = options.MessageReceivedMode;
			bool gatherWordStatistics = (options.MessageReceivedMode || options.WordStatisticsOnMessageReceivedMode);

			if (!gatherSentences && !gatherWordStatistics)
			{
				return false;
			}

			// Parse the text of the message, write the words in it to the WordStatistic table, write the sentences into the Sentence table
			IEnumerable<string> messageSentenceTexts = sentenceParser.ParseIntoSentenceTexts(message.Content, message.Tags);
			if (!messageSentenceTexts.Any())
			{
				return false;
			}

			// Either way, we're writing WordStatistics to the database
			if (gatherWordStatistics)
			{
				foreach (string text in messageSentenceTexts)
				{
					await wordStatisticAccess.WriteWordStatisticsFromString(text);
				}
			}

			// Only generate and write Sentences to the database if we're in MessageReceivedMode
			if (gatherSentences)
			{
				IEnumerable<Sentence> sentences;
				if (context.Channel is IGuildChannel guildChannel)
				{
					sentences = await discordSentenceBuilder.Build(context.Guild, guildChannel, message.Id, context.User.Id, message.CreatedAt, messageSentenceTexts);
				}
				else
				{
					sentences = DiscordSentenceBuilder.Build(context.Channel, message.Id, context.User.Id, message.CreatedAt, messageSentenceTexts);
				}
				await sentenceAccess.WriteSentenceRange(sentences);
			}

			return true;
		}

		private async Task ProcessForRetorting(IUserMessage message, ICommandContext context)
		{
			var isMention = message.Content.Contains(options.BotName, StringComparison.InvariantCultureIgnoreCase);
			var isReply = message.ReferencedMessage is not null && (message.ReferencedMessage.Author.Id == client.CurrentUser.Id);

			if (!isMention && !isReply)
				return; // no reason to butt in here.

			using (context.Channel.EnterTypingState())
			{
				var responseText = await markovChain.Generate(new SentenceFilter([], []), await keywordProvider.Find(message.Content));
				var responseSentence = sentenceRenderer.Render(responseText, context.Guild.Emotes, client.Guilds.SelectMany(g => g.Emotes));
				if (string.IsNullOrWhiteSpace(responseSentence))
				{
					responseSentence = options.EmptyMarkovReturn;
				}

				await context.Channel.SendMessageAsync(responseSentence);
			}
		}

		private async Task ProcessForDeleting(IUserMessage message, ICommandContext context)
		{
			IObjectOID oid;
			if (context.Channel is IGuildChannel guildChannel)
			{
				oid = (await discordObjectOIDBuilder.Build(context.Guild, guildChannel)).WithMessage(message.Id);
			}
			else
			{
				oid = DiscordObjectOID.ForMessage("discord.com", 0, 0, context.Channel.Id, 0, message.Id);
			}
			await sentenceAccess.DeleteSentenceRange(new SentenceFilter([oid], []), null);
		}
	}
}
