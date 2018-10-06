using CommunityBot.Features.Economy;
using CommunityBot.Features.GlobalAccounts;
using Discord;
using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;
using CommunityBot.Extensions;
using static CommunityBot.Global;
using static CommunityBot.Features.Economy.Transfer;
// ReSharper disable ConvertIfStatementToSwitchStatement

namespace CommunityBot.Modules
{
    public class Economy : ModuleBase<MiunieCommandContext>
    {
        [Command("Daily"), Remarks("Gives you some Miunies but can only be used once a day")]
        [Alias("GetDaily", "ClaimDaily")]
        public async Task GetDaily()
        {
            var result = Daily.GetDaily(Context.User.Id);

            if (result.Success)
            {
                await ReplyAsync($"Here's {Constants.DailyMuiniesGain} miunies, {Context.User.Mention}! Just for you...");
            }
            else
            {
                var timeSpanString = string.Format("{0:%h} hours {0:%m} minutes {0:%s} seconds", result.RefreshTimeSpan);
                await ReplyAsync($"You already got your daily, {Context.User.Mention}.\nCome back in {timeSpanString}.");
            }
        }

        [Command("Miunies"), Remarks("Shows how many Miunies you have")]
        [Alias("Cash", "Money")]
        public async Task CheckMiunies()
        {
            var account = GlobalUserAccounts.GetUserAccount(Context.User.Id);
            await ReplyAsync(GetMiuniesReport(account.Miunies, Context.User.Mention));
        }

        [Command("Miunies"), Remarks("Shows how many Miunies the mentioned user has")]
        [Alias("Cash", "Money")]
        public async Task CheckMiuniesOther(IGuildUser target)
        {
            var account = GlobalUserAccounts.GetUserAccount(target.Id);
            await ReplyAsync(GetMiuniesReport(account.Miunies, target.Mention));
        }

        [Command("Richest"), Remarks("Shows a user list of the sorted by Miunies. Pageable to see lower ranked users.")]
        [Alias("Top", "Top10")]
        public async Task ShowRichesPeople(int page = 1)
        {
            if (page < 1)
            {
                await ReplyAsync("Are you really trying that right now? **REALLY?**");
                return;
            }

            var guildUserIds = Context.Guild.Users.Select(user => user.Id);
            // Get only accounts of this server
            var accounts = GlobalUserAccounts.GetFilteredAccounts(acc => guildUserIds.Contains(acc.Id));

            const int usersPerPage = 9;
            // Calculate the highest accepted page number => amount of pages we need to be able to fit all users in them
            // (amount of users) / (how many to show per page + 1) results in +1 page more every time we exceed our usersPerPage  
            var lastPageNumber = 1 + (accounts.Count / (usersPerPage+1));
            if (page > lastPageNumber)
            {
                await ReplyAsync($"There are not that many pages...\nPage {lastPageNumber} is the last one...");
                return;
            }
            // Sort the accounts descending by Minuies
            var ordered = accounts.OrderByDescending(acc => acc.Miunies).ToList();

            var embB = new EmbedBuilder()
                .WithTitle("These are the richest people:")
                .WithFooter($"Page {page}/{lastPageNumber}");

            // Add fields to the embed with information of users according to the provided page we should show
            // Two conditions because:  1. Only get as many as we want 
            //                          2. The last page might not be completely filled so we have to interrupt early
            page--;
            for (var i = 1; i <= usersPerPage && i + usersPerPage * page <= ordered.Count; i++)
            {
                // -1 because we take the users non zero based input
                var account = ordered[i - 1 + usersPerPage * page];
                var user = Context.Client.GetUser(account.Id);

				//try to give it a medal in cases 1 - 3, if it is not possible just send it with out change
	            var contentName = string.Empty;
	            if (page == 0)
	            {
		            switch (i)
		            {
			            case 1:
				            contentName = $"🥇 #{i + usersPerPage * page} {user.Username}";
				            break;
			            case 2:
				            contentName = $"🥈 #{i + usersPerPage * page} {user.Username}";
				            break;
			            case 3:
				            contentName = $"🥉 #{i + usersPerPage * page} {user.Username}";
				            break;
			            default:
				            contentName = $"#{i + usersPerPage * page} {user.Username}";
				            break;
		            }
				}
	            else
	            {
					contentName = $"#{i + usersPerPage * page} {user.Username}";
				}
                embB.AddField(contentName, $"{account.Miunies} Miunies", true);
            }

            await ReplyAsync("", false, embB.Build());
        }

        [Command("Transfer")]
        [Remarks("Transferrs specified amount of your Minuies to the mentioned person.")]
        [Alias("Give", "Gift")]
        public async Task TransferMinuies(IGuildUser target, ulong amount)
        {
            // Class name left for readability
            // UserToUser alone doesn't mean much.
            var result = Transfer.UserToUser(Context.User, target, amount);

            if (result == TransferResult.SelfTransfer)
            {
                await ReplyAsync(":negative_squared_cross_mark: You can't gift yourself...\n**And you KNOW it!**");
            }
            else if (result == TransferResult.NotEnoughMiunies)
            {
                var userAccount = GlobalUserAccounts.GetUserAccount(Context.User.Id);
                await ReplyAsync($":negative_squared_cross_mark: You don't have that much Minuies! You only have {userAccount.Miunies}.");
            }
            else if (result == TransferResult.Success)
            {
                await ReplyAsync($":white_check_mark: {Context.User.Username} has given {target.Username} {amount} Minuies!");
            }
        }

        public string GetMiuniesReport(ulong miunies, string mention)
        {
            return $"{mention} has **{miunies} miunies**! {GetMiuniesCountReaction(miunies, mention)} \n\nDid you know?\n`{GetRandomDidYouKnow()}`";
        }

        [Command("newslot"), Remarks("Creates a new slot machine if you feel the current one is unlucky")]
        [Alias("newslots")]
        public async Task NewSlot(int amount = 0)
        {
            Global.Slot = new Slot(amount);
            await ReplyAsync("A new slotmachine got generated! Good luck with this puppy!");
        }

        [Command("slots"), Remarks("Play the slots! Win or lose some Miunies!")]
        [Alias("slot")]
        public async Task SpinSlot(uint amount)
        {
            if (amount < 1)
            {
                await ReplyAsync("You can' spin for that amount of Miunies.\nAND YOU KNOW IT!");
                return;
            }
            var account = GlobalUserAccounts.GetUserAccount(Context.User.Id);
            if (account.Miunies < amount)
            {
                await ReplyAsync($"Sorry but it seems like you don't have enough Minuies... You only have {account.Miunies}.");
                return;
            }

            account.Miunies -= amount;
            GlobalUserAccounts.SaveAccounts(Context.User.Id);

            var slotEmojis = Global.Slot.Spin();
            var payoutAndFlavour = Global.Slot.GetPayoutAndFlavourText(amount);

            if (payoutAndFlavour.Item1 > 0)
            {
                account.Miunies += payoutAndFlavour.Item1;
                GlobalUserAccounts.SaveAccounts();
            }            

            await ReplyAsync(slotEmojis);
            await Task.Delay(1000);
            await ReplyAsync(payoutAndFlavour.Item2);
        }

        [Command("showslots"), Remarks("Shows the configuration of the current slot machine")]
        [Alias("showslot")]
        public async Task ShowSlot()
        {
            await ReplyAsync(string.Join("\n", Global.Slot.GetCylinderEmojis(true)));
        }
    }
}
