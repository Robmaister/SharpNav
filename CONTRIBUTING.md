Contributing to SharpNav
========================

In general, contributors should follow the style of the code around their
contributions and keep code well-documented.

Contributors are expected to follow these guidelines when contributing to
SharpNav:

  - Your code should follow the [C# Coding Conventions][1] and [Framework
    Design Guidelines][2] as closely as possible except where noted here.
  - Code should be indented with hard tabs with a tabstop of 4.
  - Line length is not strictly enforced and is not currently consistent
    in SharpNav. Use your best judgement and wrap long statements where it
    makes the most sense. In most cases, lines wrap around 120 columns.
  - All public and internal members should be documented with [XML
    Documentation Comments][3].
  - If adding new functionality, write associated unit tests.
  - Ensure that your changes compile on all configurations. When using 2d or
    3d vectors, add this block just after your `using` statements to get your
    changes working with the engine/framework-integrated versions of SharpNav:

```csharp
#if MONOGAME
using Vector3 = Microsoft.Xna.Framework.Vector3;
#elif OPENTK
using Vector3 = OpenTK.Vector3;
#elif SHARPDX
using Vector3 = SharpDX.Vector3;
#endif
```

  - Exceptions to the Framework Design Guideline are acceptable as long as
    they are considered 'normal' patterns for high-performance or video game
    related purposes. For example, the use of large structs with static
    methods and `ref`/`out` parameters. This pattern is commonly used for
    vectors and matrices in just about every C# graphics/game framework.
  - By submitting a pull request, you are licensing your changes under the
    terms of the MIT license. A copy of the MIT license is available in the
    `LICENSE` file in this directory and on the [OSI website][4].

If you are unsure about anything (besides licensing), go ahead and submit the
pull request and a maintainer will let you know if there's anything you should
change to meet these requirements.


[1]: http://msdn.microsoft.com/en-us/library/ff926074.aspx
[2]: http://msdn.microsoft.com/en-us/library/ms229042.aspx
[3]: http://msdn.microsoft.com/en-us/library/b2s063f7.aspx
[4]: http://opensource.org/licenses/MIT
