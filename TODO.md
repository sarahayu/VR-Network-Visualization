# TODO

- make surface based off nodes
- show link directionality
- trash can/optinons

- make shells have facets, easier distance judgement
- try to change shell to fit subgraph more

- attaching nodes, average pos?
- fix bug: overviewlayouttransformer update ALL subnetwork nodes
    - change pushselectionevent to allow multiple subnetwork updates simult.
- fix bug: overviewrenderer not showing hovers on all dupe subnetworks
- fix bug: duplicating network deselects all network

- too big buffer error at BSpineShaderWrapper:322
- clean up transformer code and input code (less nesting!!!)

- remove communities from global
- change all new inits to just new()
- remove empty interpolators

- replicate cluster/node
    - community is global and contexted. main mlcontext is synced to global
    - store all mlnetwork nodes
    - when asking to select nodes, does it select on subnetworks as well?
- optimize reflection: https://stackoverflow.com/questions/7999652/best-way-to-cache-a-reflection-property-getter-setter


- general code cleanup  
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
- lean button opts
- straighten lines between focused communities
- high contrast
- bigger font
- bigger surface
- bring back attach nodes
- mic latency
- encode using voice, start figuring out:
    - way to generate lambda functions from criteria? e.g. create linear scale colorer from attribute name and color
-  hover over community, then while still hovering quickly swipe over node in another community. node stays hovered
    - deprecated: (IGNORE as not high priority: fixing it would unfix hovering over node immediately after unhovering another node. plus, highlights reset themselves quite easily)
    - update: GOTTA FIX
- latency examine
- change edit commands to use string color
- move multiple selections
- wrap community shape and subgraph shapes
- when asking to select nodes, does it select on subnetworks as well? - no
- how do i communicate subnetwork id across renderers and inputs? make it a part of subnetwork context, for a start. now for input...
- reorganize code
- reimplement options (so we can clean up and test out transformers)
- shrink nodes
- add surfaces
- minimap is mini of world. have little avatar moving around
    - TODO: check if node is virtual before applying context transformations
    - then, double check that CRMoveNodes is no longer laggy as hell on surface move
    - spawn surface minis
- figure out options interface
- fix surface icon placement on minimap
- selection history with hue
- small on non select
- zoom
- outline on hover
- try group hulls and only show selected nodes
- try make non selected nodes gray, transparent, or outline?
- merge
- fix database
- regenerate layout data and double check flat coords
- update storage for subgraph data
- parse all waves into one
- reimplement tooltip
- relayout graph, spatially based on friendship only and then add aggression later
- shells for subgraphs
- fix dupe glitch
- add updated schema types
- color change
- new prefab for subgraph
- select subgraph nodes
- transfer selection to new subnetwork

## Ignore
- move node moving animation to mln
- fix priority select: probably there's a max to hovered items, and further items just don't get registered. solution: minimize number of possible hoverables, in this case, shrinkwrapping cluster size
- add position to subgraph context

## Troubleshooting

1) Restart.