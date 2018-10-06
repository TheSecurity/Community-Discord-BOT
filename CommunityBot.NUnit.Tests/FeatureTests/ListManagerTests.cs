using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Discord;
using CommunityBot.Features.Lists;
using Discord.Commands;
using Moq;
using static CommunityBot.Features.Lists.ListException;

namespace CommunityBot.NUnit.Tests.FeatureTests
{
    public static class ListManagerTests
    {
        private static readonly string TestListName = "testname";
        private static readonly string TestListItem = "item";
        private static readonly ulong userId = 10;

        [Test]
        public static void UnknownCommandTest()
        {
            Assert.Throws<ListManagerException>(() => ListManager.Manage(userId, new[] { "-aabadf", "123" }));
        }

        [Test]
        public static void CreatePrivateListTest()
        {
            CustomList expected = new CustomList(userId, CustomList.ListPermission.PRIVATE, TestListName);

            ListManager.Manage(userId, new[] { "-c", TestListName });
            CustomList actual = ListManager.GetList(userId, TestListName);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void PrivateListPermissionTest()
        {
            var expected = String.Format(ListErrorMessage.Permission.NoPermission_list, TestListName);

            ListManager.Manage(userId, new[] { "-c", TestListName });

            var e = Assert.Throws<ListManagerException>(
                () => ListManager.Manage(userId + 1, new[] { "-a", TestListItem, TestListName })
            );

            Assert.AreEqual(expected, e.Message);
        }

        [Test]
        public static void CreatePublicListTest()
        {
            CustomList expected = new CustomList(userId, CustomList.ListPermission.PUBLIC, TestListName);

            ListManager.Manage(userId, new[] { "-cp", TestListName });
            CustomList actual = ListManager.GetList(userId, TestListName);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void PublicListPermissionTest()
        {
            var expected = new CustomList(userId, CustomList.ListPermission.PUBLIC, TestListName);
            expected.Add(TestListItem);

            ListManager.Manage(userId, new[] { "-cp", TestListName });

            Assert.DoesNotThrow(
                () => ListManager.Manage(userId + 1, new[] { "-a", TestListItem, TestListName })
            );

            var actual = ListManager.GetList(userId, TestListName);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void RemoveListTest()
        {
            string expected = String.Format(ListErrorMessage.General.ListDoesNotExist_list, TestListName);

            ListManager.Manage(userId, new[] { "-c", TestListName });
            ListManager.Manage(userId, new[] { "-rl", TestListName });

            var e = Assert.Throws<ListManagerException>(
                () => ListManager.GetList(userId, TestListName)
            );
            Assert.AreEqual(expected, e.Message);
        }

        [Test]
        public static void RemoveNonExistentListTest()
        {
            var excpected = String.Format(ListErrorMessage.General.ListDoesNotExist_list, TestListName);
            var e = Assert.Throws<ListManagerException>(
                () => ListManager.Manage(userId, new[] { "-rl", TestListName })
            );
            Assert.AreEqual(excpected, e.Message);
        }

        [Test]
        public static void NoListsTest()
        {
            string expected = ListErrorMessage.General.NoLists;

            var e = Assert.Throws<ListManagerException>(
                () => ListManager.Manage(userId, new[] { "-g" })
            );

            Assert.AreEqual(expected, e.Message);
        }

        [Test]
        public static void AddItemTest()
        {
            CustomList expected = new CustomList(userId, CustomList.ListPermission.PRIVATE, TestListName);
            expected.Add(TestListItem);

            ListManager.Manage(userId, new[] { "-c", TestListName });
            ListManager.Manage(userId, new[] { "-a", TestListItem, TestListName });
            CustomList actual = ListManager.GetList(userId, TestListName);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void InsertItemTest()
        {
            CustomList expected = new CustomList(userId, CustomList.ListPermission.PRIVATE, TestListName);
            expected.Add($"{TestListItem} 1");
            expected.Add($"{TestListItem} 2");
            expected.Add($"{TestListItem} 3");

            ListManager.Manage(userId, new[] { "-c", TestListName });
            ListManager.Manage(userId, new[] { "-i", "1", $"{TestListItem} 3", TestListName });
            ListManager.Manage(userId, new[] { "-i", "1", $"{TestListItem} 2", TestListName });
            ListManager.Manage(userId, new[] { "-i", "1", $"{TestListItem} 1", TestListName });
            CustomList actual = ListManager.GetList(userId, TestListName);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void InsertWithIndexOutOfBoundsTest()
        {
            var expected = String.Format(ListErrorMessage.General.IndexOutOfBounds_list, TestListName);

            ListManager.Manage(userId, new[] { "-c", TestListName });
            var e = Assert.Throws<ListManagerException>(
                () => ListManager.Manage(userId, new[] { "-i", "0", $"{TestListItem} 3", TestListName })
            );

            Assert.AreEqual(expected, e.Message);
        }

        [Test]
        public static void InsertWithWrongIndexTypeTest()
        {
            var expected = ListErrorMessage.General.WrongInputForIndex;

            ListManager.Manage(userId, new[] { "-c", TestListName });
            var e = Assert.Throws<ListManagerException>(
                () => ListManager.Manage(userId, new[] { "-i", "wrong_index", $"{TestListItem} 3", TestListName })
            );

            Assert.AreEqual(expected, e.Message);
        }

        [Test]
        public static void RemoveItemTest()
        {
            CustomList expected = new CustomList(userId, CustomList.ListPermission.PRIVATE, TestListName);
            expected.Add(TestListItem + " 0");
            expected.Add(TestListItem + " 2");

            ListManager.Manage(userId, new[] { "-c", TestListName });
            ListManager.Manage(userId, new[] { "-a", $"{TestListItem} 0", TestListName });
            ListManager.Manage(userId, new[] { "-a", $"{TestListItem} 1", TestListName });
            ListManager.Manage(userId, new[] { "-a", $"{TestListItem} 2", TestListName });
            ListManager.Manage(userId, new[] { "-r", $"{TestListItem} 1", TestListName });
            CustomList actual = ListManager.GetList(userId, TestListName);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void RemoveItemMultipleTimesTest()
        {
            for (int i = 0; i < 10; i++)
            {
                RemoveItemTest();
                TearDown();
            }
        }

        [Test]
        public static void ClearListTest()
        {
            CustomList expected = new CustomList(userId, CustomList.ListPermission.PRIVATE, TestListName);

            ListManager.Manage(userId, new[] { "-c", TestListName });
            ListManager.Manage(userId, new[] { "-a", $"{TestListItem} 0", TestListName });
            ListManager.Manage(userId, new[] { "-a", $"{TestListItem} 1", TestListName });
            ListManager.Manage(userId, new[] { "-a", $"{TestListItem} 2", TestListName });
            ListManager.Manage(userId, new[] { "-cl", TestListName });
            CustomList actual = ListManager.GetList(userId, TestListName);

            Assert.IsNotNull(actual);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void GetAllPublicPermissionTest()
        {
            var expected = CustomList.ListPermission.PUBLIC;

            ListManager.CreateListPublic(userId, new[] { TestListName });

            var actual = ListManager.Manage(userId, new[] { "-gp" }).permission;

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public static void GetAllPrivatePermissionTest()
        {
            var expected = CustomList.ListPermission.PRIVATE;

            ListManager.CreateListPrivate(userId, new[] { TestListName });

            var actual = ListManager.Manage(userId, new[] { "-g" }).permission;

            Assert.AreEqual(expected, actual);
        }

        [TearDown]
        public static void TearDown()
        {
            try
            {
                ListManager.RemoveList(userId, TestListName);
            }
            catch (ListManagerException) { }
        }
    }
}
