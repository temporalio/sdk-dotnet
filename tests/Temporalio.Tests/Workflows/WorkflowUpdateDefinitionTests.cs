#pragma warning disable CA1822 // We don't want to force workflow methods to be static

namespace Temporalio.Tests.Workflows;

using Temporalio.Workflows;
using Xunit;

public class WorkflowUpdateDefinitionTests
{
    [Fact]
    public void FromMethod_DifferentValidators_PreservesValidator()
    {
        var method = typeof(UpdateHandlers).GetMethod(nameof(UpdateHandlers.UpdateAsync))!;
        var firstValidator = typeof(UpdateHandlers).GetMethod(nameof(UpdateHandlers.ValidateFirst))!;
        var secondValidator = typeof(UpdateHandlers).GetMethod(nameof(UpdateHandlers.ValidateSecond))!;

        var withoutValidator = WorkflowUpdateDefinition.FromMethod(method);
        var first = WorkflowUpdateDefinition.FromMethod(method, firstValidator);
        var second = WorkflowUpdateDefinition.FromMethod(method, secondValidator);

        Assert.Null(withoutValidator.ValidatorMethod);
        Assert.Equal(firstValidator, first.ValidatorMethod);
        Assert.Equal(secondValidator, second.ValidatorMethod);
        Assert.Equal(withoutValidator.Name, first.Name);
        Assert.Equal(first.Name, second.Name);
        Assert.Same(withoutValidator, WorkflowUpdateDefinition.FromMethod(method));
        Assert.Same(first, WorkflowUpdateDefinition.FromMethod(method, firstValidator));
        Assert.Same(second, WorkflowUpdateDefinition.FromMethod(method, secondValidator));
    }

    [Fact]
    public void FromMethod_CachedWithoutValidator_ValidatesValidatorSignature()
    {
        var method = typeof(UpdateHandlers).GetMethod(nameof(UpdateHandlers.OtherUpdateAsync))!;
        WorkflowUpdateDefinition.FromMethod(method);

        var invalidReturn = typeof(UpdateHandlers).GetMethod(nameof(UpdateHandlers.ValidateWithResult))!;
        var returnException = Assert.Throws<ArgumentException>(
            () => WorkflowUpdateDefinition.FromMethod(method, invalidReturn));
        Assert.Contains("must be void", returnException.Message);

        var invalidParameters = typeof(UpdateHandlers).GetMethod(nameof(UpdateHandlers.ValidateWithWrongParameters))!;
        var parametersException = Assert.Throws<ArgumentException>(
            () => WorkflowUpdateDefinition.FromMethod(method, invalidParameters));
        Assert.Contains("must have the same parameters", parametersException.Message);
    }

    public class UpdateHandlers
    {
        [WorkflowUpdate]
        public Task UpdateAsync(string value) => Task.CompletedTask;

        [WorkflowUpdate]
        public Task OtherUpdateAsync(string value) => Task.CompletedTask;

        public void ValidateFirst(string value)
        {
        }

        public void ValidateSecond(string value)
        {
        }

        public string ValidateWithResult(string value) => value;

        public void ValidateWithWrongParameters(int value)
        {
        }
    }
}
