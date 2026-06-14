# SaberSense

A custom saber mod for Beat Saber built from the ground up. Loads `.saber` and `.whacker` files.

If you're coming from [CustomSabersLite](https://github.com/qqrz997/CustomSabersLite), CSL is a great mod that stays close to the vanilla experience with a clean, minimal feature set. SaberSense is for people who want more control over how their sabers look and behave.

<video src="https://github.com/user-attachments/assets/39887958-424b-4024-8746-892fe09732a7" autoplay loop muted playsinline></video>

## Saber Customization

**Transforms** - Per-saber width, length, rotation, and offset. Each saber remembers its own settings, so switching sabers restores what you had set.

**Material editing** - Opens the saber's shader properties in-game. The editor reads what the shader exposes, so you see the property names and ranges for that saber. Color pickers, float sliders, texture selection, and toggles depending on what the shader has. Properties can be split per-hand if you want different colors or values on left vs right. Changes preview live and can be reverted.

<video src="https://github.com/user-attachments/assets/00bd4fb5-aa07-437f-9659-d6fa8accfb98" autoplay loop muted playsinline></video>

**Component modifiers** - Sabers that define modifier targets let you toggle parts on or off (visibility modifiers) or adjust their position, rotation, and scale (spatial modifiers). Changes sync across both hands.

**Use trails from other sabers** - Browse your saber library with folder navigation and search, pick any saber, and apply its trail to your current one. Hit "Original" to go back to the saber's own trail.

## Trail Customization

The trail system is built from scratch with spline interpolation and framerate-independent timing. It doesn't use Beat Saber's built-in trail renderer.

**Duration** - How many frames of trail history are visible (0-100%). At 0% you get basically no trail, at 100% it holds the full 40-frame history.

**Width** - How thick the trail is as a percentage of the distance from the trail endpoint to the saber's base. Capped so it can't extend past the bottom of the blade.

**Whitestep** - White-to-color fade at the start of the trail. At 0 there's no white, at higher values the trail fades from white into the saber's color.

**Offset** - Shifts the trail up or down along the blade (-100% to +100% of the saber's length).

**Curve smoothness** - How many spline subdivisions the trail uses (2 at minimum, up to 100). Lower values look more segmented, higher values are smoother with more geometry.

**Refresh rate** - How many saber positions are sampled per second (up to 144 Hz). Set to "Auto" to match your headset's refresh rate. Higher values give smoother motion capture, lower values are cheaper.

**Flip** - Reverses the trail direction.

**Clamp texture** - Switches texture wrapping to clamp mode to prevent tiling artifacts at the trail edges.

**Local space** - Captures trail positions in player-local space instead of world space. Prevents trails from stretching out when Noodle Extensions or map modifiers move the player around mid-swing.

**Vertex color only** - Ignores the trail texture and uses only vertex color. Faster but less detailed.

**Sort order override** (experimental) - Forces the trail to render above walls. Fixes trails clipping behind obstacles.

**Edit material** - Opens the material editor for the trail's material, same system as saber materials.

## Motion Blur

Generates a sweep mesh when you swing fast enough. Traces the saber's arc using angular velocity history, samples colors from the saber surface so the blur matches, and fades out at the edges. Kills itself when you reverse direction so it doesn't fold through the saber. Strength slider controls opacity (0-100%).

## Motion Smoothing

Smooths out your swings. If you have wobbly tracking, figure-eight swings, or general swing instability, this cleans it up. Desktop-only by default since it's mostly for streaming, screensharing, or recording - you can turn it on for HMD view manually if you want but it's not recommended. Strength slider (0-100%).

## Desktop / HMD Visibility

Set which features are visible in the headset vs the desktop mirror independently. By default, motion smoothing is desktop-only and warning markers are HMD-only. You can change what shows up in each view. The mod spawns separate saber instances per view when needed.

## Presets

Save your setup (saber selection, transforms, trail settings, material edits, modifier states, motion settings) as a named `.sabersense` config. Multiple configs, switch between them from the configuration panel. Export to clipboard for sharing over Discord, import from clipboard to load someone else's setup. Per-saber customization is stored in the preset, so loading a config restores settings for every saber you've edited.

## Saber Browser

Pin favorites, navigate folders, search by name, sort by name/date/size/author. 3D saber preview with drag-to-rotate, auto-rotation, auto-framing, bloom toggle, and live trail preview as you adjust settings.

## Installation

> [!IMPORTANT]
> You need [BSIPA](https://github.com/bsmg/BeatSaber-IPA-Reloaded) (4.3.0+), [SiraUtil](https://github.com/Auros/SiraUtil) (3.1.7+), [BeatSaberMarkupLanguage](https://github.com/monkeymanboy/BeatSaberMarkupLanguage) (1.11.0+), [AssetBundleLoadingTools](https://github.com/nicoco007/AssetBundleLoadingTools) (1.1.10+), and [CameraUtils](https://github.com/Reezonate/CameraUtils) (1.0.0+) installed.

Place `SaberSense.dll` in your `Plugins` folder. Saber files go in `CustomSabers/`.

Requires Beat Saber 1.40.0+.

## Configuration

Settings are in-game from the SaberSense panel. Tabs for saber selection, trail editing, material/modifier editing, and general settings.

> [!NOTE]
> Configs do not auto-save. You need to go into the configuration panel and save manually.

## License

This project is licensed under the **SaberSense Proprietary License**. See [LICENSE](LICENSE) for the full terms.

Source code is viewable for transparency and community review but may not be copied, modified, or reused outside of contributing back to this project.

## Links

- [Discord](https://discord.gg/Jq4nKuZMef)
- [ModelSaber](https://modelsaber.com) for saber downloads
