# Changelog fragments

Add a Markdown fragment for each user-facing change. Use a fun, whimsical lowercase
kebab-case filename, such as `fixed/dancing-marshmallow.md`.

| Folder | Changes |
| --- | --- |
| `added` | New features |
| `stabilized` | Features that are no longer experimental |
| `changed` | Changes to existing functionality |
| `deprecated` | Features planned for removal |
| `breaking-changes` | Removed or backwards-incompatible features |
| `fixed` | Bug fixes |
| `security` | Security fixes |

Keep entries concise, ideally one or two sentences. Write each entry on one line
without a leading `-`; every nonempty line becomes a separate bullet when the tooling
collects fragments. Blank lines are ignored. A fragment can contain multiple entries.
Use inline Markdown and links as needed, without headings or nested lists.

`CHANGELOG.md` contains completed releases only. `mise run changelog:check` validates
pending fragments. The shared Core tooling imports Core changes during submodule
updates and collects fragments during release preparation; see
[CONTRIBUTING.md](../CONTRIBUTING.md#updating-sdk-core) for instructions.
