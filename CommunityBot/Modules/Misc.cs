using CommunityBot.Preconditions;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using CommunityBot.Helpers;
using System.Globalization;
using CommunityBot.Extensions;
using CommunityBot.Features.Lists;
using Discord.WebSocket;
using Discord.Rest;

namespace CommunityBot.Modules
{
    public class Misc : ModuleBase<MiunieCommandContext>
    {
        private CommandService _service;

        public Misc(CommandService service)
        {
            _service = service;
        }

        [Cooldown(15)]
        [Command("help"), Alias("h"),
         Remarks(
             "DMs you a huge message if called without parameter - otherwise shows help to the provided command or module")]
        public async Task Help()
        {
            await Context.Channel.SendMessageAsync("Check your DMs.");

            var dmChannel = await Context.User.GetOrCreateDMChannelAsync();

            var contextString = Context.Guild?.Name ?? "DMs with me";
            var builder = new EmbedBuilder()
            {
                Title = "Help",
                Description = $"These are the commands you can use in {contextString}",
                Color = new Color(114, 137, 218)
            };

            foreach (var module in _service.Modules)
            {
                await AddModuleEmbedField(module, builder);
            }

            await dmChannel.SendMessageAsync("", false, builder.Build());

            // Embed are limited to 24 Fields at max. So lets clear some stuff
            // out and then send it in multiple embeds if it is too big.
            builder.WithTitle("")
                .WithDescription("")
                .WithAuthor("");
            while (builder.Fields.Count > 24)
            {
                builder.Fields.RemoveRange(0, 25);
                await dmChannel.SendMessageAsync("", false, builder.Build());

            }
        }

        [Command("version"), Alias("ver")]
        [Remarks("Returns the current version of the bot.")]
        [Cooldown(5)]
        public async Task Version()
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.Color = new Color(114, 137, 218);
            builder.AddField("Version", $"The current version of the bot is: `{Global.version}`");
            await ReplyAsync("", false, builder.Build());
        }


        [Command("help"), Alias("h")]
        [Remarks("Shows what a specific command or module does and what parameters it takes.")]
        [Cooldown(5)]
        public async Task HelpQuery([Remainder] string query)
        {
            var builder = new EmbedBuilder()
            {
                Color = new Color(114, 137, 218),
                Title = $"Help for '{query}'"
            };

            var result = _service.Search(Context, query);
            if (query.StartsWith("module "))
                query = query.Remove(0, "module ".Length);
            var emb = result.IsSuccess ? HelpCommand(result, builder) : await HelpModule(query, builder);

            if (emb.Fields.Length == 0)
            {
                await ReplyAsync($"Sorry, I couldn't find anything for \"{query}\".");
                return;
            }

            await Context.Channel.SendMessageAsync("", false, emb);
        }

        private static Embed HelpCommand(SearchResult search, EmbedBuilder builder)
        {
            foreach (var match in search.Commands)
            {
                var cmd = match.Command;
                var parameters = cmd.Parameters.Select(p => string.IsNullOrEmpty(p.Summary) ? p.Name : p.Summary);
                var paramsString = $"Parameters: {string.Join(", ", parameters)}" +
                                   (string.IsNullOrEmpty(cmd.Remarks) ? "" : $"\nRemarks: {cmd.Remarks}") +
                                   (string.IsNullOrEmpty(cmd.Summary) ? "" : $"\nSummary: {cmd.Summary}");

                builder.AddField(x =>
                {
                    x.Name = string.Join(", ", cmd.Aliases);
                    x.Value = paramsString;
                    x.IsInline = false;
                });
            }

            return builder.Build();
        }

        private async Task<Embed> HelpModule(string moduleName, EmbedBuilder builder)
        {
            var module = _service.Modules.ToList().Find(mod =>
                string.Equals(mod.Name, moduleName, StringComparison.CurrentCultureIgnoreCase));
            await AddModuleEmbedField(module, builder);
            return builder.Build();
        }

        private async Task AddModuleEmbedField(ModuleInfo module, EmbedBuilder builder)
        {
            if (module == null) return;
            var descriptionBuilder = new List<string>();
            var duplicateChecker = new List<string>();
            foreach (var cmd in module.Commands)
            {
                var result = await cmd.CheckPreconditionsAsync(Context);
                if (!result.IsSuccess || duplicateChecker.Contains(cmd.Aliases.First())) continue;
                duplicateChecker.Add(cmd.Aliases.First());
                var cmdDescription = $"`{cmd.Aliases.First()}`";
                if (!string.IsNullOrEmpty(cmd.Summary))
                    cmdDescription += $" | {cmd.Summary}";
                if (!string.IsNullOrEmpty(cmd.Remarks))
                    cmdDescription += $" | {cmd.Remarks}";
                if (cmdDescription != "``")
                    descriptionBuilder.Add(cmdDescription);
            }

            if (descriptionBuilder.Count <= 0) return;
            var builtString = string.Join("\n", descriptionBuilder);
            var testLength = builtString.Length;
            if (testLength >= 1024)
            {
                throw new ArgumentException("Value cannot exceed 1024 characters");
            }
            var moduleNotes = "";
            if (!string.IsNullOrEmpty(module.Summary))
                moduleNotes += $" {module.Summary}";
            if (!string.IsNullOrEmpty(module.Remarks))
                moduleNotes += $" {module.Remarks}";
            if (!string.IsNullOrEmpty(moduleNotes))
                moduleNotes += "\n";
            if (!string.IsNullOrEmpty(module.Name))
            {
                builder.AddField($"__**{module.Name}:**__",
                    $"{moduleNotes} {builtString}\n{Constants.InvisibleString}");
            }
        }

