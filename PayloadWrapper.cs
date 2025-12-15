using System;
using System.Collections.Generic;
using System.Linq;
namespace MauiCamera
{
    /// <summary>
    /// Strongly typed generic version of <see cref="PayloadWrapper"/>
    /// </summary>
    public sealed class PayloadWrapper<T> : PayloadWrapper
    {
        public new T Data { get; set; }
    }

    /// <summary>
    /// Encapsulates data and meta data (ie. errors, tags) being returned from an api endpoint call
    /// </summary>
    public abstract class PayloadWrapper
    {
        public object Data { get; set; }
        public IList<ErrorCode> Errors { get; set; } = new List<ErrorCode>();
        public bool Success { get; set; } = true;
        public IDictionary<string, object> Tags { get; set; } = new Dictionary<string, object>();

        public string GetErrorsAsString()
        {
            return string.Join(' ', Errors.ToList().Select(o => $"{o.Message}{Environment.NewLine}"));
        }

        public void SetError(ErrorCode error)
        {
            Success = false;
            Errors.Add(error);
        }
    }
}

