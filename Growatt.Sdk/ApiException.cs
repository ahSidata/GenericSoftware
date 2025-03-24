namespace Growatt.Sdk
{
    public class ApiException : Exception
    {
        #region Public Constructors

        public ApiException(string message, int errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        #endregion Public Constructors

        #region Properties

        public int ErrorCode { get; }

        #endregion Properties
    }
}
