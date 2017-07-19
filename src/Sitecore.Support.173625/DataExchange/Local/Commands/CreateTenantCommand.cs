using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Globalization;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Web.UI.Sheer;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.DataExchange.Providers.DynamicsCrm.Local;

namespace Sitecore.Support.DataExchange.Local.Commands
{
    [Serializable]
    public class CreateTenantCommand : Command
    {
        public CreateTenantCommand()
        {
        }
        public override void Execute(CommandContext context)
        {
            if (context.Items.Length == 1)
            {
                var item = context.Items[0];
                var parameters = new NameValueCollection();
                parameters["id"] = item.ID.ToString();
                parameters["language"] = item.Language.ToString();
                parameters["database"] = item.Database.Name;
                Sitecore.Context.ClientPage.Start(this, "Run", parameters);
            }
        }
        protected void Run(ClientPipelineArgs args)
        {
            var branchId = new BranchId(new ID("{7E544830-D4CD-424C-BDBF-4D3A2D9D4470}"));
            var db = Sitecore.Configuration.Factory.GetDatabase(args.Parameters["database"]);
            var branchItem = Sitecore.Context.ContentDatabase.GetItem(branchId.ID);
            if (!args.IsPostBack)
            {
                Sitecore.Context.ClientPage.ClientResponse.Input(Translate.Text(Sitecore.DataExchange.Local.Texts.ItemNamePromptForNewTenant), branchItem.DisplayName, Sitecore.Configuration.Settings.ItemNameValidation, "'$Input' is not a valid name.", 100);
                args.WaitForPostBack();
            }
            else
            {
                if (!String.IsNullOrEmpty(args.Result) && args.Result != "undefined")
                {
                    var parent = db.GetItem(args.Parameters["id"], Sitecore.Globalization.Language.Parse(args.Parameters["language"]));
                    var tenantItem = parent.Add(args.Result, branchId);
                    FixReferences(tenantItem, branchItem);
                    Sitecore.Context.ClientPage.SendMessage(this, string.Format("item:load(id={0})", tenantItem.ID));
                }
            }
        }
        protected virtual void FixReferences(Item tenantItem, Item branchItem)
        {
            if (tenantItem == null)
            {
                Sitecore.DataExchange.Context.Logger.Error("The {0} parameter is null so references cannot be fixed.", nameof(tenantItem));
                return;
            }
            if (branchItem == null)
            {
                Sitecore.DataExchange.Context.Logger.Error("The {0} parameter is null so references cannot be fixed.", nameof(branchItem));
                return;
            }
            var ancestor = branchItem.GetChildren().FirstOrDefault();
            if (ancestor == null)
            {
                Sitecore.DataExchange.Context.Logger.Error("The branch item has no children so references cannot be fixed.", nameof(branchItem));
                return;
            }
            foreach (Item child in tenantItem.Children)
            {
                Fix(child, tenantItem, ancestor);
            }
        }
        private void Fix(Item itemToFix, Item newItem, Item ancestor)
        {
            if (itemToFix == null)
            {
                return;
            }
            var newItemPath = newItem.Paths.ContentPath;
            var ancestorPath = ancestor.Paths.ContentPath;
            foreach (Field field in itemToFix.Fields)
            {
                MultilistField field2 = field;
                if (field2 != null && field2.TargetIDs.Length > 0)
                {
                    var database = itemToFix.Database;
                    var newValues = new List<ID>();
                    foreach (var currentValue in field2.TargetIDs)
                    {
                        var referencedItem = database.GetItem(currentValue);
                        if (referencedItem != null)
                        {
                            if (referencedItem.Paths.IsDescendantOf(ancestor))
                            {
                                var currentReferencedItemPath = referencedItem.Paths.ContentPath;
                                var newReferencedItemPath = currentReferencedItemPath.Replace(ancestorPath, newItemPath);
                                var newReferencedItem = database.GetItem(newReferencedItemPath);
                                if (newReferencedItem != null)
                                {
                                    newValues.Add(newReferencedItem.ID);
                                    continue;
                                }
                            }
                            Sitecore.DataExchange.Context.Logger.Info("Value in the field was not fixed. (item: {0}, field: {1}, value: {2}, ancestor: {3})", itemToFix.ID, field.Name, currentValue, ancestor.ID);
                        }
                        newValues.Add(currentValue);
                    }
                    itemToFix.Editing.BeginEdit();
                    itemToFix[field.ID] = string.Join("|", newValues.Select(x => x.ToString()).ToArray());
                    itemToFix.Editing.EndEdit();
                }
            }
            if (!itemToFix.HasChildren)
            {
                return;
            }
            foreach (Item child in itemToFix.Children)
            {
                Fix(child, newItem, ancestor);
            }
        }
    }
}
