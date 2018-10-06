using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityBot.Configuration;
using CommunityBot.Extensions;
using CommunityBot.Features.Blogs;
using CommunityBot.Handlers;
using Discord;
using Discord.Commands;
using Newtonsoft.Json;

namespace CommunityBot.Modules
{
    [Group("Blog"), Summary("Enables you to create a block that people can subscribe to so they don't miss out if you publish a new one")]
    public class Blogs : ModuleBase<MiunieCommandContext>
    {
        private static readonly string blogFile = "blogs.json";
        [Command("Create"), Remarks("Create a new named block")]
        public async Task Create(string name)
        {
            await Context.Message.DeleteAsync();

            var blogs = DataStorage.RestoreObject<List<BlogItem>>(blogFile) ?? new List<BlogItem>();

            if (blogs.FirstOrDefault(k=>k.Name == name) == null)
            {
                var newBlog = new BlogItem
                {
                    BlogId = Guid.NewGuid(),
                    Author = Context.User.Id,
                    Name = name,
                    Subscribers = new List<ulong>()
                };

                blogs.Add(newBlog);

                DataStorage.StoreObject(blogs, blogFile, Formatting.Indented);
                
                var embed = EmbedHandler.CreateEmbed("Blog", $"Your blog {name} was created.", EmbedHandler.EmbedMessageType.Success);
                await Context.Channel.SendMessageAsync("", false, embed);
            }
            else
            {
                var embed = EmbedHandler.CreateEmbed("Blog :x:", $"There is already a Blog with the name {name}", EmbedHandler.EmbedMessageType.Error);
                await Context.Channel.SendMessageAsync("", false, embed);
            }
        }

        [Command("Post"), Remarks("Publish a new post to one of your named blocks")]
        public async Task Post(string name, [Remainder]string post)
        {
            await Context.Message.DeleteAsync();

            var blogs = DataStorage.RestoreObject<List<BlogItem>>(blogFile);

            var blog = blogs.FirstOrDefault(k => k.Name == name && k.Author == Context.User.Id);

            if (blog != null)
            {
                var subs = string.Empty;
                foreach (var subId in blog.Subscribers)
                {
                    var sub = Context.Guild.GetUser(subId);
                    
                    subs += $"{sub.Mention},";
                }

                if (string.IsNullOrEmpty(subs))
                {
                    subs = "No subs";
                }

                var embed = EmbedHandler.CreateBlogEmbed(blog.Name, post, subs, EmbedHandler.EmbedMessageType.Info, true);
                var msg = Context.Channel.SendMessageAsync("", false, embed);
                
                if (Global.MessagesIdToTrack == null)
                {
                    Global.MessagesIdToTrack = new Dictionary<ulong, string>();
                }

                Global.MessagesIdToTrack.Add(msg.Result.Id, blog.Name);

                await msg.Result.AddReactionAsync(new Emoji("➕"));
            }
        }

        [Command("Subscribe"), Remarks("Subscribe to a named blog to receive a message when a new post gets published")]
        public async Task Subscribe(string name)
        {
            await Context.Message.DeleteAsync();

            var embed = BlogHandler.SubscribeToBlog(Context.User.Id, name);

            await Context.Channel.SendMessageAsync("", false, embed);
        }

        [Command("Unsubscribe"), Remarks("Remove a subscription from a named block")]
        public async Task UnSubscribe(string name)
        {
            await Context.Message.DeleteAsync();

            var embed = BlogHandler.UnSubscribeToBlog(Context.User.Id, name);

            await Context.Channel.SendMessageAsync("", false, embed);
        }
    }
}
