using Articulate.Models;
using Articulate.Options;
using Articulate.Syndication;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Xml.XPath;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Web;
using Umbraco.Web.Models;
using Umbraco.Web.Mvc;

namespace Articulate.Controllers
{
    /// <summary>
    /// Rss controller
    /// </summary>
    /// <remarks>
    /// Cached for one minute
    /// </remarks>
#if (!DEBUG)
    [OutputCache(Duration = 300, VaryByHeader = "host")]
#endif

    public class ArticulateRssController : RenderMvcController
    {
        //NonAction so it is not routed since we want to use an overload below
        [NonAction]
        public override ActionResult Index(RenderModel model)
        {
            return base.Index(model);
        }

        protected IRssFeedGenerator FeedGenerator => UmbracoConfig.For.ArticulateOptions().GetRssFeedGenerator(UmbracoContext);

        public ActionResult Index(RenderModel model, int? maxItems)
        {
            if (!maxItems.HasValue) maxItems = 25;

            var listNodes = model.Content.Children
                .Where(x => x.DocumentTypeAlias.InvariantEquals("ArticulateArchive"))
                .ToArray();
            if (listNodes.Length == 0)
            {
                throw new InvalidOperationException("An ArticulateArchive document must exist under the root Articulate document");
            }

            var pager = new PagerModel(maxItems.Value, 0, 1);

            var listNodeIds = listNodes.Select(x => x.Id).ToArray();

            var listItems = Umbraco.GetPostsSortedByPublishedDate(pager, null, listNodeIds);

            var rootPageModel = new ListModel(listNodes[0], listItems, pager);
            
            var feed = FeedGenerator.GetFeed(rootPageModel, rootPageModel.Children<PostModel>());

            return new RssResult(feed, rootPageModel);
        }

        public ActionResult Author(RenderModel model, int authorId, int? maxItems)
        {
            var author = Umbraco.TypedContent(authorId);
            if (author == null) throw new ArgumentNullException(nameof(author));

            if (!maxItems.HasValue) maxItems = 25;

            //create a master model
            var masterModel = new MasterModel(author);

            var listNodes = masterModel.RootBlogNode.Children("ArticulateArchive").ToArray();

            var authorContenet = Umbraco.GetContentByAuthor(listNodes, author.Name, new PagerModel(maxItems.Value, 0, 1));

            var feed = FeedGenerator.GetFeed(masterModel, authorContenet.Select(x => new PostModel(x)));

            return new RssResult(feed, masterModel);
        }

        public ActionResult Categories(RenderModel model, string tag, int? maxItems)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (tag == null) throw new ArgumentNullException(nameof(tag));

            if (!maxItems.HasValue) maxItems = 25;

            return RenderTagsOrCategoriesRss(model, "ArticulateCategories", "categories", maxItems.Value);
        }

        public ActionResult Tags(RenderModel model, string tag, int? maxItems)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (tag == null) throw new ArgumentNullException(nameof(tag));

            if (!maxItems.HasValue) maxItems = 25;

            return RenderTagsOrCategoriesRss(model, "ArticulateTags", "tags", maxItems.Value);
        }

        public ActionResult RenderTagsOrCategoriesRss(RenderModel model, string tagGroup, string baseUrl, int maxItems)
        {
            var tagPage = model.Content as ArticulateVirtualPage;
            if (tagPage == null)
            {
                throw new InvalidOperationException("The RenderModel.Content instance must be of type " + typeof(ArticulateVirtualPage));
            }

            //create a blog model of the main page
            var rootPageModel = new MasterModel(model.Content.Parent);

            var contentByTag = Umbraco.GetContentByTag(
                rootPageModel,
                tagPage.Name,
                tagGroup,
                baseUrl);

            //super hack - but this is because we are replacing '.' with '-' in StringExtensions.EncodePath method
            // so if we get nothing, we'll retry with replacing back
            if (contentByTag == null)
            {
                contentByTag = Umbraco.GetContentByTag(
                    rootPageModel,
                    tagPage.Name.Replace('-', '.'),
                    tagGroup,
                    baseUrl);
            }

            if (contentByTag == null)
            {
                return HttpNotFound();
            }

            var feed = FeedGenerator.GetFeed(rootPageModel, contentByTag.Posts.Take(maxItems));

            return new RssResult(feed, rootPageModel);
        }

        /// <summary>
        /// Returns the XSLT to render the RSS nicely in a browser
        /// </summary>
        /// <returns></returns>
        public ActionResult FeedXslt()
        {
            var result = Resources.FeedXslt;
            return Content(result, "text/xml");
        }
    }
}