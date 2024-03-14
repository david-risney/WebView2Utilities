# How to contribute

One of the easiest ways to contribute is to participate in discussions on GitHub issues. You can also contribute by submitting pull requests with code changes.

## General feedback and discussions?

Start a discussion on the [repository issue tracker](/issues).

## Bugs and feature requests?

❗ **IMPORTANT: If you want to report a security-related issue, please see [security.md](./SECURITY.md)**

Before reporting a new issue, try to find an existing issue if one already exists. If it already exists, upvote (👍) it. Also, consider adding a comment with your unique scenarios and requirements related to that issue.  Upvotes and clear details on the issue's impact help us prioritize the most important issues to be worked on sooner rather than later. If you can't find one, that's okay, we'd rather get a duplicate report than none.

## How to contribute

### Getting Started

1. Enlist (Clone the Repo):
  - `git clone https://github.com/david-risney/WebView2Utilities.git` <!-- TODO update this URL -->
  - If you have a preferred method for cloning GitHub projects, feel free to use that instead.

2. Build:
  - Open `wv2util/wv2util.sln` in Visual Studio (VS).
  - Ensure the solution configurations are set to `Debug` or `Release`, and the platform is set to `Any CPU`.
  - Click `Build` to build the solution.

3. Debug:
  - Ensure that VS is running as an administrator.
  - Use VS to debug (F5).
  - If wv2util is not running as an administrator, VS will restart it as an administrator. Therefore, it is important to run VS as an administrator so that you will be debugging the correct process.

4. Test:
  - There are tests in the same solution, but different project (wv2utilTests), that you can run in VS (Ctrl+R).

### Submitting Code Changes

#### Submitting a pull request

1. Create a new branch from the `main` branch.
  - `git checkout -b my-branch-name`
  - Push your code to a user branch

2. Open a pull request (PR) in GitHub.
  - Open a PR in GitHub and describe the changes you are making.
  - If you are fixing an issue, reference the issue in the PR description.

<!-- Uncomment this section once the project has a CLA in place.

> **CLA**
> 
> This project welcomes contributions and suggestions. Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.microsoft.com
> 
> When you submit a pull request, a CLA-bot will automatically determine whether you need to provide a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions provided by the bot. You will only need to do this once across all repositories using our CLA.

--> 

3. PR/push pipeline
  - PRs and push in GitHub will build the code and run the tests in GitHub Actions.
  - If the PR fails, you will need to address the issues and push the changes to the same branch.

4. Feedback and review
  - If the pull request is ready for review, the team member will assign the pull request to a reviewer. A core contributor will review your pull request and provide feedback. 

5. Merge
  - When your pull request has had all feedback addressed, it has been signed off by one or more reviewers with commit access, and all checks are green, we will commit it.

## Code of conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.