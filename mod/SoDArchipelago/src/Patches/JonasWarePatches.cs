using System;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SoDArchipelago.Patches
{
    // Jonas's OnRefresh calls PopulatePlayerMerchandises too. Patch after the stock is filled, before its populated
    // event, so visits and successful refreshes both get exactly one extra ware with the correct vanilla price.
    [HarmonyPatch(typeof(PropEnt_Merchant_Jonas), "OnPopulateMerchandises")]
    internal static class JonasPopulatePatch
    {
        private static void Postfix(PropEnt_Merchant_Jonas __instance, DewPlayer player)
        {
            try { JonasWares.Append(__instance, player); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    // Patch Mirror's server implementation, not its client command wrapper.
    [HarmonyPatch(typeof(PropEnt_Merchant_Base), "UserCode_CmdPurchase__Int32__NetworkConnectionToClient")]
    internal static class WarePurchasePatch
    {
        private static bool Prefix(PropEnt_Merchant_Base __instance, int index, NetworkConnectionToClient sender)
        {
            if (!ProfileGuard.Marked || !NetworkServer.active) return true;
            try { return JonasWares.CanPurchase(__instance, sender.GetPlayer(), index); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }
    }

    [HarmonyPatch(typeof(PropEnt_Merchant_Base), "SpawnMerchandise")]
    internal static class WareSpawnPatch
    {
        private static bool Prefix(PropEnt_Merchant_Base __instance, MerchandiseData data, DewPlayer player)
        {
            try { return !JonasWares.Consume(__instance, player, data); }
            catch (Exception e)
            {
                Debug.LogException(e);
                // Never run a placeholder's real effect if recording its AP check failed.
                return !(JonasWares.IsLocalHost(player) && JonasWares.TryGetWare(data, out _));
            }
        }
    }

    [HarmonyPatch(typeof(UI_InGame_FloatingWindow_Shop_Item), nameof(UI_InGame_FloatingWindow_Shop_Item.UpdateContent))]
    internal static class WareShopContentPatch
    {
        private static void Postfix(UI_InGame_FloatingWindow_Shop_Item __instance, MerchandiseData d)
        {
            try { WareShopUi.UpdateContent(__instance, d); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    [HarmonyPatch(typeof(UI_InGame_FloatingWindow_Shop_Item), nameof(UI_InGame_FloatingWindow_Shop_Item.ShowTooltip))]
    internal static class WareShopTooltipPatch
    {
        private static bool Prefix(UI_InGame_FloatingWindow_Shop_Item __instance, UI_TooltipManager tooltip)
        {
            try { return !WareShopUi.ShowTooltip(__instance, tooltip); }
            catch (Exception e) { Debug.LogException(e); return true; }
        }
    }
}
