# Brief: Automate Analytics

People who run automations can't see how they're doing over time: how many ran, how many failed, which automations and actions break most, and whether that's getting better or worse. Today they open runs one at a time. Every other Umbraco product with an analytics screen is moving to the shared Analytics UI package for CMS 19, and Automate should look and behave the same.

The audience is backoffice users with access to the Automate section. They see only the workspaces they're a member of, and admins see all of them. Site visitors and developers building on Automate aren't the audience.

## Why now
- Engage, Commerce, Umbraco AI, Umbraco Forms and Umbraco Workflow are all moving their analytics screens to Analytics UI for CMS 19. Automate should match them.
- No deadline. The brief is being written now so the idea isn't lost.

## Done when
- From the Automate section, a user can open Analytics and see, for the last 7, 30 or 90 days and optionally one workspace:
  - runs per day, split by outcome (completed, failed, rejected, cancelled), compared with the previous period
  - total runs, success rate, failed runs, runs waiting for approval now, and median run time, each with its change against the previous period
  - a table of automations: runs, success rate, failures, average run time and last run, each linking to its automation
  - a table of actions: runs, failure rate, retries, average run time and the most common kind of error (for example authentication, which usually means expired credentials)
- A comparison is only shown where the earlier period's runs still exist. Runs are deleted after 90 days by default.
- The Overview dashboard keeps its status cards and recent activity, and links to Analytics for the detail.
- A user sees only runs from workspaces they can access, the same rule as the run list and the Overview dashboard.
- For the same date range and workspace, the counts match what the run list shows.
- Days are the user's own days, not UTC days: a run at 00:30 in Copenhagen counts on that Copenhagen date.
- Each chart and table loads in under 2 seconds on SQL Server with 500,000 runs in the last 90 days. If design can't get there, the feature is parked.
- It works in the light, dark and high-contrast backoffice themes.

## Must work with
- Umbraco CMS 19 only, on the `v19/dev` line (currently 19.0.0-beta1 on .NET 11). No v17 or v18 version: Analytics UI only exists for CMS 19.
- Analytics UI 19.x, referenced as `[19.0.0, 19.999.999)`. It supplies the charts, tables and tiles; Automate supplies the data.
- SQL Server and SQLite.
- Load-balanced sites. All data comes from the database, never from one server's memory.
- Workspace-based access control.
- Run retention, which is configurable and 90 days by default. A site can set it lower or turn it off.

## Unknowns
- **Analytics UI isn't released.** It has no tags, and its README says the npm and NuGet.org publishing isn't set up yet. If it isn't out in time for Automate 19.0.0, this waits for it.
- **Query speed on big sites.** Runs are indexed by automation and date, but not by workspace and date. Step runs are only indexed by run. Design checks this first against the 2-second target.
- **Is the Overview dashboard enough?** Design checks first whether a few more numbers on Overview would cover the need. If so, the feature is parked.
- **Grouping by the user's days on both databases.** The CMS's database layer can't convert time zones the same way on SQL Server and SQLite.
- **Short retention.** If a site keeps runs for fewer days than the chosen range, the start of the chart is empty, and that has to read as "deleted", not "no runs".
- **Automate isn't one of Analytics UI's listed users.** Its brief names Engage, Commerce, AI, Forms and Workflow, and "a change has to work for all". Its owners should know Automate is joining before Automate depends on it.

## Not doing
- **v17 and v18.** Analytics UI can't be used on the older lines.
- **Approvals, runs by hour of day, how far runs get through each automation, and runs by trigger.** They come after the first version.
- **Numbers Automate doesn't store today:** rate-limit rejections, outbox throughput and failures per connection. Adding storage for them is a separate feature that needs a database migration.
- **Restyling the Overview dashboard.** It only gets a link to Analytics.
