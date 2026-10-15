# Native ML Lab UI implementation

`MlLabWindow` is a WPF view over `IMlLabService`. Its four source files are
`MlLabWindow.xaml`, `MlLabWindow.xaml.cs`, `MlLabWindow.Results.cs` and
`MlLabWindow.Assistant.cs`. App owns the service and optional language-model host;
the window never disposes either. App/menu/tray/chat composition is outside these
files. The compact companion's at-rest layout is unchanged by opening the Lab.

## Lifecycle and dispatch

The constructor accepts `IMlLabService` and an optional
`Func<CancellationToken, Task<IChatModel>>`. On first load it obtains capabilities
and saved records. The algorithm selector uses the worker's capability registry,
not a separate UI algorithm allowlist. Manual actions are closed Application
records. No UI action submits Python, shell commands or generated code.

`ExecuteActionAsync` snapshots limits, admits one request from this view and awaits
the App-owned service without tying its token to the window. Training refreshes
the registry after presenting the completed result; registry-refresh failure does
not erase or reclassify a completed training result. A new view reads `service.Jobs`
and subscribes to `JobChanged`, marshalled through WPF's Dispatcher. The Jobs button
cancels the selected active job, or the most recent active job if no active row is
selected. At most 100 job rows are retained in a view.

Closing cancels language-model planning, detaches the job subscription and releases
the displayed bitmap. Awaited ML operations may continue in the App-owned service;
their completion checks the closed flag before changing controls. `BeginShutdown`
cancels planning. App performs service disposal and owned-worker shutdown.

## Data binding and bounds

Dataset, statistics, history, model and prediction tables use read-only `DataTable`
views populated with literal strings. Previews have at most 100 rows; statistics
and feature selection have at most 128 features; registry views have at most 500
records defensively; the backend currently returns at most 200 and its listing
warning is shown in History. Normal table cells are truncated at 512 UTF-16 code units without splitting
a surrogate pair. Metadata has a 262,144-character display bound and marks truncation.
The underlying full artifact is separate from the displayed excerpt.

History uses `MlDescribe` to retrieve the full persisted record before opening results.
Extended selection invokes `MlCompare` for 2–5 experiment IDs; the worker checks exact
cohort compatibility and the UI presents the returned real metric/runtime/size table.

Inspection establishes an exact `MlDatasetSelection`. Manual training rejects a
changed path/key/start/row limit until that selection has been inspected. Target
columns are excluded from features even if a checkbox is later reselected. Inputs
have bounded lengths and invariant-culture numeric parsing. Backend validation
remains authoritative, including algorithm compatibility and supported parameters.

Inspection uses a bounded leading range, not representative sampling. Both the
Data instructions and the inspected-subset label disclose this. The displayed
metadata carries actual selected-row count; this can be below the training row
limit. Source content, warnings and metadata are literal WPF text, not executable
Markdown/HTML.

## Plot viewer

`LoadProjectPlot` accepts only PNG paths lexically beneath the configured project,
rejects reparse points in the path, opens a read-only shared-read stream and limits
file size to 8 MiB. It validates the PNG signature and initial dimensions, requiring
at most 10,000 pixels per side and 16 million source pixels. The WPF decoder loads
from the stream, downsamples to at most 1,600 pixels wide, uses `OnLoad`, then freezes
the bitmap. No remote URI, web control, arbitrary process or navigation is involved.
Only the selected image is displayed; at most 32 plot choices are shown.

These checks bound the normal artifact path; they do not claim isolation from a
malicious actor concurrently replacing local filesystem ancestors or a codec flaw.

## Conversational proposal boundary

The optional `IStructuredChatModel` port supplies a JSON schema to the existing local
worker. Its current-selection alias is resolved by Application, preventing the model
from supplying a fabricated dataset path. The planning request snapshots prompt, mode, current selection and inspected column
names. Columns are omitted if the current selection differs from the inspected
selection. A three-minute planning cancellation source bounds the view's request;
the local model also enforces its own deadlines. The result is a validated typed
`MlProposal`, displayed as literal explanation and configuration text.

The UI captures the proposal's action and compute limits and enables a separate
Run button. Run consumes the proposal once and invokes the same closed dispatcher.
Changing configuration controls afterward cannot alter the captured plan. Failed
or cancelled planning leaves no executable proposal. Learn/Research/Auto are
explanation modes, with one reviewed action in all three modes.

## Native verification entry points

Reviewer-owned `MlLabSmokeChecks` uses named controls plus internal
`ExecuteActionAsync`, `IsBusy` and `CurrentStatus`; it must wait through the initial
capabilities/list sequence. Train's task includes its registry refresh. A direct
inspect action updates the selection fields and inspection state, so later native
configuration actions exercise the same guard as a user click.

Verification should cover minimum 760×560-DIP geometry, wrapped tab headers,
scrollable controls, literal untrusted text, target exclusion, changed-selection
rejection, proposal-before-run, responsive Dispatcher during real worker activity,
cancel/reuse and reopening while a service job remains active. Pure property
assertions do not establish keyboard reachability or physical rendering. The
review record owns measured pass/fail results; this implementation note claims
neither full ML Phase 1 nor production qualification. Final executed evidence is in
[Testing](../TESTING.md): 57 native checks and two live planner proposals using the
unchanged production transport, alongside .NET/Python and existing desktop regression.
