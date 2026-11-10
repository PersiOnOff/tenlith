# Contributing to Tenlith

Start with the [quick start](README.md#quick-start), [architecture](docs/architecture.md)
and [development guide](docs/development.md). A useful first contribution can be a
reproducible bug report, a test for a tenant boundary, an accessibility fix or a
clearer setup instruction.

For substantial features, open an issue describing the user problem and the
smallest useful scope before implementing. Keep the starter easy to understand;
new infrastructure needs a demonstrated use case.

## Pull requests

- Keep changes focused and explain the resulting behavior.
- Add a regression test for a bug at its responsible boundary.
- Verify backend tests, frontend tests/build and the relevant browser journey.
- For UI changes, include a real screenshot with no personal or secret data.
- For database changes, include a migration and an upgrade/recovery explanation.
- Preserve tenant isolation and API authorization even when a control is hidden.
- Never commit credentials, database dumps or email action tokens.

Commits should describe their intent, e.g. `fix: reject expired team invitations`.
No generated-author trailer or generated-by footer is required.

By contributing, you agree that your contribution is distributed under the
repository's MIT license. Treat contributors respectfully and report security
issues privately according to [SECURITY](SECURITY.md).
