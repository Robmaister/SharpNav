SharpNav
========

**NOTE:** SharpNav is still in its development phase. Use it if you want, but
there are still a decent number of bugs and the public API is still subject to
significant changes.

SharpNav is a fully-managed no-dependency C# library that generates navigation
meshes from level and model data and does pathfinding through navigation
meshes. It is a functionally equivalent library to Mikko Monomen's
[Recast Navigation](https://github.com/memononen/recastnavigation) for C# and
other CLI-based languages.

In the future, SharpNav will have multiple released version that integrate
with various game and graphics engines that use C# alongside the standalone
release (mostly using each engine's math classes like `Vector3`). Some engines
like MonoGame and Unity3D will likely also have a separate library/plugin to
make loading navigation meshes from the content pipeline or perhaps even
regenerating a navigation mesh when a model or scene file changes easy and
automatic.

Once the library is mostly done I'll also start working on on a standalone
editor to make adding off-mesh links and tweaking a navmesh a simple and
visual process.

SharpNav is proudly an [RCOS](http://rcos.rpi.edu/) project.
