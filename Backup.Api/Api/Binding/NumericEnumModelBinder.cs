using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Backup.Api.Binding;

public sealed class NumericEnumModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        ValueProviderResult valueResult = bindingContext.ValueProvider.GetValue(
            bindingContext.ModelName
        );

        if (valueResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

        string? rawValue = valueResult.FirstValue;

        if (string.IsNullOrWhiteSpace(rawValue))
            return Task.CompletedTask;

        Type modelType = bindingContext.ModelType;
        Type enumType = Nullable.GetUnderlyingType(modelType) ?? modelType;

        if (!enumType.IsEnum)
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"Type '{modelType.Name}' is not an enum."
            );
            return Task.CompletedTask;
        }

        if (!int.TryParse(rawValue, out int numericValue))
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"The value '{rawValue}' is invalid. Use a numeric enum value."
            );
            return Task.CompletedTask;
        }

        if (!Enum.IsDefined(enumType, numericValue))
        {
            string allowed = string.Join(
                ", ",
                Enum.GetValues(enumType).Cast<object>().Select(value => Convert.ToInt32(value))
            );

            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"The value '{numericValue}' is invalid. Allowed values: {allowed}."
            );
            return Task.CompletedTask;
        }

        object parsed = Enum.ToObject(enumType, numericValue);
        bindingContext.Result = ModelBindingResult.Success(parsed);
        return Task.CompletedTask;
    }
}
