SharpNav
========
[![Build Status](https://travis-ci.org/Robmaister/SharpNav.svg?branch=master)](https://travis-ci.org/Robmaister/SharpNav)

### What is it?

SharpNav is a library that generates and finds paths through navigation meshes. It is functionally equivalent to Mikko Monomen's wonderful [Recast Navigation](https://github.com/memononen/recastnavigation).

### What are the benefits of SharpNav over alternatives?

There are several:
 - **It's portable!** SharpNav is written entirely in C#, so  that the same `SharpNav.dll` will run on all .NET or Mono supported platforms, **no recompilation necessary.**
 - **It's fast!** SharpNav was written with performance in mind and **performs competitively** with Recast.
 - **It's free!** SharpNav is licensed under the [MIT License](https://github.com/Robmaister/SharpNav/blob/master/LICENSE).
 - **It's clean!** SharpNav aims to maintain a **clean and concise public API** that matches that of the Base Class Library. It also strives to maintain clean source code by following style and structure rules enforced by both StyleCop and FxCop.
 - **It's integrated!** SharpNav has compile configurations to integrate with various graphics toolkits and game engines. In the near future, there will be integration with game engine content pipelines as well.

### Usage

SharpNav is both highly configurable and simple to use. If you want to generate a `NavMesh` from a single mesh, it is as simple as the following block of code:

``` CSharp
//prepare the geometry from your mesh data
var tris = TriangleEnumerable.FromIndexedVertices( ... );

//use the default generation settings
var settings = NavMeshGenerationSettings.Default;
settings.AgentHeight = 1.7f;
settings.AgentWidth = 0.6f;

//generate the mesh
var navMesh = NavMesh.Generate(tris, settings);
```

For finer control over the generation process, you can refer to the [Examples](https://github.com/Robmaister/SharpNav/tree/master/Examples) project.
### Compiling

SharpNav follows the standard C# project structure. It is actively developed on Windows with Visual Studio 2012 and on Linux and OS X with MonoDevelop, so it should compile just fine in those cases.

SharpNav can be configured to depend on other libraries. Each one has it's own compile configuration that requires the library to be installed on the machine (in the GAC or the assembly search path), with the exception being OpenTK. OpenTK is used for the Examples project, and is therefore included in the repository. If you want to compile against a different version of OpenTK, drop in your replacement in the [`Binaries`](https://github.com/Robmaister/SharpNav/tree/master/Binaries) folder.

SharpNav is proudly an [RCOS](http://rcos.rpi.edu/) project.
