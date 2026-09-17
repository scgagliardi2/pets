Location map backdrops.

Drop each region's pixel-art PNG in this folder under the exact file name below. src/meta/locations.ts
names them (Location.art), and LocationMap draws whichever ones exist; any file that is missing
falls back to a gradient built from that Location's `tint`, so a gap degrades rather than breaks.

  town.png       Verdant Path    town square, Pokemon Center, fountain, river
  coast.png      Tidewater Cove  blue water, islands, lighthouse, shipwreck
  desert.png     Sparkfield      sand, mesas, cacti, oasis
  forest.png     Bramblewood     green forest, cabin, mine mouth, stone circle
  cavern.png     Sunken Works    dark crystal cave, mine carts, lanterns
  ruins.png      Mirror Hollow   stone arches, hooded statues, overgrown flagstones
  volcano.png    Cinder Reach    lava flows, erupting cone, obsidian ledges
  tundra.png     Wyrm's Rest     snowed-in pines, ice, grey mountains

Any wide image works — the map covers itself with the art and darkens it behind the nodes, so a
2:1 isometric scene reads well and detail near the edges is safe to lose.