        [Command("credits")]
        [Summary("Shows everyone who has worked on and contributed to me")]
        public async Task Credits()
        {
            var embB = new EmbedBuilder()
                .WithTitle("Credits")
                .WithColor(Color.Blue)
                .WithUrl("https://www.github.com/repos/petrspelos/Community-Discord-BOT/")
                .WithFooter(Global.GetRandomDidYouKnow())
                // Someone needs to pimp this message... it is lame
                .WithDescription("Peter is the one who created me... fleshed me out and taught me how to speak.\n" +
                                 "Everything was organized... my life was good :smiley:\n" +
                                 "And then he let those people lose on me... :scream:\n");


            var contributions = await GitHub.Contributions("petrspelos", "Community-Discord-BOT");
            // Sort contributions by commits
            contributions = contributions.OrderByDescending(contribution => contribution.total).ToList();
            // Creating the embeds with all the contributers and their stats
            embB = contributions.Aggregate(embB, (emb, cont) =>
            {
                // Accumulate all the weeks stats to the total stat
                var stats = cont.weeks.Aggregate(
                    Tuple.Create(0, 0),
                    (acc, week) => Tuple.Create(acc.Item1 + week.a, acc.Item2 + week.d)
                );
                return emb.AddField(GitHub.ContributionStat(cont, stats));
            });

            await ReplyAsync("", false, embB.Build());
        }

        [Command("bug")]
        [Alias("bug-report", "issue", "feedback")]
        [Summary("It sends users where to report bugs.")]
        public async Task Bug()
        {
            var embed = new EmbedBuilder();
            embed.WithColor(99, 193, 50);
            embed.WithTitle("Bug reporting");
            embed.WithDescription(@"Thank you for your interest, how about you let us know by creating an Issue on our **GitHub** " + "\n\n\n" +
            "**[ 🢂 🐞 HERE 🐞 🢀 ](https://github.com/discord-bot-tutorial/Community-Discord-BOT/issues/new/choose)**" + "\n\n\n" +
            "(*If button doesnt work: https://github.com/discord-bot-tutorial/Community-Discord-BOT/issues/new/choose*)");
            embed.WithFooter("Your help is more than welcome!");
            embed.WithAuthor(Global.Client.CurrentUser);
            embed.WithCurrentTimestamp();

            await ReplyAsync("", false, embed.Build());

        }

        [Command("Addition")]
        [Summary("Adds 2 numbers together.")]
        public async Task AddAsync(float num1, float num2)
        {
            await ReplyAsync($"The Answer To That Is: {num1 + num2}");
        }

        [Command("Subtract")]
        [Summary("Subtracts 2 numbers.")]
        public async Task SubstractAsync(float num1, float num2)
        {
            await ReplyAsync($"The Answer To That Is: {num1 - num2}");
        }

        [Command("Multiply")]
        [Summary("Multiplys 2 Numbers.")]
        public async Task MultiplyAsync(float num1, float num2)
        {
            await ReplyAsync($"The Answer To That Is {num1 * num2}");
        }

        [Command("Divide")]
        [Summary("Divides 2 Numbers.")]
        public async Task DivideAsync(float num1, float num2)
        {
            await ReplyAsync($"The Answer To That Is: {num1 / num2}");
        }
        
        [Command("Math")]
        [Summary("Computes mathematical operations.")]
        public async Task Computate(params String[] input)
        {
            StringBuilder word = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                char[] inputWithoutSpaces = input.ElementAt(i).Where(c => !Char.IsWhiteSpace(c)).ToArray();
                for (int j = 0; j < inputWithoutSpaces.Count(); j++)
                {
                    word.Append(inputWithoutSpaces[j]);
                }

                input[i] = word.ToString();
                word = new StringBuilder();
                if (input.ElementAt(i).Length > 2)
                {
                    input[i] = Operations.PerformComputation(input[i]).ToString(CultureInfo.CurrentCulture);
                }
            }
            StringBuilder sentence = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                sentence.Append(input[i]);
            }

            await ReplyAsync($"{Operations.PerformComputation(sentence.ToString())}");
        }

        [Command("List")]
        [Summary("Manage List")]
        public async Task ManageList(params String[] input)
        {
            await ListManager.HandleIO(Context, input);
        }
    }
}
