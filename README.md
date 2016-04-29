![SharpNav](https://raw.githubusercontent.com/Robmaister/SharpNav/master/Graphics/Logo/Full.png)
[![Build Status](https://img.shields.io/travis/Robmaister/SharpNav.svg)](https://travis-ci.org/Robmaister/SharpNav) [![Build status](https://ci.appveyor.com/api/projects/status/fqegsrv5pdt5b7ng?svg=true)](https://ci.appveyor.com/project/Robmaister/sharpnav) [![NuGet Version](http://img.shields.io/nuget/vpre/SharpNav.svg)](https://www.nuget.org/packages/SharpNav) [![Gratipay Tips](https://img.shields.io/gratipay/Robmaister.svg)](https://gratipay.com/Robmaister)
========


### What is it?

SharpNav is a library that generates and finds paths through navigation meshes. It is functionally equivalent to Mikko Monomen's wonderful [Recast Navigation](https://github.com/memononen/recastnavigation).

SharpNav is proudly an [RCOS](http://rcos.rpi.edu/) project.

### What are the benefits of SharpNav over alternatives?

There are several:
 - **It's portable!** SharpNav is written entirely in C#, so  that the same `SharpNav.dll` will run on all .NET or Mono supported platforms, **no recompilation necessary.**
 - **It's fast!** SharpNav was written with performance in mind and **performs competitively** with Recast.
 - **It's free!** SharpNav is licensed under the [MIT License](LICENSE).
 - **It's clean!** SharpNav aims to maintain a **clean and concise public API** that matches that of the Base Class Library. It also strives to maintain clean source code by following style and structure rules enforced by both StyleCop and FxCop.
 - **It's integrated!** SharpNav has compile configurations to integrate with various graphics toolkits and game engines. In the near future, there will be integration with game engine content pipelines as well.

## Usage

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

For finer control over the generation process, you can refer to the [Examples](Source/SharpNav.Examples) project.

## Quick Start

### NuGet

SharpNav is available on [NuGet](https://www.nuget.org/packages/SharpNav/). It can be installed by issuing the following command in the package manager console:

```
PM> Install-Package SharpNav
```

There are also NuGet packages for the versions of SharpNav that integrate with other frameworks:

 - [SharpNav.MonoGame](https://www.nuget.org/packages/SharpNav.MonoGame)
 - [SharpNav.OpenTK](https://www.nuget.org/packages/SharpNav.OpenTK)
 - [SharpNav.SharpDX](https://www.nuget.org/packages/SharpNav.SharpDX)
 
### From Source

SharpNav follows the standard C# project structure. It is actively developed on Windows with Visual Studio 2015 and on Linux and OS X with MonoDevelop, so it should compile just fine in those cases.

Binaries are not output to their project's local `bin` folder, they are all output to the `Binaries` folder in the repository's root directory.

SharpNav can be configured to depend on other libraries. Each one has it's own compile configuration that require at least one extra NuGet package. The way the project is currently structured requires all of the engine integrations' packages to be downloaded regardless of which configuration is selected (this balloons the size of a checked out and built repository to a little over 400MB). Fixing this would be a great contribution if anyone reading this is a MSBuild guru :stuck_out_tongue:

## Mailing List

Join the [SharpNav mailing list](https://groups.google.com/forum/#!forum/sharpnav) to ask questions about SharpNav or for help with using it. New versions of SharpNav will also be announced on the same mailing list.

## License

SharpNav is licensed under the MIT license. The terms of the MIT license are included in both the [LICENSE](LICENSE) file and below:

```
The MIT License (MIT)

Copyright (c) 2013-2016 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).

SharpNav contains some altered source code from Recast Navigation, Copyright (c) 2009 Mikko Mononen memon@inside.org

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```
Major contributors to SharpNav are listed in the [CONTRIBUTORS](Authors.md) file.
