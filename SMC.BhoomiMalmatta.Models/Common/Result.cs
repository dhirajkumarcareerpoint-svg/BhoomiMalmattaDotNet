using System;

namespace SMC.BhoomiMalmatta.Models.Common
{
    /// <summary>Machine-readable error with a Marathi (user-facing) message.</summary>
    public sealed class Error
    {
        public static readonly Error None = new Error(string.Empty, string.Empty);
        public Error(string code, string message) { Code = code; Message = message; }
        public string Code { get; private set; }
        public string Message { get; private set; }
    }

    public class Result
    {
        protected Result(bool ok, Error error) { IsSuccess = ok; Error = error; }
        public bool IsSuccess { get; private set; }
        public bool IsFailure { get { return !IsSuccess; } }
        public Error Error { get; private set; }
        public static Result Success() { return new Result(true, Error.None); }
        public static Result Failure(Error e) { return new Result(false, e); }
        public static Result<T> Success<T>(T value) { return new Result<T>(value, true, Error.None); }
        public static Result<T> Failure<T>(Error e) { return new Result<T>(default(T), false, e); }
    }

    public sealed class Result<T> : Result
    {
        private readonly T _value;
        internal Result(T value, bool ok, Error error) : base(ok, error) { _value = value; }
        public T Value
        {
            get
            {
                if (!IsSuccess) throw new InvalidOperationException("Result is a failure: " + Error.Code);
                return _value;
            }
        }
    }

    public interface IClock { DateTime UtcNow { get; } }
    public sealed class SystemClock : IClock { public DateTime UtcNow { get { return DateTime.UtcNow; } } }
}
