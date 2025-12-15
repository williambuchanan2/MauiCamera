using System;
using System.Collections.Generic;

namespace MauiCamera
{
    public class BaseResponseCommon
    {
        public bool Success { get; set; } = true;
        public List<ErrorCode> Errors { get; set; } = new List<ErrorCode>();
        public object Data { get; set; }
        public Dictionary<string,object> Tags { get; set; }

        public void SetError(ErrorCode error)
        {
            Success = false;
            Errors.Add(error);
        }

        public string GetErrorsAsString()
        {
            string result = "";
            foreach (ErrorCode error in Errors)
            {
                result += error.Message.ToString() + Environment.NewLine;
            }

            return result;
        }
    }
}
