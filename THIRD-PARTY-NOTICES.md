# Third-party notices

SplashTheMusical includes code and data derived from the following projects.

## Mermaider

Parts of the parsers, layout engines, renderers and tests come from [nullean/mermaider](https://github.com/nullean/mermaider).
`tools/oracle/facts.mjs` and `tools/oracle/extract.mjs` start from its `scripts/mjs-facts.mjs`.
`tools/corpus/corpus.mjs` is a port of its `scripts/mermaid-docs-corpus.py` and `scripts/mermaid-test-corpus.py`. The
`corpus/*/mermaider-*.mmd` cases are its hand-written cases, gallery examples and spec test sources.
`tests/SplashTheMusical.Conformance/OverlapLint.cs` is a port of `tests/Mermaider.Tests/Snapshots/LayoutSkeleton/OverlapLint.cs`
in the [ipjohnson/mermaider](https://github.com/ipjohnson/mermaider) fork.
`src/SplashTheMusical/ResourceLimits.cs`, `src/SplashTheMusical/MermaidResourceLimitException.cs` and
`src/SplashTheMusical/Parsing/ResourceGuard.cs` take their limits, defaults and checks from its `Models/ResourceLimits.cs`,
`MermaidResourceLimitException.cs` and `Rendering/ResourceGuard.cs`.
`src/SplashTheMusical/SvgSanitizer.cs` and `src/SplashTheMusical/Rendering/SvgValues.cs` are a port of `SvgSanitizer.cs`,
`SvgValueAllowlist.cs` and `MermaidSvgException.cs` in the ipjohnson/mermaider fork, and
`tests/SplashTheMusical.Tests/Rendering/SvgSanitizerTests.cs` and `SvgSanitizerFuzzTests.cs` port its tests of them.

```
MIT License

Copyright (c) 2025 Nullean

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Liberation fonts

`tools/oracle/fonts` holds the four Liberation Sans 2.1.5 faces from
[liberationfonts/liberation-fonts](https://github.com/liberationfonts/liberation-fonts). They are under the SIL Open
Font License 1.1, whose full text is in `tools/oracle/fonts/LICENSE`. The advance widths and kerning in
`src/SplashTheMusical/Text/Generated/FontTables.g.cs` are generated from them.

```
Digitized data copyright (c) 2010 Google Corporation
	with Reserved Font Arimo, Tinos and Cousine.
Copyright (c) 2012 Red Hat, Inc.
	with Reserved Font Name Liberation.
```

## khroma

`src/SplashTheMusical/Themes/Khroma.cs` is a port of the parsing, formatting and `adjust`, `darken`, `lighten`,
`invert`, `isDark`, `rgba` and `mix` functions of [fabiospampinato/khroma](https://github.com/fabiospampinato/khroma)
2.1.0, with its table of colour keywords.

```
The MIT License (MIT)

Copyright (c) 2019-present Fabio Spampinato, Andrew Maney

Permission is hereby granted, free of charge, to any person obtaining a
copy of this software and associated documentation files (the "Software"),
to deal in the Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
DEALINGS IN THE SOFTWARE.
```

## stylis

`CssText.AppendStylis` in `src/SplashTheMusical/Rendering/CssText.cs` follows the whitespace rules of
[thysultan/stylis](https://github.com/thysultan/stylis) 4, the CSS compiler mermaid uses, so STM writes the same text.

```
MIT License

Copyright (c) 2016-present Sultan Tarimo

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## dagre-d3-es

`src/SplashTheMusical.Layout/Dagre` is a port of the `src/dagre` and `src/graphlib` folders of
[tbo47/dagre-es](https://github.com/tbo47/dagre-es), published as dagre-d3-es 7.0.14, the version mermaid 12.3.0 uses.

```
Original dagre-d3 copyright: Copyright (c) 2013 Chris Pettitt
Original dagre copyright: Copyright (c) 2012-2014 Chris Pettitt
Original graphlib copyright: Copyright (c) 2012-2014 Chris Pettitt

Copyright (c) 2022-2024 Thibaut Lassalle, David Newell, Alois Klink, Sidharth Vinod and dagre-es contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

## d3-shape and d3-path

`src/SplashTheMusical.Layout/Paths/CurveWriter.cs` ports the line curves of [d3/d3-shape](https://github.com/d3/d3-shape)
3.2.1, and `PathWriter.cs` the path writer of [d3/d3-path](https://github.com/d3/d3-path) 3.1.1.

```
Copyright 2010-2026 Mike Bostock (d3-shape)
Copyright 2015-2026 Mike Bostock (d3-path)

Permission to use, copy, modify, and/or distribute this software for any purpose
with or without fee is hereby granted, provided that the above copyright notice
and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS
OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER
TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF
THIS SOFTWARE.
```

## fdlibm

`src/SplashTheMusical.Layout/Paths/JsMath.cs` ports fdlibm's `acos`, `atan`, `atan2`, `sin`, `cos`, their kernels and
the medium-size `rem_pio2`, as V8's `src/base/ieee754.cc` has them.

```
Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.

Developed at SunSoft, a Sun Microsystems, Inc. business.
Permission to use, copy, modify, and distribute this
software is freely granted, provided that this notice
is preserved.
```

## roughjs

`src/SplashTheMusical/Rendering/Shapes/RoughEllipse.cs` ports the roughness-0 ellipse of
[rough-stuff/rough](https://github.com/rough-stuff/rough) 4.6.6, the version mermaid 12.3.0 uses: `generateEllipseParams`,
`_computeEllipsePoints` and `_curve` in `bin/renderer.js`.

```
MIT License

Copyright (c) 2019 Preet Shihn

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Iconify utils

`MermaidIcon.Drawing` and the alias merging and name splitting in `src/SplashTheMusical/Icons` port `iconToSVG`,
`mergeIconData` and `stringToIcon` of [iconify/iconify](https://github.com/iconify/iconify)'s `@iconify/utils`, which
mermaid 12.3.0 uses. `IconPack.FromIconifyJson` reads its JSON icon set format.

```
MIT License

Copyright (c) 2021-PRESENT Vjacheslav Trushkin

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## js-sequence-diagrams

mermaid's `sequenceDiagram.jison`, which `src/SplashTheMusical/Diagrams/Sequence/SequenceLexer.cs` and
`SequenceParser.cs` port, is based on the jison grammar of [js-sequence-diagrams](https://bramp.github.io/js-sequence-diagrams/),
under the Simplified BSD license.

```
Copyright (c) 2012-2013 Andrew Brampton (bramp.net)
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## mermaid

The syntax, default configuration, themes, shapes and test cases follow [mermaid-js/mermaid](https://github.com/mermaid-js/mermaid).
The `docs-*`, `e2e-*` and `parser-*` cases in `corpus/` are taken from its docs and tests. `corpus/SOURCE.txt` names
the tag and commit. The classes in `src/SplashTheMusical/Config/Generated` are generated from its
`packages/mermaid/src/schemas/config.schema.yaml`, and their documentation comments are the schema's descriptions. The
theme classes in `src/SplashTheMusical/Themes` are ports of the files of `packages/mermaid/src/themes`.
`src/SplashTheMusical/Rendering/MermaidStylesheet.cs` is a port of `getStyles` in `packages/mermaid/src/styles.ts` and
`createCssStyles` in `packages/mermaid/src/mermaidAPI.ts`. `src/SplashTheMusical/Text/MermaidText.cs` is a port of
`hasBreaks` and `splitBreaks` in `packages/mermaid/src/diagrams/common/common.ts`, and of `wrapLabel`, `breakString` and
`calculateTextDimensions` in `packages/mermaid/src/utils.ts`. In `src/SplashTheMusical.Layout`, `Paths/EdgePath.cs` and
`Paths/RoundedPath.cs` port `insertEdge`'s path code in `packages/mermaid/src/rendering-util/rendering-elements/edges.js`
and `getLineFunctionsWithOffset` in `packages/mermaid/src/utils/lineWithOffset.ts`, and `Markers` holds the markers of
`rendering-elements/markers.js` and the marker ids of `rendering-elements/edgeMarker.ts`.
`src/SplashTheMusical/Diagrams/Flowchart` ports the lexer and grammar of
`packages/mermaid/src/diagrams/flowchart/parser/flow.jison`, the actions of `flowDb.ts`, the text rewrites of
`flowParser.ts`, `packages/mermaid/src/preprocess.ts` and `encodeEntities` in `packages/mermaid/src/utils.ts`. Its
layout and drawing port `getData` in `flowDb.ts`, `flowRenderer-v3-unified.ts`, the dagre renderer and
`mermaid-graphlib.js` of `packages/mermaid/src/rendering-util/layout-algorithms/dagre`, and the clusters, edges,
edge labels and `createText` of `packages/mermaid/src/rendering-util`.
`src/SplashTheMusical/Rendering/Shapes` ports the node shapes of `packages/mermaid/src/rendering-util/rendering-elements/shapes`
and the intersections of `rendering-elements/intersect`.
`src/SplashTheMusical/Diagrams/Requirement` ports `packages/mermaid/src/diagrams/requirement`: the grammar of
`parser/requirementDiagram.jison`, `requirementDb.ts`, `requirementRenderer.ts` and `styles.js`, and the
`requirementBox` shape of `rendering-elements/shapes/requirementBox.ts`.
`src/SplashTheMusical/Diagrams/Block` ports `packages/mermaid/src/diagrams/block`: the grammar of
`parser/block.jison`, `blockDB.ts` and the columns check of `layout.ts`.
`src/SplashTheMusical/Diagrams/Sequence` ports `packages/mermaid/src/diagrams/sequence`: the lexer and grammar of
`parser/sequenceDiagram.jison`, which is based on js-sequence-diagrams (see above), and the actions, `apply`,
`parseMessage` and `parseBoxData` of `sequenceDb.ts`.
`src/SplashTheMusical/Icons/MermaidIcons.cs` holds `unknownIcon` of `packages/mermaid/src/rendering-util/icons.ts` and
the icons of `packages/mermaid/src/diagrams/architecture/architectureIcons.ts`.

```
The MIT License (MIT)

Copyright (c) 2014 - 2022 Knut Sveidqvist

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```
