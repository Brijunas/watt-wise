# Watt Wise production candidates

Candidates for the assets listed under "Missing assets" in [style.md](../style.md#missing-assets). They are not yet reviewed or approved, so the app uses the approved PNGs in [../brand/](../brand/) until they are; once approved they move to `brand/`.

## Assets

- [logo-mark.svg](logo-mark.svg): vector master, transparent, no glow, under 50 KB.
- [logo-lockup-dark.svg](logo-lockup-dark.svg) and [logo-lockup-light.svg](logo-lockup-light.svg): the mark plus outlined Red Hat Display. Use each only on its matching background; WATT loses contrast on the other.
- [favicon.svg](favicon.svg): same geometry and shading as logo-mark.svg.
- [logo-mark-mono.svg](logo-mark-mono.svg): one `currentColor` fill.
- [maskable-icon.png](maskable-icon.png): 1024×1024, the approved raster on #05060E inside the safe zone.
- [social-card.png](social-card.png): 1200×630, the approved lockup raster on #05060E with a soft halo.

## Known limitations

- The outline is traced from the 1254px source render, not original vector paths. It is close but not edge-for-edge identical.
- The boundaries where the shapes cross are straight segments.
- The shading is approximated with gradients, so it is harder and flatter than the source; the fine texture and bevel lighting are lost.
- The SVG lockups' WISE gradient runs at 100°, while the approved PNG lockups use a horizontal gradient.
