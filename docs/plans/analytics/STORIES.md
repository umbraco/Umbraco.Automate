# Stories: Automate Analytics

**8 stories, 70 criteria.** Story ids are used by `PLAN.md` and the spec files, so never renumber them.

An "Automate user" below is a backoffice user with access to the Automate section. "The page" is the Analytics workspace's Overview view. Rule numbers (R1 to R17) are `SPEC.md`'s.

**Speed**

- [AN-1](#an-1-analytics-loads-fast-on-a-large-site) Analytics loads fast on a large site, 3 criteria

**Numbers (API and page together)**

- [AN-2](#an-2-headline-numbers) Headline numbers, 12 criteria
- [AN-3](#an-3-runs-over-time) Runs over time, 10 criteria
- [AN-4](#an-4-automations-table) Automations table, 8 criteria
- [AN-5](#an-5-actions-table) Actions table, 11 criteria

**Filters**

- [AN-6](#an-6-only-my-workspaces) Only my workspaces, 7 criteria
- [AN-7](#an-7-date-range-and-comparison) Date range and comparison, 13 criteria

**Getting there**

- [AN-8](#an-8-open-analytics) Open Analytics, 6 criteria

<details>
<summary>Ready and Done</summary>

**Ready:** the role, capability and value are stated and not hollow; Given/When/Then criteria cover the happy path; what's out of scope is explicit; the story passes INVEST.

**Done:** every criterion passes as an executable spec (C# for the server, Playwright acceptance tests for the page); sad-path criteria are covered too; the `reviewer` agent has passed the task.

</details>

---

## AN-1: Analytics loads fast on a large site

As an admin of a site with 500,000 runs in 90 days,
I want each part of Analytics to answer in under 2 seconds,
so that I can open it every day without waiting.

Out of scope: SQLite speed (development sites only).

The data: SQL Server, 500,000 runs and about 2,000,000 step runs spread over the last 90 days, and the same again over the 90 days before, with run cleanup off so the comparison is really counted (R7). All workspaces, a 90-day range compared with the 90 days before, second call.

AC1 — Summary is fast
  Given the data above
  When the summary endpoint is called
  Then it answers in under 2 seconds

AC2 — Automations are fast
  Given the data above
  When the automations endpoint is called
  Then it answers in under 2 seconds

AC3 — Actions are fast
  Given the data above
  When the actions endpoint is called
  Then it answers in under 2 seconds

## AN-2: Headline numbers

As an Automate user,
I want the period's runs, success rate, failures, median run time and waiting approvals at the top of the page,
so that I can tell at a glance whether my automations are healthy.

Out of scope: tiles that switch the chart's metric.

AC1 — Runs counts every run that started
  Given 3 completed, 1 failed and 1 running run started in the range
  When the summary is requested
  Then `current.runs` is 5

AC2 — Success rate counts rejected as success
  Given 6 completed, 2 rejected, 1 cancelled and 1 failed run in the range
  When the summary is requested
  Then `current.successRate` is 0.8

AC3 — Success rate ignores runs still going
  Given 4 completed runs and 3 suspended runs in the range
  When the summary is requested
  Then `current.successRate` is 1

AC4 — Failed runs are counted
  Given 2 failed runs in the range
  When the summary is requested
  Then `current.failed` is 2

AC5 — Median run time
  Given finished runs that took 1, 2, 3, 4 and 60 seconds
  When the summary is requested
  Then `current.medianDurationMs` is 3000

AC6 — A finished run stores its run time
  Given a run that started at 10:00:00 and ended at 10:00:02.5
  When it is saved
  Then its `DurationMs` is 2500

AC7 — The migration fills in old runs
  Given a finished run saved before the migration
  When the migration runs
  Then its `DurationMs` is its end time minus its start time in milliseconds

AC8 — Waiting for approval ignores the range
  Given 2 runs started last year that are still suspended
  When the summary is requested for the last 7 days
  Then `waitingForApproval` is 2

AC9 — Tiles show the change
  Given 110 runs in the range and 100 in the comparison
  When the page loads
  Then the Runs tile shows a badge of +10%

AC10 — Waiting for approval opens Approvals
  Given the page has loaded
  When the user clicks the Waiting for approval tile
  Then the Approvals dashboard opens

**Sad path**

AC11 — No finished runs means no success rate
  Given only running runs in the range
  When the page loads
  Then the Success rate tile shows a dash with no badge

AC12 — A run that never started isn't counted
  Given 1 pending run with no start time and 1 completed run in the range
  When the summary is requested
  Then `current.runs` is 1

## AN-3: Runs over time

As an Automate user,
I want runs per day split by outcome, compared with the period before,
so that I can see when failures started and whether volume is changing.

Out of scope: hourly bars (R14).

AC1 — Every day is in the answer
  Given a 7-day range with runs on only 2 of the days
  When the summary is requested
  Then `current.days` has 7 entries

AC2 — Each day is split by outcome
  Given 3 completed, 1 rejected, 1 cancelled, 2 failed and 1 running run on 2 October
  When the summary is requested
  Then the 2 October entry is completed 3, rejected 1, cancelled 1, failed 2, inProgress 1

AC3 — Days are the user's days
  Given a run started at 23:30 UTC on 1 October 2026
  When the summary is requested with time zone Europe/Copenhagen
  Then the run counts on 2 October

AC4 — Quarter-hour time zones are right
  Given a run started at 18:20 UTC on 1 October 2026
  When the summary is requested with time zone Asia/Kathmandu
  Then the run counts on 2 October

AC5 — The day the clocks go back is one day
  Given runs started at 00:30 and 23:30 Copenhagen time on 25 October 2026
  When the summary is requested with time zone Europe/Copenhagen
  Then both count on 25 October

AC6 — Week groups days into weeks
  Given the chart showing 14 days by day
  When the user picks Week
  Then each bar holds the total of the days in its week, starting on the user's first day of the week

AC7 — Month groups days into months
  Given a 90-day range
  When the user picks Month
  Then each bar holds the total of the days in its calendar month

AC8 — Grouping doesn't refetch
  Given the page has loaded
  When the user switches between Day, Week and Month
  Then no request is sent to the server

AC9 — The comparison is drawn behind
  Given a range with a comparison
  When the page loads
  Then each day's bar has the comparison period's bar behind it

**Sad path**

AC10 — No runs
  Given no runs in the range
  When the page loads
  Then the chart says "No runs in this period"

## AN-4: Automations table

As an Automate user,
I want each automation's runs, failures, success rate, run time and last run in one table,
so that I can find the automation that's failing and open it.

Out of scope: paging (R15).

AC1 — One row per automation that ran
  Given automations A and B ran in the range and C didn't
  When the automations are requested
  Then the items are A and B

AC2 — A row has its figures
  Given automation A had 8 completed, 1 rejected and 1 failed run averaging 2 seconds, the last starting at 14:02 on 6 November
  When the automations are requested
  Then A's row is runs 10, failed 1, successRate 0.9, avgDurationMs 2000, lastRunUtc 14:02 on 6 November

AC3 — The row names the workspace
  Given automation A is in the Commerce workspace
  When the automations are requested
  Then A's `workspaceName` is "Commerce"

AC4 — The name opens the automation
  Given the table has loaded
  When the user clicks an automation's name
  Then that automation's editor opens

AC5 — Rows show the change
  Given a range with a comparison where A ran 100 times before and 110 now
  When the page loads
  Then A's runs show a badge of +10%

AC6 — Search
  Given the table lists "Sync new orders" and "Weekly report"
  When the user types "orders" in its search box
  Then only "Sync new orders" is listed

**Sad path**

AC7 — Deleted automations aren't listed
  Given runs of an automation that has since been deleted
  When the automations are requested
  Then no row has that automation's id

AC8 — Nothing ran
  Given no runs in the range
  When the page loads
  Then the automations table shows its empty state

## AN-5: Actions table

As an Automate user,
I want each action's runs, failure rate, retries, run time and most common error,
so that I can see which integration is breaking, such as expired credentials.

Out of scope: failures per connection (not stored).

AC1 — One row per action that ran
  Given step runs of HTTP Request and Send Email in the range
  When the actions are requested
  Then the items are HTTP Request and Send Email

AC2 — Skipped steps aren't runs
  Given 4 completed and 3 skipped Send Email step runs
  When the actions are requested
  Then Send Email's `runs` is 4

AC3 — Failure rate
  Given 7 completed, 1 rejected and 2 failed step runs of an action
  When the actions are requested
  Then its `failureRate` is 0.2

AC4 — Retries add up
  Given 3 step runs of an action with RetryCount 0, 2 and 3
  When the actions are requested
  Then its `retries` is 5

AC5 — Average run time
  Given 2 finished step runs of an action taking 1 and 3 seconds
  When the actions are requested
  Then its `avgDurationMs` is 2000

AC6 — Most common error
  Given an action with 3 failed step runs of category Authentication and 1 of Timeout
  When the actions are requested
  Then its `topErrorCategory` is "Authentication"

AC7 — A step counts on the day it started
  Given a run that started on 31 October with a step that started on 1 November
  When the actions are requested for 1 November only
  Then that step run is counted

AC8 — Names come from the catalogue
  Given the table has loaded
  When the action `umbracoAutomate.httpRequest` is shown
  Then its row reads "HTTP Request"

**Sad path**

AC9 — No failures
  Given an action with no failed step runs
  When the page loads
  Then its most common error reads "None"

AC10 — An unknown error reads Other
  Given an action whose failures are all of category Unknown
  When the page loads
  Then its most common error reads "Other"

AC11 — An action from a removed package
  Given step runs of an action that's no longer installed
  When the page loads
  Then its row shows its alias

## AN-6: Only my workspaces

As an Automate user,
I want Analytics to cover only the workspaces I can access, and to narrow it to one,
so that I see my team's automations and nobody else's.

AC1 — Non-admins see their workspaces
  Given a non-admin with access to workspace Content but not Commerce
  When the summary is requested without a workspace
  Then only runs of Content automations are counted

AC2 — Admins see everything
  Given an admin
  When the summary is requested without a workspace
  Then runs of every workspace are counted

AC3 — One workspace
  Given a user with access to Content and Commerce
  When the summary is requested for Commerce
  Then only runs of Commerce automations are counted

AC4 — The current workspace counts
  Given an automation that ran in Content and was then moved to Commerce
  When the summary is requested for Commerce
  Then its runs are counted

AC5 — The select lists my workspaces
  Given a user with access to Content and Commerce
  When the page loads
  Then the workspace select offers All workspaces, Commerce and Content

**Sad path**

AC6 — A workspace I can't access
  Given a non-admin without access to Commerce
  When the summary is requested for Commerce
  Then it answers 200 with `current.runs` 0

AC7 — Only one workspace
  Given a user with access to just one workspace
  When the page loads
  Then the workspace select isn't shown

## AN-7: Date range and comparison

As an Automate user,
I want to choose the period and compare it with the one before, and find my choice there next time,
so that I can look at the right window without setting it up every visit.

AC1 — First visit
  Given the user has never opened Analytics in this browser
  When the page loads
  Then it shows the last 30 days compared with the 30 days before, for all workspaces

AC2 — The presets
  Given the page has loaded
  When the user opens the date range
  Then it offers Last 7 days, Last 30 days, Last 90 days and Custom

AC3 — A new range reloads everything
  Given the page has loaded
  When the user picks Last 7 days
  Then the summary, automations and actions are all requested again for that range

AC4 — The choice is remembered
  Given the user picked Last 90 days and Commerce, then left
  When they open Analytics again
  Then it shows Last 90 days for Commerce

AC5 — Comparison off
  Given the user turns the comparison off
  When the page reloads its data
  Then no tile, bar or table row shows a change badge

AC6 — A deleted comparison period
  Given runs are kept for 90 days and the comparison starts 120 days ago
  When the summary is requested
  Then `compare` is null

AC7 — The page says runs were deleted
  Given a range that starts before the oldest kept day
  When the page loads
  Then an info line above the tiles says runs before that day have been deleted

AC8 — Cleanup turned off
  Given run cleanup is turned off
  When the summary is requested
  Then `retainedFrom` is null

**Sad path**

AC9 — Unknown time zone
  Given the time zone "Mars/Olympus"
  When any analytics endpoint is called
  Then it answers 400

AC10 — Range backwards
  Given `from` is after `to`
  When any analytics endpoint is called
  Then it answers 400

AC11 — Range too long
  Given a range of 367 days
  When any analytics endpoint is called
  Then it answers 400

AC12 — Half a comparison, or a different length
  Given `compareFrom` without `compareTo`, or a comparison one day longer than the range
  When any analytics endpoint is called
  Then it answers 400

AC13 — A remembered workspace I've lost
  Given the user's remembered workspace is one they can no longer access
  When they open Analytics
  Then it shows all workspaces

## AN-8: Open Analytics

As an Automate user,
I want to reach Analytics from the Automate sidebar and from the Overview dashboard,
so that I can find it without being told where it is.

AC1 — In the sidebar
  Given an Automate user on a site with at least one workspace
  When they open the Automate section
  Then the sidebar shows Analytics under a Monitoring heading

AC2 — It opens
  Given the Automate sidebar
  When the user clicks Analytics
  Then the Analytics page opens with its Overview view

AC3 — Overview links to it
  Given the Overview dashboard on a site with automations
  When the user clicks the Analytics link under the status cards
  Then the Analytics page opens

**Sad path**

AC4 — No workspaces yet
  Given a site with no workspaces
  When the user opens the Automate section
  Then the Monitoring heading isn't shown

AC5 — No link on the welcome screen
  Given the Overview dashboard in its welcome state
  When it loads
  Then there's no Analytics link

AC6 — No section access
  Given a backoffice user without access to the Automate section
  When they call any analytics endpoint
  Then it answers 403
