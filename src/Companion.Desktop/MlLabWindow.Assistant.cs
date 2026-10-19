using System.Windows;
using Companion.Application.Conversation;
using Companion.Application.MachineLearning;

namespace Companion.Desktop;

public partial class MlLabWindow
{
    private LayaAdvice? taskAdvice;
    private string? advisedPrompt;

    private long proposalRevision;
    internal bool CanAcceptResearchRequest => !closed && !busy && planning is null;
    internal bool IsBoundTo(ResearchLabSelection selection) => projectId == selection.Project.Id && ReferenceEquals(service, selection.Service);

    internal void AcceptResearchRequest(Guid destination, string request)
    {
        if (projectId != destination || !CanAcceptResearchRequest)
            throw new MlException("stale_handoff", "The Lab destination changed. Review a new transfer.");
        ResearchRequestHandoff.ValidateRequest(request);
        InvalidateProposal();
        AssistantRequest.Text = request;
        TransferredRequestLabel.Text = "Transferred Chat draft · Exact request below. No model or ML action ran during transfer.";
        LabTabs.SelectedItem = AssistantTab;
        ProposalText.Text = modelFactory is null
            ? "Local conversational planning is unavailable. Configure the local model, or use the explicit dataset controls. No ML action ran."
            : "Choose a dataset in this project's Data tab if none is selected, then select Propose a plan. No ML action has run.";
        StatusLabel.Text = "Request transferred to this project. Generated plans and completed computations appear separately.";
        AssistantRequest.Focus();
    }

    private void InvalidateProposal()
    {
        proposalRevision++;
        planning?.Cancel();
        var hadProposal = proposedAction is not null;
        proposedAction = null; proposedLimits = null;
        RunProposalButton.IsEnabled = false;
        taskAdvice = null; advisedPrompt = null;
        if (hadProposal) ProposalText.Text = "The request, dataset or limits changed. Propose a new plan before running.";
    }

    private async void AnalyzeTask_Click(object sender, RoutedEventArgs e) => await AnalyzeTaskAsync();

