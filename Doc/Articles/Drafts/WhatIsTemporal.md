# What is Temporal?

> [!CAUTION]    
> **This is a draft/work in progress.
> Information in this article has not been reviewed and may be incomplete.**

Temporal is a programming model for the Cloud that makes writing highly scalable and reliable Apps as straight forward as simple software that runs on a single machine.

## Why Temporal?

Temporal users consistently name it as one of the most useful and impactful software development tools around. What is the problem that Temporal solves?

It removes much of "overhead" related to writing hyper-scalable, hyper-available and hyper-reliable applications. You get to focus on the business logic.

Could Developers report that they spend up to an order of magnitude or more time and effort on developing and maintaining boilerplate components of modern distributed architectures, than on core business logic of their applications.

Modern Cloud apps are complex. Asynchronous request processing, contextual load balancers, persistent queues, sharding, partitioning, control- and data-planes, failover resiliency, distributed transactions, independently scalable micro-services, are just some of the things you need to take care of. And, do not forget: on-call.

Temporal provides "magic" that takes care of many of those "architecture overheads". You write code using your favorite programming language and host it in the Cloud on a platform of your choice. Temporal's "magic" makes sure that your code is executed correctly, retried when needed, subroutine results are persisted, transactions are not torn. You get to focus on the essentials. Temporal does the rest.

## This is too good to be true! Is it?

Yes, a little. But we are engineers, not magicians. In reality, Temporal's "magic" is a compelling programming model backed by a set of convenient SDKs and a powerful backend-system.

There is a smooth learning curve. You will get started with solving common problems very quickly, and you will need to learn more to address specialized scenarios. But compared to life before Temporal, life _with_ Temporal will seem like magic...

...Or perhaps not? For most developers who try it, it does.<br />
There is only one way to find out whether it will change your life. Try it.

## What's the secret sauce?

Section plan:

Split code in core logic (workflow / orchestrion) and activities. 
Workflow is superhero. Reliable retirable, scalable.
Restriction: deterministic.

Activities: I/O or any other logic. No restrictions.

Temporal SDK takes over any interactions between workflow and environment, guarantees determinism, so it can be resumed exactly where stopped if it fails.
As a result, local state appears magically persistent, nothing needs to be done by the app tp manage it.
Activities are initiated at least once, an activity that completed is never re-executed.

# How does it work?

Section plan:

* App (workflow) runs on any platform you like. Just uses the SDK to interact with the environment. "workers".

* SDK intercepts all interactions with the environment, ensures determinism, restarts, replays. Calls activities and provides results from the past on replays.

* Backend orchestrates: signals to workers when there are requests to process, invokes activities on behalf of workers, stores activity results and provides them when required.

# What to know more?

Link to Temporal docs site.

For writing .NET Apps, link to next sections.
