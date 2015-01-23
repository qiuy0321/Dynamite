﻿using System;
using System.Collections.Generic;
using Autofac;
using GSoft.Dynamite.Lists;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.Hosting.Administration;

namespace GSoft.Dynamite.IntegrationTests.Lists
{
    /// <summary>
    /// Validates the entire stack of behavior behind <see cref="ListHelper"/>.
    /// The GSoft.Dynamite.wsp package (GSoft.Dynamite.SP project) needs to be 
    /// deployed to the current server environment before running these tests.
    /// Redeploy the WSP package every time GSoft.Dynamite.dll changes.
    /// </summary>
    [TestClass]
    public class ListHelperTest
    {
        #region "Ensure" should mean "Create if new or return existing"

        /// <summary>
        /// Validates that EnsureList creates a new list at the correct URL (standard /Lists/ path),
        /// if it did not exist previously.
        /// </summary>
        [TestMethod]
        public void EnsureList_WhenNotAlreadyExists_ShouldCreateNewOneAtListsPath()
        {
            // Arrange
            const string Url = "Lists/testUrl";

            using (var testScope = SiteTestScope.BlankSite())
            {
                var listInfo = new ListInfo(Url, "nameKey", "descriptionKey");

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var testRootWeb = testScope.SiteCollection.RootWeb;
                    var numberOfListsBefore = testRootWeb.Lists.Count;

                    // Act
                    var list = listHelper.EnsureList(testRootWeb, listInfo);

                    // Assert
                    Assert.AreEqual(numberOfListsBefore + 1, testRootWeb.Lists.Count);
                    Assert.IsNotNull(list);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, list.TitleResource.Value);
                    Assert.AreEqual(listInfo.DescriptionResourceKey, list.DescriptionResource.Value);

                    // Fetch the list on the root web to make sure it was created and that it persists at the right location
                    var newlyCreatedList = testRootWeb.GetList(Url);

                    Assert.IsNotNull(newlyCreatedList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, newlyCreatedList.TitleResource.Value);
                }
            }
        }

        /// <summary>
        /// Validates that EnsureList creates a new list at the correct URL (NOT relative to /Lists/)
        /// if it did not exist previously.
        /// </summary>
        [TestMethod]
        public void EnsureList_WhenNotAlreadyExists_ShouldCreateANewOneNOTListsPath()
        {
            // Arrange
            using (var testScope = SiteTestScope.BlankSite())
            {
                var listInfo = new ListInfo("testUrl", "nameKey", "descriptionKey");

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var testRootWeb = testScope.SiteCollection.RootWeb;
                    var numberOfListsBefore = testRootWeb.Lists.Count;

                    // Act
                    var list = listHelper.EnsureList(testRootWeb, listInfo);

                    // Assert
                    Assert.AreEqual(numberOfListsBefore + 1, testRootWeb.Lists.Count);
                    Assert.IsNotNull(list);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, list.TitleResource.Value);
                    Assert.AreEqual(listInfo.DescriptionResourceKey, list.DescriptionResource.Value);

                    // Fetch the list on the root web to make sure it was created and that it persists at the right location
                    var newlyCreatedList = testRootWeb.GetList("testUrl");

                    Assert.IsNotNull(newlyCreatedList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, newlyCreatedList.TitleResource.Value);
                }
            }
        }

        /// <summary>
        /// Validates that EnsureList returns the existing list if one with that name already exists at that exact same URL.
        /// </summary>
        [TestMethod]
        public void EnsureList_WhenListWithSameNameAlreadyExistsAtThatURL_ShouldReturnExistingOne()
        {
            // Arrange
            using (var testScope = SiteTestScope.BlankSite())
            {
                var listInfo = new ListInfo("testUrl", "nameKey", "descriptionKey");

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var testRootWeb = testScope.SiteCollection.RootWeb;

                    // 1- Create the list
                    var numberOfListsBefore = testRootWeb.Lists.Count;
                    var list = listHelper.EnsureList(testRootWeb, listInfo);

                    Assert.AreEqual(numberOfListsBefore + 1, testRootWeb.Lists.Count);
                    Assert.IsNotNull(list);
                    
                    var newlyCreatedList = testRootWeb.GetList("testUrl");
                    Assert.IsNotNull(newlyCreatedList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, newlyCreatedList.TitleResource.Value);

                    // Act
                    // 2- Ensure the list a second time, now that it's been created
                    var expectingListCreatedAtStep1 = listHelper.EnsureList(testRootWeb, listInfo);

                    // Assert
                    Assert.AreEqual(numberOfListsBefore + 1, testRootWeb.Lists.Count);
                    Assert.IsNotNull(newlyCreatedList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, expectingListCreatedAtStep1.TitleResource.Value);
                    Assert.AreEqual(listInfo.DescriptionResourceKey, expectingListCreatedAtStep1.DescriptionResource.Value);

                    var listCreatedAtStep1 = testRootWeb.GetList("testUrl");
                    Assert.IsNotNull(listCreatedAtStep1);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, listCreatedAtStep1.TitleResource.Value);
                }
            }
        }

        /// <summary>
        /// Validates that EnsureList doesn't allow, on the same web, to create a new list if
        /// one with the same display name already exists, even if the relative URL is different.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EnsureList_WhenListWithSameNameExistsButDifferentURL_ShouldThrowException()
        {
            // Arrange
            using (var testScope = SiteTestScope.BlankSite())
            {
                const string SameNameKey = "nameKey";
                const string SameDescriptionKey = "descriptionKey";
                const string Url = "testUrl";
                const string SecondUrl = "Lists/" + Url;
                var listInfo = new ListInfo(Url, SameNameKey, SameDescriptionKey);
                var secondListInfo = new ListInfo(SecondUrl, SameNameKey, SameDescriptionKey);

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var testRootWeb = testScope.SiteCollection.RootWeb;

                    // 1- Create (by EnsureList) a first list at "testUrl"
                    var numberOfListsBefore = testRootWeb.Lists.Count;
                    var list = listHelper.EnsureList(testRootWeb, listInfo);

                    Assert.AreEqual(numberOfListsBefore + 1, testRootWeb.Lists.Count);
                    Assert.IsNotNull(list);

                    var newlyCreatedList = testRootWeb.GetList(Url);
                    Assert.IsNotNull(newlyCreatedList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, newlyCreatedList.TitleResource.Value);

                    // Act
                    // 2- Now, attempt to create a list with the same name at a different URL ("/Lists/secondUrl")
                    var secondListShouldThrowException = listHelper.EnsureList(testRootWeb, secondListInfo);

                    // Assert
                    Assert.AreEqual(numberOfListsBefore + 1, testRootWeb.Lists.Count);
                    Assert.IsNull(secondListShouldThrowException);

                    var secondCreatedList = testRootWeb.GetList(SecondUrl);
                    Assert.IsNull(secondCreatedList);

                    // Check to see if the first list is still there
                    var regettingFirstList = testRootWeb.GetList(Url);
                    Assert.IsNotNull(regettingFirstList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, regettingFirstList.TitleResource.Value);
                }
            }
        }

        /// <summary>
        /// Validates that EnsureList creates a new list at the URL specified of a sub web one
        /// level under the root web, when no list with that name already exist there.
        /// </summary>
        [TestMethod]
        public void EnsureList_ListDoesntExistAndWantToCreateOnASubWebOneLevelUnderRoot_ShouldCreateAtCorrectURL()
        {
            // Arrange
            const string Url = "some/random/path";

            using (var testScope = SiteTestScope.BlankSite())
            {
                // Creating the ListInfo and the sub-web
                var listInfo = new ListInfo(Url, "NameKey", "DescriptionKey");
                var subWeb = testScope.SiteCollection.RootWeb.Webs.Add("subweb");

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var numberOfListsOnSubWebBefore = subWeb.Lists.Count;

                    // Act
                    var list = listHelper.EnsureList(subWeb, listInfo);

                    // Assert
                    Assert.AreEqual(numberOfListsOnSubWebBefore + 1, subWeb.Lists.Count);
                    Assert.IsNotNull(list);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, list.TitleResource.Value);

                    var newlyCreatedList = subWeb.GetList(SPUtility.ConcatUrls(subWeb.ServerRelativeUrl, Url));
                    Assert.IsNotNull(newlyCreatedList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, newlyCreatedList.TitleResource.Value);
                }
            }
        }

        /// <summary>
        /// Validates that EnsureList creates a new list at the URL specified (and at the specified web)
        /// even if a list with the same name already exists on a different web.
        /// </summary>
        [TestMethod]
        public void EnsureList_AListWithSameNameExistsOnDifferentWeb_ShouldCreateListAtSpecifiedWebAndURL()
        {
            // Arrange
            const string Url = "testUrl";
            const string NameKey = "NameKey";
            const string DescKey = "DescriptionKey";

            using (var testScope = SiteTestScope.BlankSite())
            {
                // Let's first create a list on the root web
                var listInfo = new ListInfo(Url, NameKey, DescKey);

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var rootWeb = testScope.SiteCollection.RootWeb;
                    var numberOfListsOnRootWebBefore = rootWeb.Lists.Count;

                    var listRootWeb = listHelper.EnsureList(rootWeb, listInfo);
                    
                    Assert.AreEqual(numberOfListsOnRootWebBefore + 1, rootWeb.Lists.Count);
                    Assert.IsNotNull(listRootWeb);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, listRootWeb.TitleResource.Value);

                    // Now let's create a sub web under root, and try to ensure the "same" list there. It should create a new one.
                    var subWeb = rootWeb.Webs.Add("subweb");
                    var numberOfListsOnSubWebBefore = subWeb.Lists.Count;

                    // Act
                    var listSubWeb = listHelper.EnsureList(subWeb, listInfo);
                    
                    // Assert
                    Assert.AreEqual(numberOfListsOnSubWebBefore + 1, subWeb.Lists.Count);
                    Assert.IsNotNull(listSubWeb);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, listSubWeb.TitleResource.Value);

                    // Finally, try to get both lists to make sure everything is right
                    var firstList = rootWeb.GetList(Url);
                    Assert.IsNotNull(firstList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, firstList.TitleResource.Value);

                    var secondList = subWeb.GetList(SPUtility.ConcatUrls(subWeb.ServerRelativeUrl, Url));
                    Assert.IsNotNull(secondList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, secondList.TitleResource.Value);
                }
            }
        }

        /// <summary>
        /// When EnsureList is used with a web-relative URL (for example, "testurl"), and a sub-site already exists with the
        /// same relative URL, it should throw an exception because of a URL conflict.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EnsureList_TryingToEnsureAListWithRelativeURLCorrespondingToSubSiteURL_ShouldThrowException()
        {
            // Arrange
            const string Url = "testUrl";

            using (var testScope = SiteTestScope.BlankSite())
            {
                // First, create the subweb
                var rootWeb = testScope.SiteCollection.RootWeb;
                var subWeb = rootWeb.Webs.Add(Url);

                // Now, attempt to create the list which should result in a conflicting relative URL, thus, an exception thrown.
                var listInfo = new ListInfo(Url, "NameKey", "DescriptionKey");

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var numberOfListsOnRootWebBefore = rootWeb.Lists.Count;

                    // Act
                    var list = listHelper.EnsureList(rootWeb, listInfo);

                    // Asserting that the list wasn't created
                    Assert.AreEqual(numberOfListsOnRootWebBefore, rootWeb.Lists.Count);
                    Assert.IsNull(list);
                }
            }
        }

        /// <summary>
        /// When EnsureList is used with a web-relative URL (for example, "testurl"), and a folder already exists with the
        /// same relative URL, it should throw an exception because of a URL conflict.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EnsureList_TryingToEnsureAListWithRelativeURLCorrespondingToAFolder_ShouldThrowException()
        {
            // Arrange
            const string Url = "testUrl";

            using (var testScope = SiteTestScope.BlankSite())
            {
                // First, create the folder
                var rootWeb = testScope.SiteCollection.RootWeb;
                var folder = rootWeb.RootFolder.SubFolders.Add(Url);

                // Now, attempt to create a list which should result in a conflicting relative URL and a thrown exception.
                var listInfo = new ListInfo(Url, "NameKey", "DescriptionKey");

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var numberOfListsOnRootWebBefore = rootWeb.Lists.Count;

                    // Act
                    var listThatShouldNotBeCreated = listHelper.EnsureList(rootWeb, listInfo);

                    // Assert (list should not have been created, and an exception thrown)
                    Assert.AreEqual(numberOfListsOnRootWebBefore, rootWeb.Lists.Count);
                    Assert.IsNull(listThatShouldNotBeCreated);
                }
            }
        }

        #endregion

        #region Make sure everything works fine when using sites managed paths

        /// <summary>
        /// Making sure EnsureList works fine when site collection is on a managed path.
        /// </summary>
        [TestMethod]
        public void EnsureList_WhenListDoesntExistTryingToCreateOnSiteManagedPath_ShouldCreateList()
        {
            // Arrange
            const string ManagedPath = "managed";
            const string ListUrl = "some/random/path";

            using (var testScope = SiteTestScope.ManagedPathSite(ManagedPath))
            {
                var rootWeb = testScope.SiteCollection.RootWeb;
                var listInfo = new ListInfo(ListUrl, "NameKey", "DescriptionKey");

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var numberOfListsOnRootWebBefore = rootWeb.Lists.Count;

                    // Act
                    var list = listHelper.EnsureList(rootWeb, listInfo);

                    // Assert
                    Assert.AreEqual(numberOfListsOnRootWebBefore + 1, rootWeb.Lists.Count);
                    Assert.IsNotNull(list);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, list.TitleResource.Value);

                    // Fetching the list, to make sure it persists on the web
                    var newlyCreatedList = rootWeb.GetList(ManagedPath + "/" + ListUrl);
                    Assert.IsNotNull(newlyCreatedList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, newlyCreatedList.TitleResource.Value);
                }
            }
        }

        /// <summary>
        /// Case when trying to create a list with a path/URL that's already taken by a site managed path.
        /// It should throw an exception. For example, trying to create a list with URL "managed" under the root web
        /// of a site located at server-relative path "/", but the "managed" path is already taken by a site managed path.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void EnsureList_WhenPathReservedForSiteManagedPathTryingToBeUsedAsAListPath_ShouldThrowException()
        {
            // Arrange
            const string ManagedPath = "managed";
            var listInfo = new ListInfo(ManagedPath, "NameKey", "DescriptionKey");

            using (var managedPathSite = SiteTestScope.ManagedPathSite(ManagedPath))
            {
                using (var siteAtServerRoot = SiteTestScope.BlankSite())
                {
                    var rootSiteRootWeb = siteAtServerRoot.SiteCollection.RootWeb;
                    var numberOfListsOnRootSiteRootWeb = rootSiteRootWeb.Lists.Count;

                    using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                    {
                        var listHelper = injectionScope.Resolve<IListHelper>();

                        // Act
                        var list = listHelper.EnsureList(rootSiteRootWeb, listInfo);

                        // Assert (exception expected)
                        Assert.AreEqual(numberOfListsOnRootSiteRootWeb, rootSiteRootWeb.Lists.Count);
                        Assert.IsNull(list);
                    }
                }
            }
        }

        #endregion

        #region Make sure EnsureList updates the different properties of a list if it already exists (and make sure overwrite works fine)

        /// <summary>
        /// In the case the list already exists (based on the URL), and Overwrite property is at true,
        /// EnsureList should delete the existing list and create a new one.
        /// </summary>
        [TestMethod]
        public void EnsureList_WhenListExistsBasedOnURLAndOverwriteIsTrue_ItShouldRecreateTheList()
        {
            // Arrange (create the initial list so we can test the overwrite property)
            const string Url = "testUrl";
            const string NameKey = "nameKey";
            const string DescKey = "DescriptionKey";
            var listInfo = new ListInfo(Url, NameKey, DescKey);

            using (var testScope = SiteTestScope.BlankSite())
            {
                var rootWeb = testScope.SiteCollection.RootWeb;

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    var initialList = listHelper.EnsureList(rootWeb, listInfo);

                    // Making sure the initial list got created
                    Assert.IsNotNull(initialList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, initialList.TitleResource.Value);
                    initialList = rootWeb.GetList(Url);
                    Assert.IsNotNull(initialList);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, initialList.TitleResource.Value);

                    // Setting up the second list info
                    var listInfoForOverwrite = new ListInfo(Url, "SecondList", "DescSecondList");
                    listInfoForOverwrite.Overwrite = true;

                    // Act
                    var secondList = listHelper.EnsureList(rootWeb, listInfoForOverwrite);

                    // Assert
                    Assert.IsNotNull(secondList);
                    Assert.AreEqual(listInfoForOverwrite.DisplayNameResourceKey, secondList.TitleResource.Value);
                    secondList = rootWeb.GetList(Url);
                    Assert.IsNotNull(secondList);
                    Assert.AreEqual(listInfoForOverwrite.DisplayNameResourceKey, secondList.TitleResource.Value);
                    Assert.AreEqual(listInfoForOverwrite.DescriptionResourceKey, secondList.DescriptionResource.Value);
                }
            }
        }

        /// <summary>
        /// Using EnsureList to update the name of an existing list. The item already created on the list, and everything else
        /// besides the name should stay the same.
        /// </summary>
        [TestMethod]
        public void EnsureList_ExistingListWithItemCreatedThenEnsuringThatSameListToUpdateName_ShouldKeepSameListWithUpdatedName()
        {
            // Arrange
            const string Url = "testUrl";
            const string DescKey = "DescriptionKey";
            var listName = "InitialName";

            var listInfo = new ListInfo(Url, listName, DescKey);

            using (var testScope = SiteTestScope.BlankSite())
            {
                var rootWeb = testScope.SiteCollection.RootWeb;
                var numberOfListsBefore = rootWeb.Lists.Count;

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();
                    
                    // Creating the list and adding one item in it
                    var initialList = listHelper.EnsureList(rootWeb, listInfo);
                    Assert.IsNotNull(initialList);
                    var item = initialList.AddItem();
                    item["Title"] = "Item Title";
                    item.Update();

                    var listWithItem = rootWeb.GetList(Url);
                    Assert.IsNotNull(listWithItem);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, listWithItem.TitleResource.Value);
                    Assert.AreEqual(numberOfListsBefore + 1, rootWeb.Lists.Count);
                    Assert.AreEqual(listWithItem.ItemCount, 1);

                    // Act
                    var updatedListInfo = new ListInfo(Url, "Test_ListDisplayName", DescKey);
                    var expectedDisplayName = "EN List Name";
                    var updatedList = listHelper.EnsureList(rootWeb, updatedListInfo);

                    // Assert
                    Assert.IsNotNull(updatedList);
                    updatedList = rootWeb.GetList(Url);
                    Assert.AreEqual(expectedDisplayName, updatedList.TitleResource.Value);
                    Assert.AreEqual(numberOfListsBefore + 1, rootWeb.Lists.Count);
                    Assert.AreEqual(listWithItem.ItemCount, 1);
                    Assert.AreEqual(updatedList.Items[0]["Title"], "Item Title");
                }
            }
        }

        /// <summary>
        /// Using EnsureList to update the description of an existing list. The item already created on the list, and everything else
        /// besides the description should stay the same.
        /// </summary>
        [TestMethod]
        public void EnsureList_ExistingListWithItemCreatedThenEnsuringThatSameListToUpdateDescription_ShouldKeepSameListWithUpdatedDesc()
        {
            // Arrange
            const string Url = "testUrl";
            const string ListName = "NameKey";
            var initialDescription = "Initial Description";      

            var listInfo = new ListInfo(Url, ListName, initialDescription);

            using (var testScope = SiteTestScope.BlankSite())
            {
                var rootWeb = testScope.SiteCollection.RootWeb;
                var numberOfListsBefore = rootWeb.Lists.Count;

                using (var injectionScope = IntegrationTestServiceLocator.BeginLifetimeScope())
                {
                    var listHelper = injectionScope.Resolve<IListHelper>();

                    // Creating the list and adding one item in it
                    var initialList = listHelper.EnsureList(rootWeb, listInfo);
                    Assert.IsNotNull(initialList);
                    var item = initialList.AddItem();
                    item["Title"] = "Item Title";
                    item.Update();

                    var listWithItem = rootWeb.GetList(Url);
                    Assert.IsNotNull(listWithItem);
                    Assert.AreEqual(listInfo.DisplayNameResourceKey, listWithItem.TitleResource.Value);
                    Assert.AreEqual(numberOfListsBefore + 1, rootWeb.Lists.Count);
                    Assert.AreEqual(listWithItem.ItemCount, 1);

                    // Act
                    var updatedListInfo = new ListInfo(Url, ListName, "Test_ListDescription");
                    var expectedDescription = "EN List Description";
                    var updatedList = listHelper.EnsureList(rootWeb, updatedListInfo);

                    // Assert
                    Assert.IsNotNull(updatedList);
                    updatedList = rootWeb.GetList(Url);
                    Assert.AreEqual(expectedDescription, updatedList.DescriptionResource.Value);
                    Assert.AreEqual(numberOfListsBefore + 1, rootWeb.Lists.Count);
                    Assert.AreEqual(listWithItem.ItemCount, 1);
                    Assert.AreEqual(updatedList.Items[0]["Title"], "Item Title");
                }
            }
        }

        #endregion

        #region Make sure fields and/or content types are correctly created and saved on a list when ensuring that list.

        #endregion
    }
}