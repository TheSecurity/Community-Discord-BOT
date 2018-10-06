using System;
using System.Collections.Generic;
using System.Text;
using CommunityBot.Configuration;
using System.IO;
using Discord.WebSocket;

namespace CommunityBot.Features.Lists
{
    public class CustomList
    {
        public enum ListPermission
        {
            PRIVATE,
            PUBLIC,
            READ,
            LIST
        };

        public static IReadOnlyList<String> permissionStrings = new List<String>
        {
            "private",
            "public",
            "read only",
            "view only"
        };

        public static readonly IReadOnlyDictionary<string, ListPermission> validPermissions = new Dictionary<string, ListPermission>
        {
            { "-p", ListPermission.PRIVATE },
            { "-pu", ListPermission.PUBLIC },
            { "-r", ListPermission.READ },
            { "-l", ListPermission.LIST }
        };

        public string Name { get; set; }
        public List<String> Contents { get; set; }
        public ulong OwnerId { get; set; }
        public ListPermission Permission { get; set; }

        public CustomList(ulong ownerId, ListPermission permission, String name)
        {
            this.OwnerId = ownerId;
            this.Permission = permission;
            this.Name = name;
            Contents = new List<string>();
        }

        public void Add(String item)
        {
            Contents.Add(item);
            SaveList();
        }

        public void AddRange(String[] collection)
        {
            Contents.AddRange(collection);
            SaveList();
        }

        public void Insert(int index, String item)
        {
            Contents.Insert(index, item);
            SaveList();
        }

        public void InsertRange(int index, String[] collection)
        {
            Contents.InsertRange(index, collection);
            SaveList();
        }

        public void Remove(String item)
        {
            Contents.Remove(item);
            SaveList();
        }

        public void Clear()
        {
            Contents.Clear();
            SaveList();
        }

        public int Count()
        {
            return Contents.Count;
        }

        public void Delete()
        {
            string resourceFolder = Constants.ResourceFolder;
            var path = String.Concat(resourceFolder, "/", this.Name, ".json");
            if (!File.Exists(path)) { return; }
            File.Delete(path);
        }

        public void SaveList()
        {
            DataStorage.StoreObject(this, $"{this.Name}.json", false);
        }
        
        public static CustomList RestoreList(string name)
        {
            return DataStorage.RestoreObject<CustomList>($"{name}.json");
        }

        public bool EqualContents(List<String> list)
        {
            if (this.Contents.Count != list.Count) { return false; }
            for (int i=0; i<this.Contents.Count; i++)
            {
                if (!this.Contents[i].Equals(list[i])) { return false; }
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is CustomList)) { return false; }
            CustomList comp = (CustomList)obj;
            return (EqualContents(comp.Contents) && comp.Name.Equals(this.Name));
        }

        public bool IsAllowedToList(ulong userId)
        {
            return ( !(this.OwnerId != userId && this.Permission == ListPermission.PRIVATE) );
        }

        public bool IsAllowedToRead(ulong userId)
        {
            return (    this.OwnerId == userId
                    ||  this.Permission == ListPermission.PUBLIC
                    ||  this.Permission == ListPermission.READ );
        }

        public bool IsAllowedToWrite(ulong userId)
        {
            return (    this.OwnerId == userId
                    ||  this.Permission == ListPermission.PUBLIC );
        }
    }
}
