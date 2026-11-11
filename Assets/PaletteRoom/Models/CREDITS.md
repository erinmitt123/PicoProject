# Third-party assets

All models and textures below are from [Poly Haven](https://polyhaven.com) and released under
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) (public domain). No attribution is
required; it is recorded here so the source of each file is clear.

| Asset | Used for | Source |
| --- | --- | --- |
| modern_arm_chair_01 | Armchair | https://polyhaven.com/a/modern_arm_chair_01 |
| side_table_01 | Side table | https://polyhaven.com/a/side_table_01 |
| potted_plant_02 | Plant | https://polyhaven.com/a/potted_plant_02 |
| hanging_picture_frame_01 | Wall art | https://polyhaven.com/a/hanging_picture_frame_01 |
| throw_pillows_01 | Throw pillow | https://polyhaven.com/a/throw_pillows_01 |
| Sofa_01 | Settee | https://polyhaven.com/a/Sofa_01 |
| mid_century_lounge_chair | Swivel lounge chair | https://polyhaven.com/a/mid_century_lounge_chair |
| Ottoman_01 | Leather ottoman | https://polyhaven.com/a/Ottoman_01 |
| modern_coffee_table_01 | Inlay coffee table | https://polyhaven.com/a/modern_coffee_table_01 |
| coffee_table_round_01 | Round coffee table | https://polyhaven.com/a/coffee_table_round_01 |
| ClassicNightstand_01 | Carved nightstand | https://polyhaven.com/a/ClassicNightstand_01 |
| ClassicConsole_01 | Carved console | https://polyhaven.com/a/ClassicConsole_01 |
| vintage_wooden_drawer_01 | Apothecary chest | https://polyhaven.com/a/vintage_wooden_drawer_01 |
| modern_wooden_cabinet | Slatted media unit | https://polyhaven.com/a/modern_wooden_cabinet |
| wooden_display_shelves_01 | Cube shelves | https://polyhaven.com/a/wooden_display_shelves_01 |
| dining_chair_02 | Tufted dining chair | https://polyhaven.com/a/dining_chair_02 |
| round_wooden_table_01 | Pedestal table | https://polyhaven.com/a/round_wooden_table_01 |
| ornate_mirror_01 | Arched mirror | https://polyhaven.com/a/ornate_mirror_01 |
| ceramic_vase_01 | Ceramic vase | https://polyhaven.com/a/ceramic_vase_01 |
| potted_plant_04 | Succulent | https://polyhaven.com/a/potted_plant_04 |
| rough_linen (texture) | Sofa upholstery | https://polyhaven.com/a/rough_linen |
| wool_boucle (texture) | Rug | https://polyhaven.com/a/wool_boucle |
| white_oak_veneer (texture) | Wood parts | https://polyhaven.com/a/white_oak_veneer |

Downloaded at 1K resolution via the Poly Haven API (the first five and the textures on 2026-10-19,
the fifteen from Sofa_01 down on 2026-11-09); each file's MD5 was checked against the API listing.
Only the FBX, diffuse and OpenGL normal maps are kept.

## Fonts

| Asset | Used for | License |
| --- | --- | --- |
| Noto Sans SC Regular (`Resources/Fonts/NotoSansSC-Regular.otf`) | Chinese text | SIL Open Font License 1.1, see `Resources/Fonts/NotoSansSC-OFL.txt` |
| Material Icons Round (`Resources/Fonts/MaterialIconsRound-Regular.otf`), from [google/material-design-icons](https://github.com/google/material-design-icons) | Button and rail icons | Apache License 2.0, see `Resources/Fonts/MaterialIcons-LICENSE.txt` |

## Voice

The tutorial narration (`Resources/Tutorial/Voice/*.ogg`) was generated offline with
[Piper](https://github.com/rhasspy/piper) using the `en_US-ljspeech-medium` voice, trained on the
public-domain LJ Speech dataset. Only the generated audio ships with the app; Piper itself does not.
Regenerate with `tools/generate_narration.py`.
