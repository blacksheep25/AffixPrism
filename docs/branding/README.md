# Brand assets

The approved prism/A symbol uses antique gold with cyan, violet and amber rays. The wordmark uses vector outlines rather than a font dependency. The app tile and simplified gold/white tray marks share the same silhouette.

SVG files are the editable source; transparent PNGs and the Windows multi-resolution ICO files are rendered from those sources. Run `npm install --no-save sharp` in a disposable tooling directory, then set `AFFIXPRISM_SHARP` to that module path and run `node scripts/render-branding.cjs`, or make `sharp` available through normal Node module resolution.

The render script contains the canonical geometry. No generated slogan or bloom is included in production artwork. `brand-preview.png` is the production overview.

## Application theme

Shared colours live in src/AffixPrism/Themes/AffixPrism.xaml. Charcoal-green backgrounds and panels use ivory text, antique-gold selection accents, cyan focus indicators and restrained violet labels. Item rarity and modifier colours retain their game meanings; comparison gains and losses remain green and red.

