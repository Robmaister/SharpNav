// Copyright (c) 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).
// Licensed under the MIT License - https://raw.github.com/Robmaister/SharpNav/master/LICENSE

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("SharpNav")]
[assembly: AssemblyDescription("A fully-managed navigation mesh library.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("SharpNav")]
[assembly: AssemblyCopyright("Copyright © 2013-2015 Robert Rouhani <robert.rouhani@gmail.com> and other contributors (see CONTRIBUTORS file).")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: CLSCompliant(true)]

#if MONOGAME
[assembly: InternalsVisibleTo("SharpNav.Tests")]
#else
[assembly: InternalsVisibleTo("SharpNav.Tests, PublicKey=00240000048000009400000006020000002400005253413100040000010001007db4cb90a9a4ef6908e5293fffcba3c76e490fd182595f7fd6fa32801b29ed416181383f2da9dac4271ae5f3c89a66ece56668415dcc274d775b1efbe4e013e840221fb12cfd99ffaf405ad124e5d301ba55610ee4fcf25687faf3434574b397773e720fb6573eb2e935e585926e365a1d8ac2fd864cf0b9b11932d2abb4e9af")]
#endif

// I'm attempting to follow Semantic Versioning with the built-in C# Version class as a fallback
// or alternative to NuGet's semver support. This means I need to do some special things with this
// version number to make sure that comparing these versions match the same order that the semver
// versions do.
// 
// Major and Minor are the same as semver. C#'s "Build" number is semver's "Patch" number. The
// "Revision" number is special. It's based on pre-releases and follow these rules:
// 
// * Alpha versions start at 0 and increment once per released version.
// * Beta versions start at 100 and increment once per released version.
// * The final release of any version will have a revision number of 200.
// 
// This way, a consumer can programmatically determine whether a release of SharpNav is an alpha or
// beta pre-release, or the final released version. comparability between SemVer and C#'s Version
// class is also maintained.
// 
//  SemVer Version |   C# Version
// ----------------|----------------
//     0.9.0       |   0.9.0.200
//  1.0.0-alpha.1  |    1.0.0.0
//  1.0.0-alpha.2  |    1.0.0.1
//   1.0.0-beta.1  |   1.0.0.100
//   1.0.0-beta.2  |   1.0.0.101
//     1.0.0       |   1.0.0.200
//     1.1.0       |   1.1.0.200
//   1.2.0-beta.1  |   1.2.0.100
//     1.2.0       |   1.2.0.200
//     2.0.1       |   2.0.1.200
// 
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// There are two ways of differentiating versions of SharpNav.dll that are built with engine integrations.
// The first is metadata included with the SemVer version (e.g. 1.0.0-alpha.2+monogame). The second is that
// each version is signed with a separate strong name key, which provides a few extra benefits. Multiple
// integrated versions of SharpNav can now be installed to the GAC without worrying about them interfering
// with one another.
#if MONOGAME
[assembly: AssemblyInformationalVersion("1.0.0-alpha.2+monogame")]
#elif OPENTK
[assembly: AssemblyInformationalVersion("1.0.0-alpha.2+opentk")]
#elif SHARPDX
[assembly: AssemblyInformationalVersion("1.0.0-alpha.2+sharpdx")]
#else
[assembly: AssemblyInformationalVersion("1.0.0-alpha.2")]
#endif

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("603f4e3f-26ed-4338-b400-0a1a2fe0cf10")]
