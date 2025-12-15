using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace MauiCamera
{
    [DataContract]
    public class ErrorCode
    {
        [DataMember]
        public int Code { get; set; }
        [DataMember]
        public string Message { get; set; }

        public ErrorCode()
        {

        }


        public ErrorCode(int code, string message)
        {
            Code = code;
            Message = message;
        }
    }
}
