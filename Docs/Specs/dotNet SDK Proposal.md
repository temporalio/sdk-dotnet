# Temporal .NET SDK. Architecture & Strategy
## Proposal

We are currently (Feb 2022) planning to provide support for .NET developers to use Temporal. Here, we discuss the scope for the initial version and propose the API shape.

To scope the discussion, we separate the SDK proposal specification in a few complementing documents. The documents are available online and the links can be found below.

* **Part 1: Overview & Client SDK** ([document link](https://docs.google.com/document/d/1qDT_x5I_cnZd6rZDVRVqijylidiGI7--k6ohN7apJg0/edit?usp=sharing)).
  - Overview over the SDK architecture.
  - Workflow management APIs.
  - Convention-based workflow invocation APIs.
   - Interface-based workflow invocation APIs.

* **Part 2: Worker Host Config & Basic Workflow Execution** ([document link](https://docs.google.com/document/d/1iblPmiOmSnpP5mMwxthL-Mgn0KN-AGRb8hn1-Jpu26E/edit?usp=sharing)).
  - APIs for setting up, configuring and starting a workflow worker host.
  - Trivial Workflow implementation.
  - Discussion of general restrictions on workflow code to ensure determinism.
  - Discussion of .NET implementation of asynchrony, multithreading and how it affects workflow code.

* **Part 3: Other Workflow & Activity Execution Features** ([document link](https://docs.google.com/document/d/1eEAPtFqtNM2F0UzMQ79Luyc4mny14VVw_2kQl7MXoms/edit?usp=sharing)).
  - Invoking and implementing activities.
  - Continue-As-New.
  - Deterministic equivalents for common non-deterministic APIs.
  - Timers.
  - Cancellation.
  - Dynamic workflows.
  - . . .
