using CommunityBot.Preconditions;
using Discord.Commands;
using System.Threading.Tasks;
using Discord;

namespace CommunityBot.Modules
{
    public class Basics : ModuleBase<SocketCommandContext>
    {
        [Command("Hello")]
        [Cooldown(5)]
        public async Task SayHello()
        {
            var embed = new EmbedBuilder();
            embed.WithTitle("Hello!");
            embed.WithDescription("My name is Miuni, the Community BOT!");
            embed.WithColor(240, 98, 146);
            embed.WithImageUrl(Context.Client.CurrentUser.GetAvatarUrl());
		
            await Context.Channel.SendMessageAsync("", embed: embed.Build());
        }
    }
}
