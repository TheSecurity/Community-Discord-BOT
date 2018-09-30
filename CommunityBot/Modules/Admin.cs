using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using CommunityBot.Handlers;
using CommunityBot.Preconditions;

namespace CommunityBot.Modules
{
    public class Admin : ModuleBase<SocketCommandContext>
    {
        private static readonly OverwritePermissions denyOverwrite = new OverwritePermissions(addReactions: PermValue.Deny, sendMessages: PermValue.Deny, attachFiles: PermValue.Deny);

		[Command("purge", RunMode = RunMode.Async)]
		[Remarks("Purges An Amount Of Messages")]
		[RequireUserPermission(GuildPermission.ManageMessages)]
		public async Task Clear(int amountOfMessagesToDelete)
		{
			if (amountOfMessagesToDelete < 1)
			{
				await ReplyAsync($":warning: Are you seriously trying to remove **{amountOfMessagesToDelete}**? :scream:");
				return;
			}
			var messages = await Context.Message.Channel.GetMessagesAsync(amountOfMessagesToDelete + 1).FlattenAsync(); //amount of  messages
			var overmessages = messages.Where(x => (DateTime.Now - x.Timestamp).TotalDays > 13).ToList(); //messages older than 13 days
			messages = messages.Except(overmessages).ToList();

			await (Context.Message.Channel as SocketTextChannel).DeleteMessagesAsync(messages); //removing messages younger than 13 days
			foreach (var o in overmessages) //removing messages older than 13 days
			{
				await (Context.Message.Channel as SocketTextChannel).DeleteMessageAsync(o);
			}
			if (amountOfMessagesToDelete > 1) await ReplyAsync($":information_source: Last {amountOfMessagesToDelete} messages were deleted.");
			else await ReplyAsync($":information_source: Last {amountOfMessagesToDelete} message was deleted.");
		}

	    [Command("purge", RunMode = RunMode.Async)]
	    [Remarks("Purges An Amount Of Messages")]
	    [RequireUserPermission(GuildPermission.ManageMessages)]
	    public async Task Clear(SocketGuildUser user)
	    {
		    var textchannels = Context.Guild.TextChannels.Where(x => x.Users.Contains(user)); //get all text channels where user have permissions

			foreach (var channel in textchannels)
		    {
			    var messages = await channel.GetMessagesAsync().FlattenAsync();
			    var finalmessages =  messages.Where(x => x.Author == (SocketUser)user && x.IsPinned == false).ToList(); //messages for deleting younger than 13 days 
			    var overmessages = finalmessages.Where(x => (DateTime.Now - x.Timestamp).TotalDays > 13).ToList(); //messages for deleting older than 13 days - due discord 
			    finalmessages =  finalmessages.Except(overmessages).ToList(); 

			    await channel.DeleteMessagesAsync(finalmessages); //removing messages old max. 13 days
			    foreach (var o in overmessages) //removing other messages
			    {
					await channel.DeleteMessageAsync(o);
				}
			}

		    await ReplyAsync($":information_source: All messages from {user.Username} were deleted.");
		}

	    [Command("purge", RunMode = RunMode.Async)]
	    [Remarks("Purges An Amount Of Messages")]
	    [RequireUserPermission(GuildPermission.ManageMessages)]
	    public async Task Clear(SocketGuildUser user, int amountOfMessagesToDelete)
	    {
		    if (amountOfMessagesToDelete < 1)
		    {
			    await ReplyAsync($":warning: Are you seriously trying to remove **{amountOfMessagesToDelete}**? :scream:");
			    return;
			}
		    var messages = await Context.Channel.GetMessagesAsync().FlattenAsync();
			var finalmessages = messages.Where(x => x.Author == (SocketUser)user && x.IsPinned == false).Take(amountOfMessagesToDelete + 1).ToList(); //amount of messages younger than 13 days 
		    if (finalmessages.Count() == 0)
		    {
			    await ReplyAsync($":warning: I did not find any messages from {user.Nickname}.");
				return;
			}

		    var overmessages = finalmessages.Where(x => (DateTime.Now - x.Timestamp).TotalDays > 13).ToList(); //messages older than 13 days
		    finalmessages = finalmessages.Except(overmessages).ToList();
			await (Context.Message.Channel as SocketTextChannel).DeleteMessagesAsync(finalmessages); //deleting messages younger than 13 days

		    foreach (var o in overmessages) //deleting messages older than 13 days
		    {
			    await (Context.Message.Channel as SocketTextChannel).DeleteMessageAsync(o);
		    }

			if(amountOfMessagesToDelete > 1) await ReplyAsync($":information_source: {amountOfMessagesToDelete} messages from {user.Username} were deleted in this channel.");
			else await ReplyAsync($":information_source: {amountOfMessagesToDelete} message from {user.Username} were deleted in this channel.");
		}

