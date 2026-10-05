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
            if (!IsOwnShop || !JonasWares.TryGetWare(data, out var location))
            {
                // Cells survive shop/profile changes and cache only itemName. Restore only an icon we replaced.
                if (_icon != null && view.treasureIcon.sprite == _icon && data.type == MerchandiseType.Treasure)
                {
                    var treasure = DewResources.GetByShortTypeName<Treasure>(data.itemName);
                    if (treasure != null) view.treasureIcon.sprite = treasure.icon;
                }
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
            // TMP parses backslash escapes before rich-text tags and does not decode HTML entities.
            // Protect each '<' separately so even a name containing '</noparse>' stays literal.
            string description = JonasWares.Description(location).Replace("\\", "\\u005C")
                .Replace("<", "<noparse><</noparse>");
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
