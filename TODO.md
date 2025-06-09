# TODO

- fix priority select
-  hover over community, then while still hovering quickly swipe over node in another community. node stays hovered
    - deprecated: (IGNORE as not high priority: fixing it would unfix hovering over node immediately after unhovering another node. plus, highlights reset themselves quite easily)
    - update: GOTTA FIX
- when splatting to surface, account for random nodes that are not part of same community
- move control point calculation to a transformer
    - keep bspline count constant
- have position stack so we can restore positions after returning from hairball
- move all selections at once?
- billboard labels...

- undo system
    - not priority, but better to implement a skeleton now than later

- move nodes back on surface deletion
- when using voice, attach cluster not based on cluster pos
- replicate cluster/node
- way to generate lambda functions from criteria? e.g. create linear scale colorer from attribute name and color

- refactor surface
- select groups using minimap

## Done

- delete surfaces
- move surfaces with things
- put things on surfaces
- database
- optimize database startup
- set nodes properties
    - like scaling to some constant
- update store on attribute changes
- make nodes flat on surfaces
- fix closest surface bug
- clean up stuff
- fix grab exit/hover exit bug
- don't deselect on surface move
- change link behaviour when splatting
- improve bring node
- find out how to include props as anonymous types
- tooltip

## Ignore
- move node moving animation to mln

## Troubleshooting

1) Restart.