		[Command("kick")]
        [Remarks("Kick A User")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kick([NoSelf][RequireBotHigherHirachy] SocketGuildUser user)
        {
            await user.KickAsync();
        }

        [Command("mute")]
        [Remarks("Mutes A User")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        public async Task Mute(SocketGuildUser user)
        {
            await Context.Guild.GetUser(user.Id).ModifyAsync(x => x.Mute = true);

            var muteRole = await GetMuteRole(user.Guild);
            if (!user.Roles.Any(r => r.Id == muteRole.Id))
                await user.AddRoleAsync(muteRole).ConfigureAwait(false);
        }

        [Command("unmute")]
        [Remarks("Unmutes A User")]
        [RequireUserPermission(GuildPermission.MuteMembers)]
        public async Task Unmute([NoSelf] SocketGuildUser user)
        {
            await Context.Guild.GetUser(user.Id).ModifyAsync(x => x.Mute = false).ConfigureAwait(false);

            try { await user.ModifyAsync(x => x.Mute = false).ConfigureAwait(false); } catch { }
            try { await user.RemoveRoleAsync(await GetMuteRole(user.Guild)).ConfigureAwait(false); } catch { }
        }

        [Command("ban")]
        [Remarks("Ban A User")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Ban([NoSelf][RequireBotHigherHirachy] SocketGuildUser user)
        {
            await Context.Guild.AddBanAsync(user);
        }

        [Command("unban")]
        [Remarks("Unban A User")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Unban([Remainder]string user)
        {
            var bans = await Context.Guild.GetBansAsync();

            var theUser = bans.FirstOrDefault(x => x.User.ToString().ToLowerInvariant() == user.ToLowerInvariant());

            await Context.Guild.RemoveBanAsync(theUser.User).ConfigureAwait(false);
        }

        [Command("unban")]
        [Remarks("Unban A User")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        public async Task Unban(ulong id)
        {
            var bans = await Context.Guild.GetBansAsync();

            var theUser = bans.FirstOrDefault(x => x.User.Id == id);

            await Context.Guild.RemoveBanAsync(theUser.User);
        }

        [Command("nickname")]
        [Remarks("Set A User's Nickname")]
        [RequireUserPermission(Discord.GuildPermission.ManageNicknames)]
        public async Task Nickname(SocketGuildUser username, [Remainder]string name)
        {
            await Context.Guild.GetUser(username.Id).ModifyAsync(x => x.Nickname = name);
        }

        [Command("createtext")]
        [Remarks("Make A Text Channel")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task Text(string channelname)
        {
            await Context.Guild.CreateTextChannelAsync(channelname);
        }

        [Command("createvoice")]
        [Remarks("Make A Voice Channel")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task Voice([Remainder]string channelname)
        {
            await Context.Guild.CreateVoiceChannelAsync(channelname);
        }

        [Command("announce")]
        [Remarks("Make A Announcement")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Announce([Remainder]string announcement)
        {
            var embed = EmbedHandler.CreateEmbed("Announcement By " + Context.Message.Author, announcement, EmbedHandler.EmbedMessageType.Info, true);

            await Context.Channel.SendMessageAsync("", false, embed);
            await Context.Message.DeleteAsync();
        }

        [Command("echo")]
        [Remarks("Make The Bot Say A Message")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Echo([Remainder] string message)
        {
            var embed = EmbedHandler.CreateEmbed("Message by: " + Context.Message.Author.Username, message, EmbedHandler.EmbedMessageType.Info, true);

            await Context.Channel.SendMessageAsync("", false, embed);
            await Context.Message.DeleteAsync();
        }

        [Command("Game"), Alias("ChangeGame", "SetGame")]
        [Remarks("Change what the bot is currently playing.")]
        [RequireOwner]
        public async Task SetGame([Remainder] string gamename)
        {
            await Context.Client.SetGameAsync(gamename);
            await ReplyAsync($"Changed game to `{gamename}`");
        }

        public async Task<IRole> GetMuteRole(IGuild guild)
        {
            const string defaultMuteRoleName = "Muted";

            var muteRoleName = "Muted";

            var muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName);

            if (muteRole == null)
            {
                try
                {
                    muteRole = await guild.CreateRoleAsync(muteRoleName, GuildPermissions.None).ConfigureAwait(false);
                }
                catch
                {
                    muteRole = guild.Roles.FirstOrDefault(r => r.Name == muteRoleName) ?? await guild.CreateRoleAsync(defaultMuteRoleName, GuildPermissions.None).ConfigureAwait(false);
                }
            }

            foreach (var toOverwrite in (await guild.GetTextChannelsAsync()))
            {
                try
                {
                    if (!toOverwrite.PermissionOverwrites.Any(x => x.TargetId == muteRole.Id && x.TargetType == PermissionTarget.Role))
                    {
                        await toOverwrite.AddPermissionOverwriteAsync(muteRole, denyOverwrite)
                                .ConfigureAwait(false);

                        await Task.Delay(200).ConfigureAwait(false);
                    }
                }
                catch
                {

                }
            }

            return muteRole;
        }

        [Command("setAvatar"), Remarks("Sets the bots Avatar")]
        [RequireOwner]
        public async Task SetAvatar(string link)
        {
            var s = Context.Message.DeleteAsync();

            try
            {
                var webClient = new WebClient();
                byte[] imageBytes = webClient.DownloadData(link);

                var stream = new MemoryStream(imageBytes);

                var image = new Image(stream);
                await Context.Client.CurrentUser.ModifyAsync(k => k.Avatar = image);
            }
            catch (Exception)
            {
                var embed = EmbedHandler.CreateEmbed("Avatar", "Coult not set the avatar!", EmbedHandler.EmbedMessageType.Exception);
                await Context.Channel.SendMessageAsync("", false, embed);
            }
        }
    }
}
