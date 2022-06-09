# Welcome to the Temporal SDK for .NET.

Temporal SDK for .NET is a set of libraries and tools that enable writing applications using any .NET language that are based on the [Temporal programming model](./What_is_Temporal.md).

## <big>🏗</big> The SDK is currently under construction.

Temporal SDK for .NET is currently in early stages of development.

#### Roadmap:

📅 **Client SDK. Alpha.** _Release in June '22_.  
<small>Allows .NET applications to act as "clients" to Temporal-applications written in any language readily supported by Temporal.</small>  
<big> ⇣ </big>   
📅 **Activity Worker SDK. Alpha.** _Work in progress_.  
<small>Allows writing Temporal activities using .NET. The activities can be orchestrated by workflows written in any language readily supported by Temporal.</small>  
<big> ⇣ </big>   
📅 **Workflow Worker SDK. Alpha.** _Planned_.  
<small>Allows writing complete Temporal applications, including Workflows, in .NET (or using a mixture of any languages readily supported by Temporal).</small>  
<big> ⇣ </big>   
📅 **Client & Worker SDK. Beta.** _Planned_.  
<small>Offers full party with other Temporal SDKs. Can handle pre-production workloads and production workloads under careful observation, including at scale.</small>  
<big> ⇣ </big>   
📅 **Client & Worker SDK. GA v1.** _Planned_.  
<small>Offers full party with other Temporal SDKs, including the latest system features. Confidently handles mission-critical production workloads at hyper-scale.</small>  

#### Detailed work items tracker:

* <https://github.com/orgs/temporalio/projects/11/views/4>

#### Issues & Work items repository:

* <https://github.com/temporalio/sdk-dotnet/issues>


## Supported platforms

The Temporal .NET SDK is supported on the following .NET platforms:

* .NET Framework 4.6.2 or later
* .NET Core 3.1 or later
* .NET 6 or later

The Temporal .NET SDK is supported on the following operating systems:

* Windows Server 2016 or later
* Windows 10 & 11
* Linux

We do not _officially_ support MacOS in production scenarios. However, we ship an ("officially unsupported") MacOS release that you can use for local development or for any other purpose at your own risk. Our CI gates include MacOS, and we have active contributors who use Macs for their development.


## Engineering Partnerships

Our .NET SDK is currently being built.
We are keen to make sure that what we build is what you need.
So, we'd love to learn about your specific use-cases and requirements. In addition, we are keen to work with partners who can test our early pre-release versions. Such partnerships tend to be win-win scenarios, as such support may enable us to prioritize your requirements.

In addition, we happily accept contributions. We can collaborate on features and in certain cases, your contributions can affect our roadmap and prioritization.  
Please reach out to us!

* Join our public Slack channel:  
[https://**temporalio**.slack.com/channels/**dotnet-sdk**](https://temporalio.slack.com/channels/dotnet-sdk).
* File a Github issue:  
[https://github.com/**temporalio/sdk-dotnet/issues**](https://github.com/temporalio/sdk-dotnet/issues).

In addition, please see our [Contribution Guide](./Contribution_Guide.md).
