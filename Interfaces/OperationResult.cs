namespace EcommerceSystem.Interfaces
{
    public class OperationResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public static OperationResult Ok() => new() { Success = true };
        public static OperationResult Fail(string error) => new() { Success = false, Error = error };
    }

    public class OperationResult<T> : OperationResult
    {
        public T? Value { get; init; }
        public static OperationResult<T> Ok(T value) => new() { Success = true, Value = value };
        // Hides OperationResult.Fail(string) to return a typed OperationResult<T>
        public new static OperationResult<T> Fail(string error) => new() { Success = false, Error = error };
    }
}