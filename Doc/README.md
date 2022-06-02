# `sdk-dotnet /Doc/`

This folder contains documentation-related files.

Do not confuse it with the [`/Docs`](../Docs) directory, which is reserved for use by Github Pages automation (the name is dictated by Github).

The contents of this folder and its sub-folders are rendered into a documentation website using DocFx. Most of the materials are designed for both, direct consumption (i.e. just access them from the repo), as well as for being rendered into the website. However, there are some helper files that are specifically intended to support the rendering process. For example, the `toc.yml` files help render the website's menu and navigation bars.

* The [Articles](./Articles/) folder contains documentation related to the Temporal SDK for .NET that does not fit well as in-code comments and is not generic enough to be published on the high-level temporal website.

* The [ApiDoc](./ApiDoc/) folder contains assets that are embedded directly into the docs which are generated from in-code comments.