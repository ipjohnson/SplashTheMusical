# Getting started

STM renders a Mermaid source to SVG through `IMermaid`. `Mermaid.Default` is a ready instance, and the
`SplashTheMusical.DependencyModule` package registers one in a service collection.

## Render through dependency injection

This is the sample host in `samples/SplashTheMusical.Sample`, which CI builds with native AOT and runs on every pull
request:

<<< @/../samples/SplashTheMusical.Sample/Program.cs{cs}

## How close STM is to mermaid.js

The [parity report](/parity) gives the pass rate of each diagram type at each level. The [comparison](/compare/) shows
mermaid.js's drawing and STM's of every corpus case side by side.
