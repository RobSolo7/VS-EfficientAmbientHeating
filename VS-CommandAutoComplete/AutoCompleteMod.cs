using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace CommandAutoComplete
{
    public class AutoCompleteSystem : ModSystem
    {
        private ICoreClientAPI capi = null!;
        private List<string> availableCommands = new List<string>();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            // This mod only makes sense on the client side (HUD/Input)
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            this.capi = api;

            // Hook into KeyDown to intercept the Tab key
            api.Event.KeyDown += OnKeyDown;

            // Initialize or refresh commands when the player actually joins a world
            api.Event.PlayerJoin += (byPlayer) =>
            {
                RefreshCommandList();
            };
        }

        private void RefreshCommandList()
        {
            availableCommands.Clear();

            // standard client-side commands starting with '.'
            availableCommands.AddRange(new string[] {
                ".cam", ".client", ".clearchat", ".debug", ".fountain",
                ".freemove", ".help", ".lockfly", ".noclip", ".recomposechat"
            });
        }

        private void OnKeyDown(KeyEvent e)
        {
            // SAFETY CHECK: 
            // If the world is loading, capi.World or the Player might be null.
            // Accessing OpenedGuis during world gen is the most likely cause of your crash.
            if (capi?.World?.Player == null || capi.Gui?.OpenedGuis == null)
            {
                return;
            }

            // Check if the key pressed is Tab (GlKeys.Tab)
            if (e.KeyCode == (int)GlKeys.Tab)
            {
                // Find the chat or console dialog if it is currently open
                var chatDialog = capi.Gui.OpenedGuis.FirstOrDefault(g =>
                    g != null &&
                    (g.GetType().Name.Contains("Chat") || g.GetType().Name.Contains("Console"))
                );

                if (chatDialog != null)
                {
                    // Logic for autocomplete would go here.
                    // For now, we just log to the chat to prove it works.
                    capi.ShowChatMessage("Tab detected in Chat/Console!");

                    // IMPORTANT: Setting Handled to true stops the game from 
                    // running its default Tab behavior (like switching channels).
                    e.Handled = true;
                }
            }
        }

        public override void Dispose()
        {
            // Unsubscribe from events when the mod is disabled to prevent memory leaks
            if (capi != null)
            {
                capi.Event.KeyDown -= OnKeyDown;
            }
            base.Dispose();
        }
    }
}