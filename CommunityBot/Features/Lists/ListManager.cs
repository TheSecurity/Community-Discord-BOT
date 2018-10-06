using System;
using System.Collections.Generic;
using System.Text;
using CommunityBot.Configuration;
using System.Linq;
using Discord;
using CommunityBot.Helpers;
using Discord.WebSocket;
using Discord.Rest;
using Discord.Commands;
using System.Threading.Tasks;
using static CommunityBot.Features.Lists.ListException;

namespace CommunityBot.Features.Lists
{
    public static class ListManager
    {
        private static readonly string ListManagerLookup = "list_manager_lookup.json";

        private static string LineIndicator = " <--";

        private static Dictionary<ulong, ulong> ListenForReactionMessages = new Dictionary<ulong, ulong>();

        public static IReadOnlyDictionary<string, Emoji> ControlEmojis = new Dictionary<string, Emoji>
        {
            {"up", new Emoji("⬆") },
            {"down", new Emoji("⬇") },
            {"check", new Emoji("✅") }
        };

        private static IReadOnlyDictionary<string, Func<ulong, string[], ListOutput>> ValidOperations = new Dictionary<string, Func<ulong, string[], ListOutput>>
        {
            { "-g", GetAllPrivate },
            { "-gp", GetAllPublic },
            { "-c", CreateListPrivate },
            { "-cp", CreateListPublic },
            { "-a", Add },
            { "-i", Insert },
            { "-l", OutputList },
            { "-r", Remove },
            { "-rl", RemoveList },
            { "-cl", Clear }
        };

        private static List<CustomList> Lists = new List<CustomList>();

        static ListManager()
        {
            RestoreOrCreateLists();
        }

        public static async Task HandleIO(SocketCommandContext context, params string[] input)
        {
            ListManager.ListOutput output;
            try
            {
                output = ListManager.Manage(context.User.Id, input);
            }
            catch (ListManagerException e)
            {
                output = ListManager.GetListOutput(e.Message, CustomList.ListPermission.PUBLIC);
            }
            catch (ListPermissionException e)
            {
                output = ListManager.GetListOutput(e.Message, CustomList.ListPermission.PUBLIC);
            }
            RestUserMessage message;
            if (output.permission == null || output.permission != CustomList.ListPermission.PRIVATE)
            {
                message = (RestUserMessage)await context.Channel.SendMessageAsync(output.outputString, false, output.outputEmbed);
            }
            else
            {
                var dmChannel = await context.User.GetOrCreateDMChannelAsync();
                message = (RestUserMessage)await dmChannel.SendMessageAsync(output.outputString, false, output.outputEmbed);
            }
            if (output.listenForReactions)
            {
                await message.AddReactionAsync(ListManager.ControlEmojis["up"]);
                await message.AddReactionAsync(ListManager.ControlEmojis["down"]);
                await message.AddReactionAsync(ListManager.ControlEmojis["check"]);
                ListManager.ListenForReactionMessages.Add(message.Id, context.User.Id);
            }
        }

        public static ListOutput Manage(ulong userId, params string[] input)
        {
            var sa = SeperateArray(input, 0);
            string command = sa.seperated;
            string[] values = sa.array;

            ListOutput result;
            try
            {
                result = ValidOperations[command](userId, values);
            }
            catch (KeyNotFoundException)
            {
                throw GetListManagerException(ListErrorMessage.General.UnknownCommand_command, command);
            }
            return result;
        }

        public static ListOutput GetAllPrivate(ulong userId, params string[] input)
        {
            return GetAll(userId, CustomList.ListPermission.PRIVATE, input);
        }

        public static ListOutput GetAllPublic(ulong userId, params string[] input)
        {
            return GetAll(userId, CustomList.ListPermission.PUBLIC, input);
        }