    internal async Task AnalyzeTaskAsync()
    {
        if (closed || busy || planning is not null || laya is null) return;
        taskAdvice = null; advisedPrompt = null;
        proposedAction = null; proposedLimits = null; RunProposalButton.IsEnabled = false;
        AnalyzeTaskButton.IsEnabled = ProposeButton.IsEnabled = false;
        CancelPlanButton.IsEnabled = true;
        LayaAdviceText.Text = "Analyzing the task with the optional local CPU LAYA model. No ML action has run.";
        LayaAdviceExpander.IsExpanded = true;
        ProposalText.Text = "Task analysis is advisory. Propose a plan to produce a new action for review.";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        planning = cancellation;
        var revision = proposalRevision;
        try
        {
            var prompt = AssistantRequest.Text;
            var advice = await laya.AdviseAsync(prompt, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (closed || revision != proposalRevision) return;
            taskAdvice = advice; advisedPrompt = prompt;
            var scores = string.Join("\n", advice.Probabilities.OrderByDescending(item => item.Value)
                .Select(item => $"{item.Key}: {item.Value:P1}"));
            LayaAdviceText.Text = $"Suggested category: {advice.Category}\n\n{advice.Explanation}\n\nModel category scores (not calibrated confidence):\n{scores}\n\nCPU inference: {advice.ElapsedSeconds:F2} s; checkpoint {advice.ModelRevision}.\n" +
                (advice.Truncated ? "The request was truncated; this suggestion will not inform the plan.\n" : "") +
                (advice.CanSuggest ? "Propose a plan can use this as a nonbinding hint for this unchanged request.\n" : "The category is ambiguous; specify your task. This suggestion will not inform the plan.\n") +
                string.Join("\n", advice.Warnings) + "\nNo action ran. Recommendations do not establish scientific conclusions.";
            StatusLabel.Text = "LAYA task advice is ready in Local LAYA advice. You can override it in your request or explicit controls.";
        }
        catch (OperationCanceledException) { if (!closed) LayaAdviceText.Text = "LAYA analysis stopped. No action ran."; }
        catch (Exception error) when (error is LayaException or ArgumentException or InvalidOperationException)
        { if (!closed) LayaAdviceText.Text = $"{error.Message}\nNo action ran. Existing chat and configuration controls remain available."; }
        finally
        {
            if (ReferenceEquals(planning, cancellation)) planning = null;
            if (!closed)
            {
                AnalyzeTaskButton.IsEnabled = laya is not null;
                ProposeButton.IsEnabled = modelFactory is not null;
                CancelPlanButton.IsEnabled = false;
            }
        }
    }

    private async void Propose_Click(object sender, RoutedEventArgs e) => await ProposeAsync();

    internal async Task ProposeAsync()
    {
        if (closed || busy || planning is not null || modelFactory is null) return;
        proposedAction = null;
        proposedLimits = null;
        RunProposalButton.IsEnabled = false;
        ProposeButton.IsEnabled = false;
        AnalyzeTaskButton.IsEnabled = false;
        CancelPlanButton.IsEnabled = true;
        ProposalText.Text = "Preparing a typed proposal with the local model. No ML action has run.";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        planning = cancellation;
        var revision = proposalRevision;
        try
        {
            // Only Lab controls can capture a dataset. Conversation text supplies no path authority.
            var dataset = ReadDataset();
            var limits = ReadLimits();
            MlValidation.Validate(new MlInspect(dataset), limits);
            var prompt = AssistantRequest.Text;
            var mode = AssistantMode.SelectedItem as string ?? "Learn";
            var columns = inspectedSelection == dataset ? features.Select(feature => feature.Name).ToArray() : [];
            var model = await modelFactory(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (closed || revision != proposalRevision) return;
            var hint = advisedPrompt == prompt && taskAdvice is { CanSuggest: true } ? taskAdvice : null;
            var proposal = await new MlConversationPlanner(model).PlanAsync(prompt, dataset, columns, mode, cancellation.Token, hint);
            cancellation.Token.ThrowIfCancellationRequested();
            if (closed || revision != proposalRevision) return;
            // A model proposal cannot expand the user's captured resource budget.
            MlValidation.Validate(proposal.Action, limits);
            proposedAction = proposal.Action;
            proposedLimits = limits;
            ProposalText.Text = $"{proposal.Explanation}\n\nReview this exact action and its paths, features, split and limits before running:\n\n{proposal.ConfigurationJson}\n\nCompute limits: {proposedLimits.MaxRows:N0} rows; {proposedLimits.MemoryMiB:N0} MiB worker memory; {proposedLimits.Threads} CPU threads.\n\nNo action has run. Training and inspection can create saved project artifacts.";
            StatusLabel.Text = "Plan ready for review. Select Run reviewed plan only if its configuration matches your intent.";
        }
        catch (OperationCanceledException) { if (!closed) ProposalText.Text = "Planning stopped. No action ran."; }
        catch (Exception exception) when (exception is MlException or ChatModelException or ArgumentException or InvalidOperationException)
        { if (!closed) ProposalText.Text = $"{exception.Message}\nNo action ran. You can use the explicit configuration controls."; }
        finally
        {
            if (ReferenceEquals(planning, cancellation)) planning = null;
            if (!closed)
            {
                ProposeButton.IsEnabled = modelFactory is not null;
                AnalyzeTaskButton.IsEnabled = laya is not null;
                CancelPlanButton.IsEnabled = false;
                RunProposalButton.IsEnabled = proposedAction is not null && !busy;
            }
        }
    }

    private void CancelPlan_Click(object sender, RoutedEventArgs e) => planning?.Cancel();

    private async void RunProposal_Click(object sender, RoutedEventArgs e)
    {
        if (closed || proposedAction is not { } action || proposedLimits is not { } limits || planning is not null || busy) return;
        // Consume a proposal once. Repeat execution needs a newly reviewed proposal.
        proposedAction = null;
        proposedLimits = null;
        RunProposalButton.IsEnabled = false;
        var result = await ExecuteActionAsync(action, limits);
        if (result is not null && !closed && action is MlInspect inspect)
        {
            DatasetPath.Text = inspect.Dataset.Path;
            DatasetKey.Text = inspect.Dataset.Key ?? "";
            StartRow.Text = inspect.Dataset.Start.ToString(System.Globalization.CultureInfo.InvariantCulture);
            RowLimit.Text = inspect.Dataset.RowLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);
            inspectedSelection = inspect.Dataset;
        }
    }
}
