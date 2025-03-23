using System;
using System.Collections.Generic;
using System.Text;

namespace Growatt.Sdk
{
    public class ApiException : Exception
    {
        public int ErrorCode { get; }

        public ApiException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
