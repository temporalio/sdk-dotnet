# Amazon S3 External Storage Driver

> [!WARNING]
> This extension is experimental and may change in the future.

This extension stores and retrieves Temporal payloads in Amazon S3 using external storage.

It has no AWS dependency. It defines `S3StorageDriver` and the `IS3StorageDriverClient` interface,
and you supply the S3 client. Use the `Temporalio.Extensions.Aws.S3Driver.AwsSdk` package for a
client backed by the official AWS SDK, or implement `IS3StorageDriverClient` yourself.

## Quick Start

```csharp
using Amazon.S3;
using Temporalio.Extensions.Aws.S3Driver;
using Temporalio.Extensions.Aws.S3Driver.AwsSdk;

var driver = new S3StorageDriver(
    new AwsSdkS3StorageDriverClient(new AmazonS3Client()),
    "my-temporal-payloads");
```

To choose the bucket per payload, pass a function instead of a bucket name:

```csharp
var driver = new S3StorageDriver(
    client,
    (context, payload) => payload.Data.Length > 10 * 1024 * 1024 ? "large-payloads" : "small-payloads");
```

## Required IAM Permissions

Before uploading a payload, the driver checks whether the object already exists with a `HeadObject`
request. That check needs `s3:GetObject`. It also needs `s3:ListBucket`, because without it S3
reports a missing object as `403 Forbidden` instead of `404 Not Found`, and the upload fails.

```json
[
  {
    "Effect": "Allow",
    "Action": ["s3:PutObject", "s3:GetObject"],
    "Resource": "arn:aws:s3:::my-temporal-payloads/*"
  },
  {
    "Effect": "Allow",
    "Action": "s3:ListBucket",
    "Resource": "arn:aws:s3:::my-temporal-payloads"
  }
]
```

Components that store payloads need all three permissions. Components that only retrieve payloads
need only `s3:GetObject`.

## S3 Storage Key Specification

All Temporal S3 drivers generate S3 keys in a consistent manner.

### Key format

Workflow key:

```text
v0/ns/{namespace}/wt/{workflow-type}/wi/{workflow-id}/ri/{run-id}/d/{hash-algorithm}/{hex-digest}
```

Activity key:

```text
v0/ns/{namespace}/at/{activity-type}/ai/{activity-id}/ri/{run-id}/d/{hash-algorithm}/{hex-digest}
```

Fallback key (unknown target):

```text
v0/d/{hash-algorithm}/{hex-digest}
```

- If no namespace, workflow, or activity information is available, the fallback is used.
- Dynamic path segments are percent-encoded (rules below).
- Missing values (including a missing `run-id`) are encoded as `null`.
- `hex-digest` is lower-case SHA-256 hex (64 characters).

### Percent-encoding rules

The Temporal SDKs escape anything that isn't listed in S3's safe character set: https://docs.aws.amazon.com/AmazonS3/latest/userguide/object-keys.html

Safe Characters:

```text
Alphanumeric characters
  0-9
  a-z
  A-Z

Special characters
  Exclamation point (!)
  Hyphen (-)
  Underscore (_)
  Period (.)
  Asterisk (*)
  Single quotation mark (')
  Opening parenthesis (()
  Closing parenthesis ())
```

### Examples

Workflow key example:

```text
input:
  namespace=payments prod
  workflow-type=ChargeWorkflow
  workflow-id=order+123=abc
  run-id=3f1d6c7a-8b2e-4f7a-9d0a-87a6f95e4d31
  hash-algorithm=sha256
  hex-digest=9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08

output:
  v0/ns/payments%20prod/wt/ChargeWorkflow/wi/order%2B123%3Dabc/ri/3f1d6c7a-8b2e-4f7a-9d0a-87a6f95e4d31/d/sha256/9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08
```

Activity key example:

```text
input:
  namespace=payments prod
  activity-type=Capture/Charge
  activity-id=activity id+42
  run-id=9e1d1fd9-2f8a-4c40-93e2-731f31b9268b
  hash-algorithm=sha256
  hex-digest=2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824

output:
  v0/ns/payments%20prod/at/Capture%2FCharge/ai/activity%20id%2B42/ri/9e1d1fd9-2f8a-4c40-93e2-731f31b9268b/d/sha256/2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824
```
