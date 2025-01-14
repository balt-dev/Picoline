# Picoline

Adds a way to replace Madeline with her PICO-8 rendition!

![output](https://github.com/user-attachments/assets/1593b215-f40b-401b-a1ca-3b1c7c892ddf)

Heavily copies code from https://github.com/NoelFB/Celeste.
The license for this code is found in [LICENSE-CELESTE](./LICENSE-CELESTE).

## Note about compatibility

This mod replaces the Player object with a class extending it whenever a level is loaded.

Along with this, when PICO-8 mode is active, the player **does not call `base.Update()` or `base.Render()`.**

All mechanics _in the vanilla game that are not left unused_ have been tested and work.

Compatibility with other mods that alter the player (e.g. custom `Booster`s, or custom dash states like Communal Helper's `Dream Dash Refill`) should be expected to be slim to none in PICO-8 mode.

Despite this, **this mod explicitly supports a select few Extended Variants**, namely `Dash`/`Jump Count`, `Gravity`, `Max Fall Speed`, and `Madeline is Silhouette`.
