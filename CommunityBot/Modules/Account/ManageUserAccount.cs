using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityBot.Extensions;
using CommunityBot.Features.GlobalAccounts;
using CommunityBot.Helpers;
using CommunityBot.Entities;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace CommunityBot.Modules.Account
{
    [Group("account")]
    public class ManageUserAccount : ModuleBase<MiunieCommandContext>
    {
        [Command("info")]
        public async Task AccountInformation(SocketGuildUser user = null)
        {
            user = user ?? (SocketGuildUser) Context.User;

            var userAccount = GlobalUserAccounts.GetUserAccount(user);
            
            var embed = new EmbedBuilder()
                .WithAuthor($"{user.Username}'s account information", user.GetAvatarUrl())
                .AddField("Joined at: ", user.JoinedAt.Value.DateTime.ToString())
                .AddField("**Last message**", userAccount.LastMessage.ToString(), true)
                .AddField("**Last daily: **", userAccount.LastDaily.ToString(), true)
                .AddField("**Miunies**:", userAccount.Miunies, true)
                .AddField("**Number of tags**:", userAccount.Tags.Count, true)
                .AddField("**Number of reminders**: ", userAccount.Reminders.Count, true)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .WithFooter($"Requested by {Context.User.Username}")
                .WithThumbnailUrl(user.GetAvatarUrl())
                .Build();
            
            await Context.Channel.SendMessageAsync(Context.User.Mention, false, embed);
        }

        [Command("ShowCommandHistory"), Alias("CommandHistory")]
        public async Task ShowCommandHistory()
        {            
            await Context.Channel.SendMessageAsync(GetCommandHistory(Context.UserAccount));
        }
        
        //Could be in the extended ModuleBase, with a few changes
        private string GetCommandHistory(GlobalUserAccount userAccount)
        {
            var commandHistory = userAccount.CommandHistory.Select(cH => $"{cH.UsageDate.ToString("G")} {cH.Command}");
            return String.Join("\n", commandHistory); //Return the command history separated by line
        }
        
        [Command("GetAllMyAccountData"), Alias("GetMyData", "MyData")]
        public async Task GetAccountFile()
        {
            var userFilePath = GlobalUserAccounts.GetAccountFilePath(Context.User.Id);
            if (String.IsNullOrEmpty(userFilePath))
            {
                Context.Channel.SendMessageAsync("I don't have any information about you.");
                return;
            }

            await Context.User.SendFileAsync(userFilePath, $"This is all I got about you!");
            await Context.Channel.SendMessageAsync($"{Context.User.Mention} DM sent!");
        }

        [Command("DeleteAllMyAccountData", RunMode = RunMode.Async)]
        public async Task DeleteAccount()
        {
            var response = await AwaitMessageYesNo("I will delete all the data I know about you, are you sure?", "Yes", "No");
            if (response is null)
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention} you took so long to reply!");
            }
            else
            {
                await EvaluateResponse(response, "Yes");
            }
        }

        
        private async Task EvaluateResponse(SocketMessage response, string optionYes)
        {
            var message = "";
            if (response.Content.ToLower().Contains(optionYes.ToLower()))
            {
                message = GlobalUserAccounts.DeleteAccountFile(Context.User.Id)
                    ? "All your data have been deleted."
                    : "We don't have information about you or couldn't find it.";
            }
            else
            {
                message = "Alright, nevermind then!";
            }

            await Context.Channel.SendMessageAsync(Context.User.Mention + " " + message);
        }

        private async Task<SocketMessage> AwaitMessageYesNo(string message, string optionYes, string optionNo)
        {
            await Context.Channel.SendMessageAsync(
                $"{Context.User.Mention} {message}. Reply with `{optionYes}` or `{optionNo}`");
            var response = await Context.Channel.AwaitMessage(msg => EvaluateResponse(msg, optionYes, optionNo));
            return response;
        }

        private bool EvaluateResponse(SocketMessage arg, params String[] options)
            => options.Any(option => arg.Content.ToLower().Contains(option.ToLower()) && arg.Author == Context.User);
    }
}
