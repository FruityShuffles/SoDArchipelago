using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace SoDArchipelago
{
    internal static class WareShopUi
    {
        private static Texture2D _texture;
        private static Sprite _icon;

        private static Sprite Icon
        {
            get
            {
                if (_icon != null) return _icon;
                using var stream = typeof(WareShopUi).Assembly.GetManifestResourceStream("SoDArchipelago.ware_icon.png");
                using var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                _texture = new Texture2D(2, 2);
                _texture.LoadImage(bytes.ToArray());
                _icon = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), new Vector2(0.5f, 0.5f));
                return _icon;
            }
        }

        private static bool IsOwnShop => JonasWares.IsLocalHost(DewPlayer.local) &&
            ManagerBase<FloatingWindowManager>.softInstance?.currentTarget is PropEnt_Merchant_Jonas;

        public static void UpdateContent(UI_InGame_FloatingWindow_Shop_Item view, MerchandiseData data)
        {
            if (!IsOwnShop) return;
            if (!JonasWares.TryGetWare(data, out var location))
            {
                // Cells cache only itemName; a normal Cloak in a reused AP cell needs its vanilla icon restored.
                if (data.type == MerchandiseType.Treasure && data.itemName == JonasWares.Placeholder)
                    view.treasureIcon.sprite = DewResources.GetByShortTypeName<Treasure>(data.itemName).icon;
                return;
            }
            view.treasureIcon.sprite = Icon;
            // Jonas applies the buyer's price multiplier even to Treasures; the vanilla cell omits it.
            view.costDisplay.Setup(data.price.MultiplyGold(DewPlayer.local.buyPriceMultiplier));
            bool available = data.count > 0 && JonasWares.IsAvailable(location);
            view.GetComponent<Button>().interactable = available;
            view.quantityText.text = available ? "1" : "0";
        }

        public static bool ShowTooltip(UI_InGame_FloatingWindow_Shop_Item view, UI_TooltipManager tooltip)
        {
            if (!IsOwnShop || !JonasWares.TryGetWare(view.data, out var location)) return false;
            // Like other merchandise, the icon's tooltip supplies its name. Escape multiworld names' TMP markup.
            string description = JonasWares.Description(location).Replace("<", "&lt;").Replace(">", "&gt;");
            string price = view.data.price.MultiplyGold(DewPlayer.local.buyPriceMultiplier).gold.ToString("#,##0");
            tooltip.ShowRawTextTooltip(view.transform.position,
                description + "\n" + location.Name + "\n" + price + " gold" +
                (JonasWares.IsAvailable(location) && view.data.count > 0 ? "" : "\nPurchased"));
            return true;
        }

        public static void Cleanup()
        {
            if (_icon != null) Object.Destroy(_icon);
            if (_texture != null) Object.Destroy(_texture);
            _icon = null;
            _texture = null;
        }
    }
}
