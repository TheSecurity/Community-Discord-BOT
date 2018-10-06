using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityBot.Extensions;
using CommunityBot.Features.GlobalAccounts;
using CommunityBot.Providers;
using Discord;
using Discord.Commands;

namespace CommunityBot.Modules.RoleAssignments
{
    [Group("RoleByPhrase"), Alias("rbp"), Summary("Settings for auto-assigning roles based on a sent Phrase")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class RoleByPhrase : ModuleBase<MiunieCommandContext>
    {
        [Command("status"), Alias("s"), RequireUserPermission(GuildPermission.Administrator)]
        [Remarks("Returns the current state of RoleByPhrase lists and relations.")]
        public async Task RbpStatus()
        {
            var rbp = GlobalGuildAccounts.GetGuildAccount(Context.Guild).RoleByPhraseSettings;

            var phrases = rbp.Phrases.Any() ? string.Join("\n", rbp.Phrases.Select(p => $"({rbp.Phrases.IndexOf(p)}) - {p}")) : "No phrases stored\nAdd one with `rbp addPhrase YOUR-PHRASE`";
            var roles = rbp.RolesIds.Any() ? string.Join("\n", rbp.RolesIds.Select(r => $"({rbp.RolesIds.IndexOf(r)}) - {Context.Guild.GetRole(r).Name}")) : "No roles stored\nAdd one with `rbp addRole @SomeRole`";
            var relations = rbp.Relations.Any() ? string.Join("\n", rbp.Relations.Select(r => $"Phrase {r.PhraseIndex} => Role {r.RoleIdIndex}")) : "No relations created\nAdd one with `rbp addRelation PHRASE-ID ROLE-ID`";

            var embed = new EmbedBuilder();
            embed.WithTitle($"Role Assignments for {Context.Guild.Name}");
            embed.AddField("Phrases", phrases);
            embed.AddField("Roles", roles);
            embed.AddField("Relations", relations);

            await Context.Channel.SendMessageAsync("", embed: embed.Build());
        }

        [Command("addPhrase"), Alias("ap"), RequireUserPermission(GuildPermission.Administrator)]
        [Remarks("Adds a new phrase to the guild's settings. (Phrase is a Remainder, so no double quotes are needed)")]
        public async Task RbpAddPhrase([Remainder]string phrase)
        {
            var result = RoleByPhraseProvider.AddPhrase(Context.Guild, phrase);

            if (result == RoleByPhraseProvider.RoleByPhraseOperationResult.Success)
            {
                await RbpStatus();
            }
            else
            {
                await ReplyAsync("Something went wrong...");
                Console.WriteLine(result.ToString());
            }
        }

        [Command("addRole"), Alias("arole"), RequireUserPermission(GuildPermission.Administrator)]
        [Remarks("Adds a new phrase to the guild's settings. (Phrase is a Remainder, so no double quotes are needed)")]
        public async Task RbpAddRole(IRole role)
        {
            var result = RoleByPhraseProvider.AddRole(Context.Guild, role);

            if (result == RoleByPhraseProvider.RoleByPhraseOperationResult.Success)
            {
                await RbpStatus();
            }
            else
            {
                await ReplyAsync("Something went wrong...");
                Console.WriteLine(result.ToString());
            }
        }

        [Command("addRelation"), Alias("arel"), RequireUserPermission(GuildPermission.Administrator)]
        [Remarks("Adds a new relation between a phrase and a role. Relation are automatically enabled and used after you add them.")]
        public async Task RbpAddRelation(int phraseIndex, int roleIndex)
        {
            var result = RoleByPhraseProvider.CreateRelation(Context.Guild, phraseIndex, roleIndex);

            if (result == RoleByPhraseProvider.RelationCreationResult.Success)
            {
                await RbpStatus();
            }
            else
            {
                await ReplyAsync("Something went wrong...");
                Console.WriteLine(result.ToString());
            }
        }

        [Command("removeRelation"), Alias("rrel"), RequireUserPermission(GuildPermission.Administrator)]
        [Remarks("Removes a relation between a phrase and a role.")]
        public async Task RbpRemoveRelation(int phraseIndex, int roleIndex)
        {
            RoleByPhraseProvider.RemoveRelation(Context.Guild, phraseIndex, roleIndex);
            await RbpStatus();
        }

        [Command("removePhrase"), Alias("rp"), RequireUserPermission(GuildPermission.Administrator)]
        [Remarks("Removes a phrase and its relations.")]
        public async Task RbpRemovePhrase(int phraseIndex)
        {
            RoleByPhraseProvider.RemovePhrase(Context.Guild, phraseIndex);
            await RbpStatus();
        }

        [Command("removeRole"), Alias("rrole"), RequireUserPermission(GuildPermission.Administrator)]
        [Remarks("Removes a role and its relations.")]
        public async Task RbpRemoveRole(int roleIndex)
        {
            RoleByPhraseProvider.RemoveRole(Context.Guild, roleIndex);
            await RbpStatus();
        }
    }
}