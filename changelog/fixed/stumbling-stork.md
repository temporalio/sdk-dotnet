Fixed a `NullReferenceException` when a workflow decoded a cancelled child workflow start carrying a failure, with a payload codec that implements `IWithSerializationContext<IPayloadCodec>`.
