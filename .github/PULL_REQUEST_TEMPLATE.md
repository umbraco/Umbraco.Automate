### Prerequisites

- [ ] Branch name follows the convention (`vN/feature/<anything>`, see [CONTRIBUTING.md](https://github.com/umbraco/Umbraco.Automate/blob/v18/dev/CONTRIBUTING.md#branch-naming-convention))
- [ ] PR targets the correct `vN/dev` base branch
- [ ] PR title follows [Conventional Commits](https://www.conventionalcommits.org/) (example: `fix(trigger): Resolve memory leak in event listener`)
- [ ] I have added steps to test this contribution in the description below

If there's an existing issue for this PR then this fixes <!-- link to the issue here! -->

### Description

<!--
    A description of the changes proposed in the pull-request and how to test these changes.

    The most successful pull requests usually look a like this:

    * Fill in this template with details: what did you do, why did you do it, how can we test the changes?
    * Include screenshots and animated GIFs whenever there is a backoffice change.
    * Unit tests, while optional are awesome, thank you!

    While these are guidelines and not strict requirements, they really help us evaluate your PR quicker.
-->

### Checks

- [ ] `dotnet build` and `dotnet test` pass for the affected product(s)
- [ ] Frontend builds (only if there are frontend changes)
- [ ] Documentation updated (only if needed)
- [ ] Database migrations added (only if the schema changed)

### Other version lines

Umbraco.Automate maintains several version lines at once and they are never forward-merged, so each line needs its own PR. See the [backport workflow](https://github.com/umbraco/Umbraco.Automate/blob/v18/dev/CONTRIBUTING.md#development-workflow).

- [ ] This change only applies to the version line I am targeting
- [ ] This change should be ported to another active line — linked PR: <!-- link here, or say "to follow" -->

<!-- Thanks for contributing to Umbraco.Automate! -->