        private static ListOutput GetAll(ulong userId, CustomList.ListPermission outputPermission, params string[] input)
        {
            if (Lists.Count == 0) { throw GetListManagerException(ListErrorMessage.General.NoLists); }

            Func<int, int, int> GetLonger = (i1, i2) => { return (i1 > i2 ? i1 : i2); };

            var tableValuesList = new Dictionary<String, String>();

            for (int i = 0; i < Lists.Count; i++)
            {
                CustomList l = Lists[i];
                if (l.IsAllowedToList(userId))
                {
                    tableValuesList.Add(l.Name, $"{l.Count()} {GetNounPlural("item", l.Count())}");
                }
            }
            var maxItemLength = 0;
            var tableValues = new string[tableValuesList.Count,2];
            for (int i=0; i<tableValues.GetLength(0); i++)
            {
                var keyPair = tableValuesList.ElementAt(i);
                tableValues[i, 0] = keyPair.Key;
                tableValues[i, 1] = keyPair.Value;
                maxItemLength = GetLonger(maxItemLength, keyPair.Key.Length);
                maxItemLength = GetLonger(maxItemLength, keyPair.Value.Length);
            }

            var header = new[] { "List name", "Item count" };
            foreach (string s in header)
            {
                maxItemLength = GetLonger(maxItemLength, s.Length);
            }
            var tableSettings = new MessageFormater.TableSettings("All lists", header, -(maxItemLength), true);
            string output = MessageFormater.CreateTable(tableSettings, tableValues);
            var count = 0;
            for (int i=0; i<output.Length; i++)
            {
                if (output[i] == '\n')
                {
                    if (count == 8)
                    {
                        output = output.Insert(i, LineIndicator);
                        break;
                    }
                    else
                    {
                        count++;
                    }
                }
            }

            var returnValue = GetListOutput(output, outputPermission);
            returnValue.listenForReactions = true;
            return returnValue;
        }

