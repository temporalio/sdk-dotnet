# <small>Temporal Data Model:</small><br />Workflows, -Chains and -Runs

    
In this article we consider similarities and differences between 2 key concepts in Temporal:
"_Workflow Chain_" and "_Workflow Run_".  
In most typical applications you will never need to dive deep into this topic. You will simply _use_ Workflows and everything will "just work". However, Temporal experts and users of certain advanced scenarios will need to understand this space.

## Workflow Chain

Technically, a Temporal workflow is a chain-sequence of one or more _Workflow Runs_ from the initial invocation of a workflow to its eventual conclusion.
Thus, a logical workflow (as in "just" workflow, or "the" workflow) is really a _Workflow Chain_.           

## Workflow Run

A _Workflow Run_ is single execution of the main workflow routine.

Completely executing a logical workflow from the initial invocation to the eventual conclusion involves a chain of one or more _Workflow Runs_.

The Temporal server orchestrates the execution of workflows by ensuring that Workers execute _Workflow Runs_ as required to complete a _Workflow Chain_ (aka a logical Workflow).

## .NET Client SDK Data Model

* _Workflow Chains_ are modeled by instances of type `IWorkflowHandle`.
* _Workflow Runs_ are modeled by instances of type `IWorkflowRunHandle`.


In most scenarios, users only need to interact with logical workflows, i.e., with _Workflow Chains_ via the respective `IWorkflowHandle` instances. For example, when a user needs to send a signal to a workflow (or perform a query, terminate, cancel, etc...), they invoke a corresponding `IWorkflowHandle` API. Under the covers, the handle automatically interacts with the current _Run_ within the _Workflow Chain_ represented by the handle.

Similarly, when a users polls for the result of a workflow, the respective `IWorkflowHandle` API automatically "follows" the _Chain_ until the "final" _Run_ of a chain completes.

However, in some advanced scenarios users need to explicitly interact with a specific _Run_ within the _Workflow Chain_ representing a particular logical workflow. This is done using a `IWorkflowRunHandle` instances.

Examples that demonstrate how to do that can be found in [this sample](https://github.com/temporalio/sdk-dotnet/blob/master/Src/Samples/WorkflowClient.UsageSamples/Temporal.Sdk.WorkflowClient.UsageSamples/public/Part3_AddressIndividualRuns.cs).

## Specific _Workflow Runs_ within _Workflow Chains_:

As discussed above, a Temporal workflow is a _Chain_ of one or more _Workflow Runs_. Such a _Workflow Chain_ spans from the initial invocation of a workflow to its eventual conclusion.
A _Chain_ may include many _Runs_, however, some of the _Runs_ have a special significance:

- The **FIRST** _Run_ of a _Chain_ is the _Run_ that was initiated when the logical workflow was started.  
Typically, the first _Run_ is used to IDENTIFY the entire chain.
I.e., the workflow-run-id of the first _Run_ within a _Workflow Chain_ identifies not only that particular _Run_, but also the _Workflow Chain_ as a whole.

- The **LATEST** _Run_ of a _Chain_ is the _Run_ that has started most recently.  
Such LATEST _Run_ is sometimes also termed CURRENT. However, technically, the latest _Run_ may have a `Running` status (then the entire _Chain_ is also said to be _Running_) or it may already have _concluded_ (then the entire _Chain_ is said to be _concluded_). To account for this, the .NET Workflow Client SDK uses the term LATEST rather than CURRENT.

- The **FINAL** _Run_ of a _Chain_ is the _Run_ that concludes the _Chain_. No further _Runs_ are known to follow after the final _Run_ of a given _Workflow Chain_. In most scenarios it is not known with what Status a _Run_ will conclude, until it does so. Therefore, a workflow may not have a final _Run_ until it eventually concludes.

Examples for addressing specific Runs within a workflow can be found [here](https://github.com/temporalio/sdk-dotnet/blob/db93bbedf9af6c03dfe9d09b22109df853e0139f/Src/Samples/WorkflowClient.UsageSamples/Temporal.Sdk.WorkflowClient.UsageSamples/public/Part3_AddressIndividualRuns.cs#L143-L242).