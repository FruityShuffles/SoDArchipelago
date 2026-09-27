namespace SoDArchipelago
{
    // Connection settings, editable in the mod manager's config UI (ModBehaviour.modConfigFields picks up ModConfig
    // fields) or with ap_server / ap_slot / ap_password. Kept separate from the profile binding: an archipelago.gg
    // room's port can change without affecting which profile belongs to which seed/slot.
    public class ApConfig : ModConfig
    {
        [LabelText("Server")]
        [Description("Archipelago server address and port, e.g. archipelago.gg:38281")]
        public string server = "archipelago.gg:38281";

        [LabelText("Slot")]
        [Description("Your slot (player) name")]
        public string slot = "";

        [LabelText("Password")]
        [Description("Room password, if any")]
        public string password = "";
    }
}