        public static async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cacheMessage, SocketReaction reaction)
        {
            if ( ListenForReactionMessages.ContainsKey(reaction.MessageId) )
            {
                reaction.Message.Value.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                if (ListenForReactionMessages[reaction.MessageId] == reaction.User.Value.Id)
                {
                    if (reaction.Emote.Name == ControlEmojis["up"].Name)
                    {
                        await HandleMovement(reaction, cacheMessage.Value.Content, true);
                    }
                    else if (reaction.Emote.Name == ControlEmojis["down"].Name)
                    {
                        await HandleMovement(reaction, cacheMessage.Value.Content, false);
                    }
                    else if (reaction.Emote.Name == ControlEmojis["check"].Name)
                    {
                        var seperatedMessage = SepereateMessageByLines(cacheMessage.Value.Content);
                        foreach (string s in seperatedMessage)
                        {
                            if (ContainsLineIndicator(s))
                            {
                                reaction.Message.Value.DeleteAsync();
                                ListenForReactionMessages.Remove(reaction.MessageId);
                                var listName = s.Split('|', StringSplitOptions.RemoveEmptyEntries)[0];
                                listName = listName.Remove(0, 1);
                                listName = listName.Remove(listName.Length-1, 1);
                                var context = new SocketCommandContext(Global.Client, reaction.Message.Value);
                                await HandleIO(context, new[] { "-l", listName }).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        private static async Task HandleMovement(SocketReaction reaction, string message, bool dirUp)
        {
            var seperatedMessage = SepereateMessageByLines(message);
            reaction.Message.Value.ModifyAsync(msg => msg.Content = PerformMove(seperatedMessage, dirUp));
        }

        private static string[] SepereateMessageByLines(string message)
        {
            return message.Split('\n');
        }

        private static string PerformMove(string[] messageLines, bool dirUp)
        {
            var newMessageLines = new string[messageLines.Length];
            messageLines.CopyTo(newMessageLines, 0);

            
            for (int i=0; i<messageLines.Length; i++)
            {
                var line = messageLines[i];
                var substringStart = line.Length - LineIndicator.Length;
                if (substringStart < 0) { continue; }

                var subLine = line.Substring(substringStart);
                if (subLine.Equals(LineIndicator))
                {
                    var newIndex = i + (dirUp ? -2 : 2);
                    if (newIndex > 7 && newIndex < messageLines.Length-1)
                    {
                        newMessageLines[i] = line.Substring(0, substringStart);
                        newMessageLines[newIndex] = messageLines[newIndex] + LineIndicator;
                    }
                    break;
                }
            }
            var newMessage = new StringBuilder();
            foreach(string s in newMessageLines)
            {
                newMessage.Append($"{s}\n");
            }
            return newMessage.ToString();
        }

        private static bool ContainsLineIndicator(string line)
        {
            var substringLength = line.Length - LineIndicator.Length;
            if (substringLength < 0) { return false; }

            var subLine = line.Substring(substringLength);
            return (subLine.Equals(LineIndicator));
        }

        public static ListOutput CreateListPrivate(ulong userId, params string[] input)
        {
            return CreateList(userId, CustomList.ListPermission.PRIVATE, input);
        }

        public static ListOutput CreateListPublic(ulong userId, params string[] input)
        {
            return CreateList(userId, CustomList.ListPermission.PUBLIC, input);
        }

        private static ListOutput CreateList(ulong userId, CustomList.ListPermission listPermissions, params string[] input)
        {
            if (input.Length == 0 || input.Length > 2)
            {
                throw GetListManagerException(ListErrorMessage.General.WrongFormat);
            }
            var listName = input[0];

            try
            {
                GetList(userId, listName);
            }
            catch (ListManagerException)
            {
                var newList = new CustomList(userId, listPermissions, listName);
                newList.SaveList();

                Lists.Add(newList);

                WriteContents();

                var permissionAsString = CustomList.permissionStrings[(int)listPermissions];
                return GetListOutput($"Created {permissionAsString} list '{listName}'", listPermissions);
            }
            throw GetListManagerException(ListErrorMessage.General.ListAlreadyExists_list, listName);
        }

        public static CustomList GetList(ulong userId, params string[] input)
        {
            CustomList list = Lists.Find(l => l.Name.Equals(input[0]));
            if (list == null)
            {
                throw GetListManagerException(ListErrorMessage.General.ListDoesNotExist_list, input[0]);
            }
            return list;
        }

        public static ListOutput Add(ulong userId, string[] input)
        {
            if (input.Length < 2)
            {
                throw GetListManagerException(ListErrorMessage.General.WrongFormat);
            }

            var sa = SeperateArray(input);
            string name = sa.seperated;
            string[] values = sa.array;

            var list = GetList(userId, name);
            CheckPermissionWrite(userId, list);

            list.AddRange(values);

            string output = $"Added {GetNounPlural("item", (values.Length))}";
            return GetListOutput(output);
        }

        public static ListOutput Insert(ulong userId, string[] input)
        {
            if (input.Length < 3) { throw GetListManagerException(); }

            var sa = SeperateArray(input);
            string name = sa.seperated;
            string[] values = sa.array;

            sa = SeperateArray(values, 0);
            string indexstring = sa.seperated;
            values = sa.array;

            int index = 0;
            try
            {
                index = int.Parse(indexstring) - 1;
            }
            catch (FormatException e)
            {
                throw GetListManagerException(ListErrorMessage.General.WrongInputForIndex);
            }

            var list = GetList(userId, name);
            CheckPermissionWrite(userId, list);

            if (index < 0 || index > list.Count())
            {
                throw GetListManagerException(ListErrorMessage.General.IndexOutOfBounds_list, list.Name);
            }

            list.InsertRange(index, values);

            string output = $"Inserted {GetNounPlural("value", values.Length)}";
            return GetListOutput(output);
        }

        public static ListOutput RemoveList(ulong userId, params string[] input)
        {
            if (input.Length != 1)
            {
                throw GetListManagerException(ListErrorMessage.General.WrongFormat);
            }

            CustomList list = GetList(userId, input[0]);
            CheckPermissionWrite(userId, list);

            list.Delete();
            Lists.Remove(list);

            WriteContents();

            string output = $"Removed list '{input[0]}'";
            return GetListOutput(output);
        }

        public static ListOutput Remove(ulong userId, string[] input)
        {
            if (input.Length != 2)
            {
                throw GetListManagerException(ListErrorMessage.General.WrongFormat);
            }

            var sa = SeperateArray(input);
            string name = sa.seperated;
            string[] values = sa.array;

            var list = GetList(userId, name);
            CheckPermissionWrite(userId, list);

            list.Remove(values[0]);

            string output = $"Removed '{values[0]}' from the list";
            return GetListOutput(output);
        }

        public static ListOutput OutputList(ulong userId, params string[] input)
        {
            if (input.Length != 1)
            {
                throw GetListManagerException(ListErrorMessage.General.WrongFormat);
            }

            CustomList list = GetList(userId, input[0]);
            
            CheckPermissionWrite(userId, list);

            if (list.Count() == 0)
            {
                throw GetListManagerException(ListErrorMessage.General.ListIsEmpty_list, input[0]);
            }

            Func<int, int, int> GetLonger = (i1, i2) => { return (i1 > i2 ? i1 : i2); };

            var maxItemLength = 0;
            var values = new string[list.Count(), 1];
            for (int i = 0; i < list.Count(); i++)
            {
                var row = $"{(i+1).ToString()}: {list.Contents[i]}";
                values[i, 0] = row;
                maxItemLength = GetLonger(maxItemLength, row.Length);
            }
            maxItemLength = GetLonger(maxItemLength, list.Name.Length);

            var tableSettings = new MessageFormater.TableSettings(list.Name, -(maxItemLength), false);
            string output = MessageFormater.CreateTable(tableSettings, values);

            return GetListOutput(output, list.Permission);
        }

        public static ListOutput Clear(ulong userId, string[] input)
        {
            if (input.Length != 1)
            {
                throw GetListManagerException(ListErrorMessage.General.WrongFormat);
            }

            var list = GetList(userId, input[0]);
            CheckPermissionWrite(userId, list);

            list.Clear();

            string output = $"Cleared list '{input[0]}'";
            return GetListOutput(output);
        }

        private static SeperatedArray SeperateArray(string[] input)
        {
            return SeperateArray(input, input.Length - 1);
        }

        private static SeperatedArray SeperateArray(string[] input, int index)
        {
            var sa = new SeperatedArray();
            sa.seperated = input[index];
            sa.array = new string[input.Length - 1];
            for (int i = 0; i < input.Length; i++)
            {
                if (i < index)
                {
                    sa.array[i] = input[i];
                }
                else if (i > index)
                {
                    sa.array[i - 1] = input[i];
                }
            }
            return sa;
        }

        private struct SeperatedArray
        {
            public string seperated { get; set; }
            public string[] array { get; set; }
        }

        public struct ListOutput : IEquatable<object>
        {
            public string outputString { get; set; }
            public Embed outputEmbed { get; set; }
            public bool listenForReactions { get; set; }
            public CustomList.ListPermission permission { get; set; }
        }

        public static ListOutput GetListOutput(string s)
        {
            return new ListOutput { outputString = s, permission = CustomList.ListPermission.PUBLIC };
        }

        public static ListOutput GetListOutput(string s, CustomList.ListPermission p)
        {
            return new ListOutput { outputString = s, permission = p };
        }

        public static ListOutput GetListOutput(Embed e)
        {
            return new ListOutput { outputEmbed = e, permission = CustomList.ListPermission.PUBLIC };
        }

        public static ListOutput GetListOutput(Embed e, CustomList.ListPermission p)
        {
            return new ListOutput { outputEmbed = e, permission = p };
        }

        private static string GetNounPlural(string s, int count)
        {
            return $"{s}{(count < 2 ? "" : "s")}";
        }

        public static void WriteContents()
        {
            List<string> listNames = Lists.Select(l => l.Name).ToList<string>();
            DataStorage.StoreObject(listNames, ListManagerLookup, false);
        }

        public static void RestoreOrCreateLists()
        {
            Lists = new List<CustomList>();

            var listNames = DataStorage.RestoreObject<List<string>>(ListManagerLookup);
            if (listNames == null) { return; }
            
            foreach (string name in listNames)
            {
                var l = CustomList.RestoreList(name);
                if (l != null)
                {
                    Lists.Add(l);
                }
            }
        }

        public static ListManagerException GetListManagerException()
        {
            return GetListManagerException(ListErrorMessage.General.UnknownError);
        }

        public static ListManagerException GetListManagerException(string message, params string[] parameters)
        {
            var formattedMessage = String.Format(message, parameters);
            return new ListManagerException(formattedMessage);
        }

        public static ListPermissionException GetListPermissionException()
        {
            return GetListPermissionException(ListErrorMessage.Permission.NoPermission_list);
        }

        public static ListPermissionException GetListPermissionException(string message, params string[] parameters)
        {
            var formattedMessage = String.Format(message, parameters);
            return new ListPermissionException(formattedMessage);
        }

        private static void CheckPermissionList(ulong userId, CustomList list)
        {
            if (!list.IsAllowedToList(userId))
            {
                throw GetListManagerException(ListErrorMessage.Permission.NoPermission_list, list.Name);
            }
        }

        private static void CheckPermissionRead(ulong userId, CustomList list)
        {
            if (!list.IsAllowedToRead(userId))
            {
                throw GetListManagerException(ListErrorMessage.Permission.NoPermission_list, list.Name);
            }
        }

        private static void CheckPermissionWrite(ulong userId, CustomList list)
        {
            if (!list.IsAllowedToWrite(userId))
            {
                throw GetListManagerException(ListErrorMessage.Permission.NoPermission_list, list.Name);
            }
        }
    }
}
