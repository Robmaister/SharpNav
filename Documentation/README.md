SharpNav Documentation
======================

If you came here looking for docs, you should go to http://sharpnav.com/docs
for the compiled web documentation. This directory contains markdown versions
of the pages included in the compiled docs as well as a configuration for
[SharpDoc][1], the tool used to generate the documentation.

## Building Documentation

Before anything else, make sure you have built at least one version of
SharpNav (preferrably in the Standalone configuration).

Almost everything you need is in this directory. There is no NuGet package for
SharpDoc yet, so you should clone SharpDocs and compile the solution. If you'd
like to take advantage of the `build-docs` script in the `Build/` directory,
make sure SharpDocs is cloned in the same directory that SharpNav is cloned
in. For example, if SharpNav were located in `C:\Projects\SharpNav`, then
clone SharpDoc to `C:\Projects\SharpDoc`.

**Note**: This process is not limited to Windows. Mono can do everything
described here.

With the script, you can simply call `./build-docs.sh` and
`./build-docs.sh --clean` to generate and delete documentation, respectively.
The generated docs will be output to the `/Binaries/Documentation` directory
by default.

If you aren't using the script, you can build documentation with the following
command:

```
SharpDoc.exe --config=<PATH TO THIS DIR>/config.xml --output=<OUTPUT DIR> <PATH TO DLL>/SharpNav.dll
```


[1]: https://github.com/xoofx/SharpDoc
