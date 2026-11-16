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
Font License 1.1, whose full text is in `tools/oracle/fonts/LICENSE`.

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

## mermaid

The syntax, default configuration, themes, shapes and test cases follow [mermaid-js/mermaid](https://github.com/mermaid-js/mermaid).
The `docs-*`, `e2e-*` and `parser-*` cases in `corpus/` are taken from its docs and tests. `corpus/SOURCE.txt` names
the tag and commit. The classes in `src/SplashTheMusical/Config/Generated` are generated from its
`packages/mermaid/src/schemas/config.schema.yaml`, and their documentation comments are the schema's descriptions. The
theme classes in `src/SplashTheMusical/Themes` are ports of the files of `packages/mermaid/src/themes`.
`src/SplashTheMusical/Rendering/MermaidStylesheet.cs` is a port of `getStyles` in `packages/mermaid/src/styles.ts` and
`createCssStyles` in `packages/mermaid/src/mermaidAPI.ts`.

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
