using AtelierTomato.Markov.Model;
using AtelierTomato.Markov.Model.ObjectOID;
using AtelierTomato.Markov.Model.ObjectOID.Types;
using AtelierTomato.Markov.Service.Discord;
using AtelierTomato.Markov.Storage;
using AtelierTomato.MarkovBot.Discord.Core.CommandModules.ParameterTypes;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.Options;

namespace AtelierTomato.MarkovBot.Discord.Core.CommandModules
{
	public class PermissionsModule : ModuleBase<SocketCommandContext>
	{
		private readonly DiscordBotOptions options;
		private readonly DiscordObjectOIDBuilder discordObjectOIDBuilder;
		private readonly IAuthorPermissionAccess authorPermissionAccess;

		public PermissionsModule(IOptions<DiscordBotOptions> options, DiscordObjectOIDBuilder discordObjectOIDBuilder, IAuthorPermissionAccess authorPermissionAccess)
		{
			this.options = options.Value;
			this.discordObjectOIDBuilder = discordObjectOIDBuilder;
			this.authorPermissionAccess = authorPermissionAccess;
		}

		[Command("optin")]
		[Alias("oi")]
		[Summary("Allows the user to opt in to having their messages gather by the bot.")]
		public async Task OptIn(PermissionScope from, PermissionScope to)
		{
			var oid = await getOID();
			var fromOID = getScope(from, oid);
			var toOID = getScope(to, oid);
			await authorPermissionAccess.WriteAuthorPermission(new AuthorPermission(new AuthorOID(ServiceType.Discord, oid.Instance, Context.User.Id.ToString()), fromOID, toOID));
			await ReplyAsync($"""Opted user "{Context.User.GlobalName}" into {options.BotName} from current {from} to current {to}.""");
		}

		[Command("optout")]
		[Alias("oo")]
		[Summary("Allows the user to opt out of having their messages gather by the bot.")]
		public async Task OptOut(PermissionScope from)
		{
			var oid = await getOID();
			var fromOID = getScope(from, oid);
			await authorPermissionAccess.WriteAuthorPermission(new AuthorPermission(new AuthorOID(ServiceType.Discord, oid.Instance, Context.User.Id.ToString()), fromOID, new SpecialObjectOID(SpecialObjectOIDType.PermissionDenied)));
			await ReplyAsync($"""Opted user "{Context.User.GlobalName}" out of {options.BotName} from current {from}.""");
		}

		private async Task<DiscordObjectOID> getOID()
		{
			DiscordObjectOID oid;
			if (Context.Channel is IGuildChannel guildChannel)
			{
				oid = await discordObjectOIDBuilder.Build(Context.Guild, guildChannel);
			}
			else
			{
				oid = DiscordObjectOID.ForThread(options.DiscordInstance, 0, 0, Context.Channel.Id, 0);
			}

			return oid;
		}

		private static DiscordObjectOID? getScope(PermissionScope scope, DiscordObjectOID oid)
		{
			return scope switch
			{
				PermissionScope.Global => null,
				PermissionScope.Discord => DiscordObjectOID.ForInstance(oid.Instance),
				PermissionScope.Server => DiscordObjectOID.ForServer(oid.Instance, oid.Server!.Value),
				PermissionScope.Category => DiscordObjectOID.ForCategory(oid.Instance, oid.Server!.Value, oid.Category!.Value),
				PermissionScope.Channel => DiscordObjectOID.ForChannel(oid.Instance, oid.Server!.Value, oid.Category!.Value, oid.Channel!.Value),
				PermissionScope.Thread => DiscordObjectOID.ForThread(oid.Instance, oid.Server!.Value, oid.Category!.Value, oid.Channel!.Value, oid.Thread ?? 0),
				_ => throw new InvalidOperationException()
			};
		}
	}
}
